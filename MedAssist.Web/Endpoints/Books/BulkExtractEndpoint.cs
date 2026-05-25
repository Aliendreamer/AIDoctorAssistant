using FastEndpoints;
using MedAssist.AI.Ingestion;
using MedAssist.Data.Repositories;
using MedAssist.Web.Services;

namespace MedAssist.Web.Endpoints.Books;

public sealed class BulkExtractEndpoint : EndpointWithoutRequest
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ExtractionTracker _tracker;
    private readonly ILogger<BulkExtractEndpoint> _logger;

    public BulkExtractEndpoint(IServiceScopeFactory scopeFactory, ExtractionTracker tracker, ILogger<BulkExtractEndpoint> logger)
    {
        _scopeFactory = scopeFactory;
        _tracker = tracker;
        _logger = logger;
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

        var eligible = books
            .Where(b => !string.IsNullOrEmpty(b.FilePath) && File.Exists(b.FilePath) && !_tracker.IsRunning(b.Id))
            .ToList();

        if (eligible.Count == 0)
        {
            await Send.ResponseAsync(new { message = "No eligible books found (either no PDF on disk, or all already running)." }, 200, ct);
            return;
        }

        foreach (var book in eligible)
        {
            if (!_tracker.TryStart(book.Id, book.BookId, out _))
            {
                _tracker.Reset(book.Id);
                _tracker.TryStart(book.Id, book.BookId, out _);
            }
        }

        _ = Task.Run(async () =>
        {
            foreach (var book in eligible)
            {
                try
                {
                    await using var bgScope = _scopeFactory.CreateAsyncScope();
                    var marker = bgScope.ServiceProvider.GetRequiredService<MarkerClient>();

                    var markdownPath = Path.ChangeExtension(book.FilePath, ".md");
                    _logger.LogInformation("Bulk extract [{Done}/{Total}]: submitting Marker job for {BookId}",
                        eligible.IndexOf(book) + 1, eligible.Count, book.BookId);

                    var jobId = await marker.StartConversionAsync(book.FilePath);
                    _logger.LogInformation("Bulk extract: polling job {JobId} for {BookId}", jobId, book.BookId);

                    var markdown = await marker.PollStatusAsync(jobId);

                    await File.WriteAllTextAsync(markdownPath, markdown);
                    _tracker.MarkDone(book.Id);
                    _logger.LogInformation("Bulk extract: done {BookId} → {Path}", book.BookId, markdownPath);
                }
                catch (Exception ex)
                {
                    _tracker.MarkFailed(book.Id, ex.Message);
                    _logger.LogError(ex, "Bulk extract: failed for {BookId}", book.BookId);
                }
            }
        }, CancellationToken.None);

        await Send.ResponseAsync(
            new { message = $"Bulk extraction started for {eligible.Count} book(s). Poll GET /api/admin/books/extract/status for progress." },
            202, ct);
    }
}
