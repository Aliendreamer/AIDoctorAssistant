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
    private readonly IConfiguration _configuration;

    public TriggerIndexEndpoint(IServiceScopeFactory scopeFactory, ILogger<TriggerIndexEndpoint> logger, MedAssistDbContext medAssistDbContext, IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _medAssistDbContext = medAssistDbContext;
        _configuration = configuration;
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

        if (book is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var mdBasePath = _configuration["Books:MdPath"] ?? "/books/mdfiles";
        var cachedMarkdownPath = Path.Combine(mdBasePath, $"{book.BookId}.md");
        var hasCachedMarkdown = File.Exists(cachedMarkdownPath);

        // Indexing needs either cached markdown (skip Marker) or the source PDF (to OCR).
        // After a DB wipe the PDFs may be gone while the markdown cache survives — that's fine.
        if (!hasCachedMarkdown && !File.Exists(book.FilePath))
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

        var force = Query<string>("force", isRequired: false)?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
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

                var markdownPath = Path.Combine(mdBasePath, $"{bookId}.md");
                string markdown;

                if (File.Exists(markdownPath))
                {
                    _logger.LogInformation("Using cached markdown for {BookId}", bookId);
                    markdown = await File.ReadAllTextAsync(markdownPath);
                }
                else
                {
                    var marker = bgScope.ServiceProvider.GetRequiredService<MarkerClient>();
                    _logger.LogInformation("Submitting Marker job for {BookId} at {Path}", bookId, pdfPath);
                    var jobId = await marker.StartConversionAsync(pdfPath);
                    _logger.LogInformation("Polling Marker job {JobId} for {BookId}", jobId, bookId);
                    markdown = await marker.PollStatusAsync(jobId);
                    await File.WriteAllTextAsync(markdownPath, markdown);
                    _logger.LogInformation("Marker conversion done for {BookId}, markdown cached at {Path}", bookId, markdownPath);
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
