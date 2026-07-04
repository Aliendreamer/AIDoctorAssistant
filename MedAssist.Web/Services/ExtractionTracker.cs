using MedAssist.Data.Repositories;
using MedAssist.Shared.Models;

namespace MedAssist.Web.Services;

/// <summary>
/// Durable facade over extraction status (audit P1-8 follow-up). Was an in-memory
/// <c>ConcurrentDictionary</c> whose state vanished on restart; now a singleton that opens a
/// short-lived DI scope per call (so it never captures a scoped <c>DbContext</c>) and delegates to
/// <see cref="ExtractionStatusRepository"/> — mirroring how <c>Bm25VocabCache</c> reaches the DB.
/// </summary>
public sealed class ExtractionTracker(IServiceScopeFactory scopeFactory)
{
    public Task<ExtractionStartOutcome> TryStartAsync(int bookDbId, string bookSlug, CancellationToken cancellationToken = default)
        => WithRepoAsync(repo => repo.TryStartAsync(bookDbId, bookSlug, cancellationToken));

    public Task MarkDoneAsync(int bookDbId, CancellationToken cancellationToken = default)
        => WithRepoAsync(repo => repo.MarkDoneAsync(bookDbId, cancellationToken));

    public Task MarkFailedAsync(int bookDbId, string error, CancellationToken cancellationToken = default)
        => WithRepoAsync(repo => repo.MarkFailedAsync(bookDbId, error, cancellationToken));

    public Task<bool> IsRunningAsync(int bookDbId, CancellationToken cancellationToken = default)
        => WithRepoAsync(repo => repo.IsRunningAsync(bookDbId, cancellationToken));

    public Task<ExtractionEntry?> GetAsync(int bookDbId, CancellationToken cancellationToken = default)
        => WithRepoAsync(repo => repo.GetAsync(bookDbId, cancellationToken));

    public Task<IReadOnlyList<ExtractionEntry>> GetAllAsync(CancellationToken cancellationToken = default)
        => WithRepoAsync(repo => repo.GetAllAsync(cancellationToken));

    public Task<int> MarkInterruptedRunningAsFailedAsync(CancellationToken cancellationToken = default)
        => WithRepoAsync(repo => repo.MarkInterruptedRunningAsFailedAsync(cancellationToken));

    private async Task<T> WithRepoAsync<T>(Func<ExtractionStatusRepository, Task<T>> action)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<ExtractionStatusRepository>());
    }

    private async Task WithRepoAsync(Func<ExtractionStatusRepository, Task> action)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<ExtractionStatusRepository>());
    }
}
