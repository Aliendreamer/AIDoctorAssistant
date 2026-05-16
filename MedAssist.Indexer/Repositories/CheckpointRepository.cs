using MedAssist.Indexer.Database;
using Microsoft.Data.Sqlite;

namespace MedAssist.Indexer.Repositories;

public sealed record IngestionCheckpoint(
    string BookId,
    int TotalChunks,
    int IndexedChunks,
    int LastChunkIndex,
    string Status,
    DateTimeOffset UpdatedAt);

public sealed class CheckpointRepository
{
    private readonly DbInitializer _db;

    public CheckpointRepository(DbInitializer db) => _db = db;

    public async Task UpsertAsync(IngestionCheckpoint checkpoint, CancellationToken cancellationToken = default)
    {
        await using var connection = _db.CreateConnection();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO ingestion_checkpoints (book_id, total_chunks, indexed_chunks, last_chunk_index, status, updated_at)
            VALUES ($bookId, $totalChunks, $indexedChunks, $lastChunkIndex, $status, $updatedAt)
            ON CONFLICT(book_id) DO UPDATE SET
                total_chunks      = excluded.total_chunks,
                indexed_chunks    = excluded.indexed_chunks,
                last_chunk_index  = excluded.last_chunk_index,
                status            = excluded.status,
                updated_at        = excluded.updated_at;
            """;
        cmd.Parameters.AddWithValue("$bookId", checkpoint.BookId);
        cmd.Parameters.AddWithValue("$totalChunks", checkpoint.TotalChunks);
        cmd.Parameters.AddWithValue("$indexedChunks", checkpoint.IndexedChunks);
        cmd.Parameters.AddWithValue("$lastChunkIndex", checkpoint.LastChunkIndex);
        cmd.Parameters.AddWithValue("$status", checkpoint.Status);
        cmd.Parameters.AddWithValue("$updatedAt", checkpoint.UpdatedAt.ToString("O"));
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IngestionCheckpoint?> GetByBookIdAsync(string bookId, CancellationToken cancellationToken = default)
    {
        await using var connection = _db.CreateConnection();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT book_id, total_chunks, indexed_chunks, last_chunk_index, status, updated_at
            FROM ingestion_checkpoints WHERE book_id = $bookId;
            """;
        cmd.Parameters.AddWithValue("$bookId", bookId);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new IngestionCheckpoint(
            reader.GetString(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetInt32(3),
            reader.GetString(4),
            DateTimeOffset.Parse(reader.GetString(5)));
    }
}
