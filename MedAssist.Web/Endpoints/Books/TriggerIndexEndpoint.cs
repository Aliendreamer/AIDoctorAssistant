using FastEndpoints;
using MedAssist.AI.Ingestion;
using MedAssist.Data;
using MedAssist.Data.Repositories;
using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace MedAssist.Web.Endpoints.Books;

public sealed class TriggerIndexEndpoint : EndpointWithoutRequest
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TriggerIndexEndpoint> _logger;
    private readonly MedAssistDbContext _medAssistDbContext;

    public TriggerIndexEndpoint(IServiceScopeFactory scopeFactory, ILogger<TriggerIndexEndpoint> logger, MedAssistDbContext medAssistDbContext)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _medAssistDbContext = medAssistDbContext;
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

        if (book.Status == BookStatus.InProgress)
        {
            await Send.ResponseAsync(new { message = $"Book '{book.BookId}' is already being indexed." }, 409, ct);
            return;
        }

        var checkpointRepo = scope.ServiceProvider.GetRequiredService<CheckpointRepository>();

        var force = Query<string>("force")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
        if (force)
        {
            var vectorStore = scope.ServiceProvider.GetRequiredService<IVectorStore>();
            await vectorStore.DeleteCollectionAsync(ct);
            await _medAssistDbContext.Bm25Vocab.ExecuteDeleteAsync(ct);
            await _medAssistDbContext.Bm25Stats.ExecuteDeleteAsync(ct);
            _logger.LogInformation("Force re-index: cleared Qdrant and BM25 vocab for {BookId}", book.BookId);
        }

        // Always clear the checkpoint so a previous complete/partial run doesn't block re-indexing.
        await checkpointRepo.DeleteAsync(book.BookId, ct);

        var pdfPath = book.FilePath;
        var bookId = book.BookId;

        _ = Task.Run(async () =>
        {
            try
            {
                await using var bgScope = _scopeFactory.CreateAsyncScope();
                var indexer = bgScope.ServiceProvider.GetRequiredService<BookIndexer>();

                var markdownPath = Path.ChangeExtension(pdfPath, ".md");
                string markdown;

                if (File.Exists(markdownPath))
                {
                    _logger.LogInformation("Using cached Docling markdown for {BookId}", bookId);
                    markdown = await File.ReadAllTextAsync(markdownPath);
                }
                else
                {
                    var docling = bgScope.ServiceProvider.GetRequiredService<DoclingClient>();
                    _logger.LogInformation("Starting Docling conversion for {BookId}", bookId);
                    await using var pdfStream = File.OpenRead(pdfPath);
                    markdown = await docling.ConvertPdfToMarkdownAsync(pdfStream, $"{bookId}.pdf");
                    await File.WriteAllTextAsync(markdownPath, markdown);
                    _logger.LogInformation("Docling conversion done for {BookId}, markdown cached at {Path}", bookId, markdownPath);
                }

                _logger.LogInformation("Starting indexing for {BookId}", bookId);
                await indexer.IndexAsync(markdown, bookId, book.Title, book.Author, book.Language, book.Edition);

                _logger.LogInformation("Background indexing complete for {BookId}", bookId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background indexing failed for {BookId}", bookId);
                try
                {
                    await using var failScope = _scopeFactory.CreateAsyncScope();
                    var failRepo = failScope.ServiceProvider.GetRequiredService<BookRepository>();
                    await failRepo.UpsertAsync(new BookInfo
                    {
                        BookId = bookId,
                        Title = book.Title,
                        Author = book.Author,
                        Language = book.Language,
                        Edition = book.Edition,
                        Status = BookStatus.Failed
                    }, CancellationToken.None);
                }
                catch (Exception innerEx)
                {
                    _logger.LogError(innerEx, "Failed to update book status to Failed for {BookId}", bookId);
                }
            }
        }, CancellationToken.None);

        await Send.ResponseAsync(new { message = $"Indexing started for '{book.BookId}'. Check book status for progress." }, 202, ct);
    }
}
