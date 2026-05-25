using FastEndpoints;
using MedAssist.AI.Ingestion;
using MedAssist.Data.Repositories;
using MedAssist.Web.Services;

namespace MedAssist.Web.Endpoints.Books;

public sealed class ExtractBookEndpoint : EndpointWithoutRequest
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ExtractionTracker _tracker;
    private readonly ILogger<ExtractBookEndpoint> _logger;

    public ExtractBookEndpoint(IServiceScopeFactory scopeFactory, ExtractionTracker tracker, ILogger<ExtractBookEndpoint> logger)
    {
        _scopeFactory = scopeFactory;
        _tracker = tracker;
        _logger = logger;
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

        if (!_tracker.TryStart(id, book.BookId, out var existing))
        {
            if (existing.State == ExtractionState.Running)
            {
                await Send.ResponseAsync(new { message = $"Extraction already in progress for '{book.BookId}'." }, 409, ct);
                return;
            }
            _tracker.Reset(id);
            _tracker.TryStart(id, book.BookId, out _);
        }

        var bookId = book.BookId;
        var filePath = book.FilePath;
        var markdownPath = Path.ChangeExtension(filePath, ".md");

        _ = Task.Run(async () =>
        {
            try
            {
                await using var bgScope = _scopeFactory.CreateAsyncScope();
                var marker = bgScope.ServiceProvider.GetRequiredService<MarkerClient>();

                _logger.LogInformation("Submitting Marker job for {BookId}", bookId);
                var jobId = await marker.StartConversionAsync(filePath);

                _logger.LogInformation("Polling Marker job {JobId} for {BookId}", jobId, bookId);
                var markdown = await marker.PollStatusAsync(jobId);

                // Python already saved the file as a safety net; write here too in case paths differ
                await File.WriteAllTextAsync(markdownPath, markdown);
                _tracker.MarkDone(id);
                _logger.LogInformation("Marker extraction done for {BookId}, saved to {Path}", bookId, markdownPath);
            }
            catch (Exception ex)
            {
                _tracker.MarkFailed(id, ex.Message);
                _logger.LogError(ex, "Marker extraction failed for {BookId}", bookId);
            }
        }, CancellationToken.None);

        await Send.ResponseAsync(new { message = $"Extraction started for '{bookId}'. Poll GET /api/admin/books/extract/status?id={id} for progress." }, 202, ct);
    }
}
