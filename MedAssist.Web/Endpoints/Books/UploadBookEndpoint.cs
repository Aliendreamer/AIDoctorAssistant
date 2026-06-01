using FastEndpoints;
using MedAssist.Data;
using MedAssist.Data.Entities;
using MedAssist.Shared.Models;
using Microsoft.AspNetCore.Http.Timeouts;
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

[RequestTimeout("upload")]
public sealed class UploadBookEndpoint(
    MedAssistDbContext medAssistDbContext,
    IConfiguration configuration) : Endpoint<UploadBookRequest, UploadBookResponse>
{
    private readonly MedAssistDbContext _medAssistDbContext = medAssistDbContext;
    public override void Configure()
    {
        Post("/api/admin/books/upload");
        Roles("Admin");
        AllowFileUploads();
        Options(x => x.WithMetadata(new RequestSizeLimitAttribute(764 * 1024 * 1024)));
    }

    public override async Task HandleAsync(UploadBookRequest req, CancellationToken ct)
    {
        var pdfPath = configuration["Books:PdfPath"]
            ?? throw new InvalidOperationException("Books:PdfPath is not configured.");
        Directory.CreateDirectory(pdfPath);

        var destPath = Path.Combine(pdfPath, $"{req.BookId}.pdf");

        await using (var fs = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        await using (var input = req.File.OpenReadStream())
        {
            await input.CopyToAsync(fs);
        }

        var existing = await _medAssistDbContext.Books.FirstOrDefaultAsync(b => b.BookId == req.BookId, ct);
        int id;
        if (existing is not null)
        {
            existing.Title = req.Title;
            existing.Author = req.Author;
            existing.Language = req.Language;
            existing.Edition = req.Edition;
            existing.FilePath = destPath;
            existing.Status = BookStatus.Pending;
            existing.TotalChunks = 0;
            existing.IndexedAt = null;
            await _medAssistDbContext.SaveChangesAsync(ct);
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
                Status = BookStatus.Pending,
            };
            _medAssistDbContext.Books.Add(entity);
            await _medAssistDbContext.SaveChangesAsync(ct);
            id = entity.Id;
        }

        await Send.OkAsync(new UploadBookResponse
        {
            Id = id,
            BookId = req.BookId,
            Status = BookStatus.Pending.ToString().ToLowerInvariant(),
            Message = $"Book '{req.Title}' uploaded. POST /api/admin/index?id={id} to index."
        }, cancellation: ct);
    }
}
