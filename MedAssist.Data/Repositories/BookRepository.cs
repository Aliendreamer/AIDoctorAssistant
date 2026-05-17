using MedAssist.Data.Entities;
using MedAssist.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace MedAssist.Data.Repositories;

public sealed class BookRepository
{
    private readonly IDbContextFactory<MedAssistDbContext> _dbFactory;

    public BookRepository(IDbContextFactory<MedAssistDbContext> dbFactory) => _dbFactory = dbFactory;

    public async Task UpsertAsync(BookInfo book, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var existing = await db.Books.FirstOrDefaultAsync(b => b.BookId == book.BookId, cancellationToken);
        if (existing is not null)
        {
            existing.Title = book.Title;
            existing.Author = book.Author;
            existing.Language = book.Language;
            existing.Edition = book.Edition;
            if (!string.IsNullOrEmpty(book.FilePath))
            {
                existing.FilePath = book.FilePath;
            }
            existing.TotalChunks = book.TotalChunks;
            existing.Status = book.Status.ToString().ToLowerInvariant();
            existing.IndexedAt = book.IndexedAt;
        }
        else
        {
            db.Books.Add(new BookEntity
            {
                BookId = book.BookId,
                Title = book.Title,
                Author = book.Author,
                Language = book.Language,
                Edition = book.Edition,
                FilePath = book.FilePath,
                TotalChunks = book.TotalChunks,
                Status = book.Status.ToString().ToLowerInvariant(),
                IndexedAt = book.IndexedAt
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<BookInfo?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Books.FindAsync([id], cancellationToken);
        return entity is null ? null : MapToInfo(entity);
    }

    public async Task<BookInfo?> GetByBookIdAsync(string bookId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.Books.FirstOrDefaultAsync(b => b.BookId == bookId, cancellationToken);
        return entity is null ? null : MapToInfo(entity);
    }

    private static BookInfo MapToInfo(BookEntity b) => new()
    {
        Id = b.Id,
        BookId = b.BookId,
        Title = b.Title,
        Author = b.Author,
        Language = b.Language,
        Edition = b.Edition,
        FilePath = b.FilePath,
        TotalChunks = b.TotalChunks,
        Status = Enum.Parse<BookStatus>(b.Status, ignoreCase: true),
        IndexedAt = b.IndexedAt
    };
}
