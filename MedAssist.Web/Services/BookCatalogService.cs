using MedAssist.Data;
using MedAssist.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace MedAssist.Web.Services;

public sealed class BookCatalogService
{
    private readonly IDbContextFactory<MedAssistDbContext> _dbFactory;

    public BookCatalogService(IDbContextFactory<MedAssistDbContext> dbFactory)
        => _dbFactory = dbFactory;

    public async Task<IReadOnlyList<BookInfo>> GetAllBooksAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);

        var books = await db.Books
            .Where(b => b.Status == "indexed")
            .OrderBy(b => b.Title)
            .ToListAsync(cancellationToken);

        return books.Select(b => new BookInfo
        {
            Id = b.Id,
            Title = b.Title,
            Author = b.Author,
            Language = b.Language,
            Edition = b.Edition,
            TotalChunks = b.TotalChunks,
            Status = Enum.Parse<BookStatus>(b.Status, ignoreCase: true),
            IndexedAt = b.IndexedAt
        }).ToList();
    }
}
