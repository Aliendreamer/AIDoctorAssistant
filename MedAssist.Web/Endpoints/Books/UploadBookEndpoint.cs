using FastEndpoints;
using MedAssist.Data;
using MedAssist.Data.Entities;
using MedAssist.Shared.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MedAssist.Web.Endpoints.Books;

public sealed class UploadBookRequest
{
    public string BookId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public string Edition { get; set; } = string.Empty;
    public IFormFile File { get; set; } = null!;
}

public sealed class UploadBookResponse
{
    public int Id { get; init; }
    public string BookId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public sealed class UploadBookEndpoint(
    IDbContextFactory<MedAssistDbContext> dbFactory,
    IConfiguration configuration) : Endpoint<UploadBookRequest, UploadBookResponse>
{
    public override void Configure()
    {
        Post("/api/admin/books/upload");
        Roles("Admin");
        AllowFileUploads();
        Options(x => x.WithMetadata(new RequestSizeLimitAttribute(250 * 1024 * 1024)));
    }

    public override async Task HandleAsync(UploadBookRequest req, CancellationToken ct)
    {
        var rawBooksPath = configuration["Books:RawPath"]
            ?? throw new InvalidOperationException("Books:RawPath is not configured.");
        Directory.CreateDirectory(rawBooksPath);

        var safeFileName = Path.GetFileName(req.File!.FileName);
        var destPath = Path.Combine(rawBooksPath, safeFileName);

        await using (var fs = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        await using (var input = req.File.OpenReadStream())
        {
            await input.CopyToAsync(fs);
        }

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var existing = await db.Books.FirstOrDefaultAsync(b => b.BookId == req.BookId, ct);
        int id;
        if (existing is not null)
        {
            existing.Title = req.Title;
            existing.Author = req.Author;
            existing.Language = req.Language;
            existing.Edition = req.Edition;
            existing.FilePath = destPath;
            existing.Status = IngestionStatus.Pending;
            existing.TotalChunks = 0;
            existing.IndexedAt = null;
            await db.SaveChangesAsync(ct);
            id = existing.Id;
        }
        else
        {
            var entity = new BookEntity
            {
                BookId = req.BookId,
                Title = req.Title,
                Author = req.Author,
                Language = req.Language,
                Edition = req.Edition,
                FilePath = destPath,
                Status = IngestionStatus.Pending,
            };
            db.Books.Add(entity);
            await db.SaveChangesAsync(ct);
            id = entity.Id;
        }

        await HttpContext.Response.SendAsync(new UploadBookResponse
        {
            Id = id,
            BookId = req.BookId,
            Status = IngestionStatus.Pending,
            Message = $"Book '{req.Title}' uploaded. POST /api/admin/index?id={id} to index."
        }, cancellation: ct);
    }
}
