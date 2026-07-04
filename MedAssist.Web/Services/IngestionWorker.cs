using MedAssist.AI.Ingestion;
using MedAssist.Data.Repositories;
using MedAssist.Shared.Models;
using MedAssist.Shared.Validation;

namespace MedAssist.Web.Services;

/// <summary>
/// Host-managed consumer of <see cref="IngestionQueue"/> (audit P1-8). Replaces fire-and-forget
/// <c>Task.Run</c> in the index/extract endpoints: work is tracked by the host, runs on its own DI
/// scope, and honors <c>ApplicationStopping</c> via the <c>stoppingToken</c> so a shutdown mid-job
/// stops at a safe point instead of being abandoned. Index jobs checkpoint, so an interrupted index
/// resumes on the next start; extract jobs are re-triggerable.
/// </summary>
public sealed class IngestionWorker(
    IngestionQueue queue,
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ExtractionTracker tracker,
    ILogger<IngestionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // The in-memory job queue does not survive a restart, so any extraction left "running" in the
        // durable tracker was interrupted — reconcile it to Failed so it isn't reported as in-flight
        // forever and can be re-triggered (audit P1-8 follow-up).
        try
        {
            var reconciled = await tracker.MarkInterruptedRunningAsFailedAsync(stoppingToken);
            if (reconciled > 0)
            {
                logger.LogWarning("Reconciled {Count} interrupted extraction(s) to Failed on startup", reconciled);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reconcile interrupted extractions on startup");
        }

        await foreach (var job in queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                switch (job.Kind)
                {
                    case IngestionJobKind.Index:
                        await RunIndexAsync(job, stoppingToken);
                        break;
                    case IngestionJobKind.Extract:
                        await RunExtractAsync(job, stoppingToken);
                        break;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                logger.LogInformation("Ingestion interrupted by shutdown before finishing {Kind} for {BookId}", job.Kind, job.BookSlug);
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Ingestion job {Kind} failed for {BookId}", job.Kind, job.BookSlug);
            }
        }
    }

    private async Task RunIndexAsync(IngestionJob job, CancellationToken ct)
    {
        var mdBasePath = configuration["Books:MdPath"] ?? "/books/mdfiles";
        var markdownPath = BookIdRules.ResolveWithin(mdBasePath, job.BookSlug, ".md");

        await using var scope = scopeFactory.CreateAsyncScope();
        try
        {
            var indexer = scope.ServiceProvider.GetRequiredService<BookIndexer>();

            string markdown;
            if (File.Exists(markdownPath))
            {
                logger.LogInformation("Using cached markdown for {BookId}", job.BookSlug);
                markdown = await File.ReadAllTextAsync(markdownPath, ct);
            }
            else
            {
                var marker = scope.ServiceProvider.GetRequiredService<MarkerClient>();
                logger.LogInformation("Submitting Marker job for {BookId} at {Path}", job.BookSlug, job.FilePath);
                var jobId = await marker.StartConversionAsync(job.FilePath, ct);
                markdown = await marker.PollStatusAsync(jobId, ct);
                await File.WriteAllTextAsync(markdownPath, markdown, ct);
            }

            logger.LogInformation("Starting indexing for {BookId}", job.BookSlug);
            await indexer.IndexAsync(markdown, job.BookSlug, job.Title, job.Author, job.Language, job.Edition, ct);
            logger.LogInformation("Background indexing complete for {BookId}", job.BookSlug);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Shutdown mid-index: leave the book InProgress with its checkpoint so it resumes on the
            // next start. Rethrow so the worker loop exits cleanly.
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Background indexing failed for {BookId}", job.BookSlug);
            await MarkBookFailedAsync(scope, job);
        }
    }

    private async Task RunExtractAsync(IngestionJob job, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        try
        {
            var marker = scope.ServiceProvider.GetRequiredService<MarkerClient>();
            logger.LogInformation("Submitting Marker job for {BookId}", job.BookSlug);
            var jobId = await marker.StartConversionAsync(job.FilePath, ct);
            var savePath = await marker.PollStatusAsync(jobId, ct);

            if (!File.Exists(savePath))
            {
                throw new InvalidOperationException($"Marker reported done but file not found: {savePath}");
            }

            // Persist the outcome even if shutdown was requested — CancellationToken.None.
            await tracker.MarkDoneAsync(job.BookId, CancellationToken.None);
            logger.LogInformation("Marker extraction done for {BookId}, saved to {Path}", job.BookSlug, savePath);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            await tracker.MarkFailedAsync(job.BookId, ex.Message, CancellationToken.None);
            logger.LogError(ex, "Marker extraction failed for {BookId}", job.BookSlug);
        }
    }

    private static async Task MarkBookFailedAsync(AsyncServiceScope scope, IngestionJob job)
    {
        var bookRepo = scope.ServiceProvider.GetRequiredService<BookRepository>();
        await bookRepo.UpsertAsync(new BookInfo
        {
            BookId = job.BookSlug,
            Title = job.Title,
            Author = job.Author,
            Language = job.Language,
            Edition = job.Edition,
            Status = BookStatus.Failed
        }, CancellationToken.None);
    }
}
