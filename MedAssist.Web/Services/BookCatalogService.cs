using MedAssist.Shared.Models;
using Microsoft.Data.Sqlite;

namespace MedAssist.Web.Services;

public sealed class BookCatalogService
{
    private readonly string _connectionString;

    public BookCatalogService(string databasePath)
    {
        _connectionString = $"Data Source={databasePath};Mode=ReadOnly";
    }

    public async Task<IReadOnlyList<BookInfo>> GetAllBooksAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT id, title, author, language, edition, total_chunks, status, indexed_at FROM books WHERE status = 'indexed' ORDER BY title;";

        var results = new List<BookInfo>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new BookInfo
            {
                Id = reader.GetString(0),
                Title = reader.GetString(1),
                Author = reader.GetString(2),
                Language = reader.GetString(3),
                Edition = reader.GetString(4),
                TotalChunks = reader.GetInt32(5),
                Status = Enum.Parse<BookStatus>(reader.GetString(6), ignoreCase: true),
                IndexedAt = reader.IsDBNull(7) ? null : DateTimeOffset.Parse(reader.GetString(7))
            });
        }

        return results;
    }
}
