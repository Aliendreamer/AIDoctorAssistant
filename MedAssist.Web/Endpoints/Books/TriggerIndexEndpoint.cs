using FastEndpoints;
using MedAssist.AI.Ingestion;
using MedAssist.Data.Repositories;

namespace MedAssist.Web.Endpoints.Books;

public sealed class TriggerIndexEndpoint : EndpointWithoutRequest
{
    private readonly IServiceScopeFactory _scopeFactory;

    public TriggerIndexEndpoint(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

    public override void Configure()
    {
        Post("/api/admin/index");
        Roles("Admin");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var numericId = Query<int>("id", isRequired: true);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var bookRepo = scope.ServiceProvider.GetRequiredService<BookRepository>();
        var book = await bookRepo.GetByIdAsync(numericId, ct);

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
            await using var bgScope = _scopeFactory.CreateAsyncScope();
            var logger = bgScope.ServiceProvider.GetRequiredService<ILogger<TriggerIndexEndpoint>>();
            try
            {
                var docling = bgScope.ServiceProvider.GetRequiredService<DoclingClient>();
                var indexer = bgScope.ServiceProvider.GetRequiredService<BookIndexer>();

                await using var pdfStream = File.OpenRead(pdfPath);
                var markdown = await docling.ConvertPdfToMarkdownAsync(pdfStream, $"{bookId}.pdf");
                await indexer.IndexAsync(markdown, bookId, book.Title, book.Author, book.Language, book.Edition);

                logger.LogInformation("Background indexing complete for {BookId}", bookId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Background indexing failed for {BookId}", bookId);
            }
        }, CancellationToken.None);

        await HttpContext.Response.SendAsync(
            new { message = $"Indexing started for '{book.BookId}'. Check book status for progress." },
            statusCode: 202,
            cancellation: ct);
    }
}
