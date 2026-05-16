using MedAssist.Indexer.Database;
using MedAssist.Shared.Models;
using Microsoft.Data.Sqlite;

namespace MedAssist.Indexer.Repositories;

public sealed class BookRepository
{
    private readonly DbInitializer _db;

    public BookRepository(DbInitializer db) => _db = db;

    public async Task UpsertAsync(BookInfo book, CancellationToken cancellationToken = default)
    {
        await using var connection = _db.CreateConnection();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO books (id, title, author, language, edition, total_chunks, status, indexed_at)
            VALUES ($id, $title, $author, $language, $edition, $totalChunks, $status, $indexedAt)
            ON CONFLICT(id) DO UPDATE SET
                title        = excluded.title,
                author       = excluded.author,
                language     = excluded.language,
                edition      = excluded.edition,
                total_chunks = excluded.total_chunks,
                status       = excluded.status,
                indexed_at   = excluded.indexed_at;
            """;
        cmd.Parameters.AddWithValue("$id", book.Id);
        cmd.Parameters.AddWithValue("$title", book.Title);
        cmd.Parameters.AddWithValue("$author", book.Author);
        cmd.Parameters.AddWithValue("$language", book.Language);
        cmd.Parameters.AddWithValue("$edition", book.Edition);
        cmd.Parameters.AddWithValue("$totalChunks", book.TotalChunks);
        cmd.Parameters.AddWithValue("$status", book.Status.ToString().ToLowerInvariant());
        cmd.Parameters.AddWithValue("$indexedAt", book.IndexedAt?.ToString("O") ?? (object)DBNull.Value);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BookInfo>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _db.CreateConnection();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT id, title, author, language, edition, total_chunks, status, indexed_at FROM books ORDER BY title;";

        var results = new List<BookInfo>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(MapRow(reader));
        }

        return results;
    }

    public async Task<BookInfo?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var connection = _db.CreateConnection();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT id, title, author, language, edition, total_chunks, status, indexed_at FROM books WHERE id = $id;";
        cmd.Parameters.AddWithValue("$id", id);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? MapRow(reader) : null;
    }

    private static BookInfo MapRow(SqliteDataReader reader) => new()
    {
        Id = reader.GetString(0),
        Title = reader.GetString(1),
        Author = reader.GetString(2),
        Language = reader.GetString(3),
        Edition = reader.GetString(4),
        TotalChunks = reader.GetInt32(5),
        Status = Enum.Parse<BookStatus>(reader.GetString(6), ignoreCase: true),
        IndexedAt = reader.IsDBNull(7) ? null : DateTimeOffset.Parse(reader.GetString(7))
    };
}
