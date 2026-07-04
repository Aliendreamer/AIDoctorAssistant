using FastEndpoints;
using MedAssist.Shared.Models;
using MedAssist.Web.Services;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.Mvc;

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
public sealed class UploadBookEndpoint(BookApplicationService books) : Endpoint<UploadBookRequest, UploadBookResponse>
{
    private readonly BookApplicationService _books = books;

    public override void Configure()
    {
        Post("/api/admin/books/upload");
        Roles("Admin");
        AllowFileUploads();
        Options(x => x.WithMetadata(new RequestSizeLimitAttribute(764 * 1024 * 1024)));
    }

    public override async Task HandleAsync(UploadBookRequest req, CancellationToken ct)
    {
        await using var content = req.File.OpenReadStream();
        var result = await _books.UploadAsync(
            new UploadBookInput(req.BookId, req.Title, req.Author, req.Language, req.Edition, content, req.File.FileName), ct);

        if (!result.Success)
        {
            await Send.ResponseAsync(new UploadBookResponse { Message = result.Message }, 400, ct);
            return;
        }

        await Send.OkAsync(new UploadBookResponse
        {
            Id = result.Id,
            BookId = req.BookId,
            Status = BookStatus.Pending.ToString().ToLowerInvariant(),
            Message = $"Book '{req.Title}' uploaded. POST /api/admin/index?id={result.Id} to index."
        }, cancellation: ct);
    }
}
