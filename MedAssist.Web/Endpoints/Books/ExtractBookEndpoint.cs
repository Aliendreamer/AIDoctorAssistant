using FastEndpoints;
using MedAssist.Data.Repositories;
using MedAssist.Web.Services;

namespace MedAssist.Web.Endpoints.Books;

public sealed class ExtractBookEndpoint : EndpointWithoutRequest
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ExtractionTracker _tracker;
    private readonly IngestionQueue _queue;

    public ExtractBookEndpoint(IServiceScopeFactory scopeFactory, ExtractionTracker tracker, IngestionQueue queue)
    {
        _scopeFactory = scopeFactory;
        _tracker = tracker;
        _queue = queue;
    }

    public override void Configure()
    {
        Post("/api/admin/books/extract");
        Roles("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!int.TryParse(Query<string>("id"), out var id) || id <= 0)
        {
            await Send.ResponseAsync(new { message = "Query parameter 'id' must be a positive integer." }, 400, ct);
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var bookRepo = scope.ServiceProvider.GetRequiredService<BookRepository>();
        var book = await bookRepo.GetByIdAsync(id, ct);

        if (book is null || !File.Exists(book.FilePath))
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var outcome = await _tracker.TryStartAsync(id, book.BookId, ct);
        if (!outcome.Started)
        {
            await Send.ResponseAsync(new { message = $"Extraction already in progress for '{book.BookId}'." }, 409, ct);
            return;
        }

        // Hand off to the host-managed ingestion worker instead of a detached Task.Run (audit P1-8).
        await _queue.EnqueueAsync(new IngestionJob(
            IngestionJobKind.Extract, id, book.BookId, book.Title, book.Author, book.Language, book.Edition, book.FilePath), ct);

        await Send.ResponseAsync(new { message = $"Extraction started for '{book.BookId}'. Poll GET /api/admin/books/extract/status?id={id} for progress." }, 202, ct);
    }
}
