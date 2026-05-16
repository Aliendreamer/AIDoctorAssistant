using FastEndpoints;
using MedAssist.AI.Ingestion;
using MedAssist.Data.Repositories;

namespace MedAssist.Web.Endpoints.Books;

public sealed class TriggerIndexEndpoint : EndpointWithoutRequest
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TriggerIndexEndpoint> _logger;

    public TriggerIndexEndpoint(IServiceScopeFactory scopeFactory, ILogger<TriggerIndexEndpoint> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/api/admin/index");
        Roles("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!int.TryParse(Query<string>("id"), out var id) || id <= 0)
        {
            await HttpContext.Response.SendAsync(
                new { message = "Query parameter 'id' must be a positive integer." },
                statusCode: 400,
                cancellation: ct);
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var bookRepo = scope.ServiceProvider.GetRequiredService<BookRepository>();
        var book = await bookRepo.GetByIdAsync(id, ct);

        if (book is null || !File.Exists(book.FilePath))
        {
            await HttpContext.Response.SendNotFoundAsync(ct);
            return;
        }

        if (book.Status == Shared.Models.BookStatus.InProgress)
        {
            await HttpContext.Response.SendAsync(
                new { message = $"Book '{book.BookId}' is already being indexed." },
                statusCode: 409,
                cancellation: ct);
            return;
        }

        var pdfPath = book.FilePath;
        var bookId = book.BookId;

        _ = Task.Run(async () =>
        {
            try
            {
                await using var bgScope = _scopeFactory.CreateAsyncScope();
                var docling = bgScope.ServiceProvider.GetRequiredService<DoclingClient>();
                var indexer = bgScope.ServiceProvider.GetRequiredService<BookIndexer>();

                _logger.LogInformation("Starting Docling conversion for {BookId}", bookId);
                await using var pdfStream = File.OpenRead(pdfPath);
                var markdown = await docling.ConvertPdfToMarkdownAsync(pdfStream, $"{bookId}.pdf");

                _logger.LogInformation("Docling conversion done for {BookId}, starting indexing", bookId);
                await indexer.IndexAsync(markdown, bookId, book.Title, book.Author, book.Language, book.Edition);

                _logger.LogInformation("Background indexing complete for {BookId}", bookId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background indexing failed for {BookId}", bookId);
            }
        }, CancellationToken.None);

        await HttpContext.Response.SendAsync(
            new { message = $"Indexing started for '{book.BookId}'. Check book status for progress." },
            statusCode: 202,
            cancellation: ct);
    }
}
