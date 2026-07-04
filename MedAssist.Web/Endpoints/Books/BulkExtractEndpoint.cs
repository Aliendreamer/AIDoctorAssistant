using FastEndpoints;
using MedAssist.Data.Repositories;
using MedAssist.Shared.Models;
using MedAssist.Shared.Validation;
using MedAssist.Web.Services;

namespace MedAssist.Web.Endpoints.Books;

public sealed class BulkExtractEndpoint : EndpointWithoutRequest
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ExtractionTracker _tracker;
    private readonly IConfiguration _configuration;
    private readonly IngestionQueue _queue;

    public BulkExtractEndpoint(IServiceScopeFactory scopeFactory, ExtractionTracker tracker, IConfiguration configuration, IngestionQueue queue)
    {
        _scopeFactory = scopeFactory;
        _tracker = tracker;
        _configuration = configuration;
        _queue = queue;
    }

    public override void Configure()
    {
        Post("/api/admin/books/extract/all");
        Roles("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var bookRepo = scope.ServiceProvider.GetRequiredService<BookRepository>();
        var books = await bookRepo.GetAllAsync(ct);

        var runningIds = (await _tracker.GetAllAsync(ct))
            .Where(e => e.State == ExtractionState.Running)
            .Select(e => e.BookDbId)
            .ToHashSet();

        var mdBasePath = _configuration["Books:MdPath"] ?? "/books/mdfiles";
        var eligible = books
            .Where(b => BookIdRules.IsValid(b.BookId))
            .Where(b => !string.IsNullOrEmpty(b.FilePath) && File.Exists(b.FilePath) && !runningIds.Contains(b.Id))
            .Where(b =>
            {
                var mdPath = BookIdRules.ResolveWithin(mdBasePath, b.BookId, ".md");
                return !File.Exists(mdPath) || File.GetLastWriteTimeUtc(mdPath) <= File.GetLastWriteTimeUtc(b.FilePath);
            })
            .ToList();

        if (eligible.Count == 0)
        {
            await Send.ResponseAsync(new { message = "No eligible books found (either no PDF on disk, or all already running)." }, 200, ct);
            return;
        }

        // Mark each eligible book running, then enqueue an extract job for the host-managed worker
        // to process serially — no detached Task.Run (audit P1-8).
        foreach (var book in eligible)
        {
            await _tracker.TryStartAsync(book.Id, book.BookId, ct);

            await _queue.EnqueueAsync(new IngestionJob(
                IngestionJobKind.Extract, book.Id, book.BookId, book.Title, book.Author, book.Language, book.Edition, book.FilePath), ct);
        }

        await Send.ResponseAsync(
            new { message = $"Bulk extraction started for {eligible.Count} book(s). Poll GET /api/admin/books/extract/status for progress." },
            202, ct);
    }
}
