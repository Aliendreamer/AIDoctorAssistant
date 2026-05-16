using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;
using Microsoft.Data.Sqlite;

namespace MedAssist.AI.Dictionary;

public sealed class BM25VocabService : IBM25VocabStore
{
    private readonly string _connectionString;

    public BM25VocabService(string databasePath)
        => _connectionString = $"Data Source={databasePath};Mode=ReadOnly";

    public async Task<BM25VocabSnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        using var conn = new SqliteConnection(_connectionString);
        conn.Open();

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

    private static float ComputeIdf(int df, int n) =>
        n > 0 ? MathF.Log(1f + (n - df + 0.5f) / (df + 0.5f)) : 0f;
}
