using MedAssist.Data;
using MedAssist.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace MedAssist.Web.Services;

public sealed class BookCatalogService(MedAssistDbContext db)
{
    public async Task<IReadOnlyList<BookInfo>> GetAllBooksAsync(CancellationToken cancellationToken = default)
    {
        var books = await db.Books
            .Where(b => b.Status == BookStatus.Indexed)
            .OrderBy(b => b.Title)
            .ToListAsync(cancellationToken);

        return books.Select(b => new BookInfo
        {
            Id = b.Id,
            BookId = b.BookId,
            Title = b.Title,
            Author = b.Author,
            Language = b.Language,
            Edition = b.Edition,
            FilePath = b.FilePath,
            TotalChunks = b.TotalChunks,
            Status = b.Status,
            IndexedAt = b.IndexedAt
        }).ToList();
    }
}
