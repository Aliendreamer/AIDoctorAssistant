using MedAssist.Data;
using MedAssist.Data.Entities;
using MedAssist.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace MedAssist.Indexer.Repositories;

public sealed class BookRepository
{
    private readonly MedAssistDbContext _db;

    public BookRepository(MedAssistDbContext db) => _db = db;

    public async Task UpsertAsync(BookInfo book, CancellationToken cancellationToken = default)
    {
        var existing = await _db.Books.FindAsync([book.Id], cancellationToken);
        if (existing is not null)
        {
            existing.Title = book.Title;
            existing.Author = book.Author;
            existing.Language = book.Language;
            existing.Edition = book.Edition;
            existing.TotalChunks = book.TotalChunks;
            existing.Status = book.Status.ToString().ToLowerInvariant();
            existing.IndexedAt = book.IndexedAt;
        }
        else
        {
            _db.Books.Add(new BookEntity
            {
                Id = book.Id,
                Title = book.Title,
                Author = book.Author,
                Language = book.Language,
                Edition = book.Edition,
                TotalChunks = book.TotalChunks,
                Status = book.Status.ToString().ToLowerInvariant(),
                IndexedAt = book.IndexedAt
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BookInfo>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var books = await _db.Books
            .OrderBy(b => b.Title)
            .ToListAsync(cancellationToken);

        return books.Select(MapToInfo).ToList();
    }

    public async Task<BookInfo?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Books.FindAsync([id], cancellationToken);
        return entity is null ? null : MapToInfo(entity);
    }

    private static BookInfo MapToInfo(BookEntity b) => new()
    {
        Id = b.Id,
        Title = b.Title,
        Author = b.Author,
        Language = b.Language,
        Edition = b.Edition,
        TotalChunks = b.TotalChunks,
        Status = Enum.Parse<BookStatus>(b.Status, ignoreCase: true),
        IndexedAt = b.IndexedAt
    };
}
