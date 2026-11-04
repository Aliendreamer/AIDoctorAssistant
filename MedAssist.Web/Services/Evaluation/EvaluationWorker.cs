using MedAssist.Data.Repositories;
using MedAssist.Shared.Constants;
using MedAssist.Shared.Models.Evaluation;

namespace MedAssist.Web.Services.Evaluation;

/// <summary>Host-managed consumer of <see cref="EvaluationCoordinator"/>, so a run outlives the admin's page.</summary>
public sealed class EvaluationWorker(
    EvaluationCoordinator coordinator,
    IServiceScopeFactory scopeFactory,
    ILogger<EvaluationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ReconcileInterruptedRunsAsync(stoppingToken);

        await foreach (var runId in coordinator.DequeueAllAsync(stoppingToken))
        {
            try
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, coordinator.TokenFor(runId));
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<EvaluationRunner>().RunAsync(runId, linked.Token);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Evaluation run {RunId} ended unexpectedly", runId);
                await TryMarkFailedAsync(runId, ex.Message);
            }
            finally
            {
                coordinator.Release(runId);
            }
        }
    }

    // The runner could not record its own end, so a run would otherwise look active until a restart.
    private async Task TryMarkFailedAsync(Guid runId, string error)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<EvaluationRepository>()
                .FinishAsync(runId, EvalRunStatus.Failed, null, error, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not mark evaluation run {RunId} as failed", runId);
        }
    }

    // The queue is in memory, so a run still marked active after a restart will never finish.
    private async Task ReconcileInterruptedRunsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var reconciled = await scope.ServiceProvider.GetRequiredService<EvaluationRepository>()
                .FailInterruptedAsync("Interrupted by an application restart.", cancellationToken);
            await scope.ServiceProvider.GetRequiredService<ChatHistoryRepository>()
                .ClearByUserPrefixAsync(EvaluationConstants.EvalUserPrefix, cancellationToken);
            if (reconciled > 0)
            {
                logger.LogWarning("Reconciled {Count} interrupted evaluation run(s) to Failed on startup", reconciled);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to reconcile interrupted evaluation runs on startup");
        }
    }
}
