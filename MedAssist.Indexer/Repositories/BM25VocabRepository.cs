using MedAssist.Indexer.Database;
using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;
using Microsoft.Data.Sqlite;

namespace MedAssist.Indexer.Repositories;

public sealed class BM25VocabRepository : IBM25VocabStore
{
    private readonly DbInitializer _db;

    public BM25VocabRepository(DbInitializer db) => _db = db;

    public async Task<BM25VocabSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        using var conn = _db.CreateConnection();

        int totalDocs;
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COALESCE(MAX(total_documents), 0) FROM bm25_vocab";
            totalDocs = (int)(long)(await cmd.ExecuteScalarAsync(cancellationToken))!;
        }

        var termIds = new Dictionary<string, uint>();
        var idfWeights = new Dictionary<uint, float>();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT id, term, document_frequency FROM bm25_vocab WHERE document_frequency >= 2 ORDER BY id";
            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var termId = (uint)reader.GetInt64(0);
                var term = reader.GetString(1);
                var df = reader.GetInt32(2);
                termIds[term] = termId;
                idfWeights[termId] = ComputeIdf(df, totalDocs);
            }
        }

        return new BM25VocabSnapshot(termIds, idfWeights, totalDocs);
    }

    public async Task UpsertTermsAsync(
        IReadOnlyDictionary<string, int> termDfs,
        int totalDocs,
        CancellationToken cancellationToken = default)
    {
        using var conn = _db.CreateConnection();
        using var tx = conn.BeginTransaction();

        using (var cmd = conn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = "UPDATE bm25_vocab SET total_documents = @n, updated_at = datetime('now')";
            cmd.Parameters.AddWithValue("@n", totalDocs);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var (term, df) in termDfs)
        {
            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = """
                INSERT INTO bm25_vocab (term, document_frequency, total_documents, updated_at)
                VALUES (@term, @df, @n, datetime('now'))
                ON CONFLICT(term) DO UPDATE SET
                    document_frequency = document_frequency + @df,
                    total_documents = @n,
                    updated_at = datetime('now')
                """;
            cmd.Parameters.AddWithValue("@term", term);
            cmd.Parameters.AddWithValue("@df", df);
            cmd.Parameters.AddWithValue("@n", totalDocs);
            await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        tx.Commit();
    }

    public async Task<int> GetTotalDocumentsAsync(CancellationToken cancellationToken = default)
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COALESCE(MAX(total_documents), 0) FROM bm25_vocab";
        return (int)(long)(await cmd.ExecuteScalarAsync(cancellationToken))!;
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        using var conn = _db.CreateConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM bm25_vocab";
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private static float ComputeIdf(int df, int n) =>
        n > 0 ? MathF.Log(1f + (n - df + 0.5f) / (df + 0.5f)) : 0f;
}
