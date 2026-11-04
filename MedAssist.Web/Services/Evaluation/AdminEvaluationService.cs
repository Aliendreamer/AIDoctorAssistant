using MedAssist.AI.Evaluation;
using MedAssist.Data.Entities;
using MedAssist.Data.Repositories;
using MedAssist.Shared.Models.Evaluation;

namespace MedAssist.Web.Services.Evaluation;

public sealed record EvalRunView(
    Guid Id,
    string Note,
    EvalRunStatus Status,
    string Phase,
    int Done,
    int Total,
    string StartedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    bool IsBaseline,
    string? Error,
    EvalRunSnapshot Snapshot,
    EvalRunSummary? Summary,
    EvalVerdict Verdict);

public sealed record EvalRunDetail(
    EvalRunView Run,
    IReadOnlyList<EvalCaseResult> Cases,
    EvalComparison? Comparison,
    Guid? BaselineId,
    IReadOnlyList<EvalRunView> BaselineChoices);

public sealed record EvalFormDefaults(string DefaultModel, IReadOnlyList<string> Models, int TotalCases, int ReviewedCases);

/// <summary>
/// Blazor-facing evaluation operations. Each call opens its own DI scope: a circuit's scope lives as
/// long as the page, and its polling timer must not share a <c>DbContext</c> with a button click.
/// </summary>
public sealed class AdminEvaluationService(IServiceScopeFactory scopeFactory, EvaluationCoordinator coordinator)
{
    private const int _listedRuns = 50;

    public async Task<EvalFormDefaults> GetFormDefaultsAsync(CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var models = await provider.GetRequiredService<OllamaModelCatalog>().ListAsync(ct);
        var golden = provider.GetRequiredService<GoldenSet>();
        return new EvalFormDefaults(
            provider.GetRequiredService<EvaluationSnapshotFactory>().DefaultModel, models, golden.Cases.Count, golden.ReviewedCount);
    }

    public async Task<(bool Success, string Message, Guid? RunId)> StartAsync(
        EvalRunRequest request, string startedBy, CancellationToken ct = default)
    {
        var runId = Guid.NewGuid();
        if (!coordinator.TryReserve(runId))
        {
            return (false, "An evaluation is already running.", null);
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var provider = scope.ServiceProvider;
            var snapshot = await provider.GetRequiredService<EvaluationSnapshotFactory>().CreateAsync(request, ct);

            var missing = await FindMissingModelAsync(provider, snapshot, ct);
            if (missing is not null)
            {
                coordinator.Release(runId);
                return (false, $"Ollama has no model named '{missing}'.", null);
            }

            await provider.GetRequiredService<EvaluationRepository>().AddRunAsync(new EvaluationRunEntity
            {
                Id = runId,
                Note = request.Note.Trim(),
                Status = EvalRunStatus.Queued,
                StartedBy = startedBy,
                CreatedAt = DateTimeOffset.UtcNow,
                Snapshot = EvalJson.Serialize(snapshot),
            }, ct);
            await coordinator.EnqueueAsync(runId, ct);
            return (true, "Evaluation started.", runId);
        }
        catch
        {
            coordinator.Release(runId);
            throw;
        }
    }

    public bool Cancel(Guid runId) => coordinator.Cancel(runId);

    /// <summary>Whether this run is really executing, whatever its stored status says.</summary>
    public bool IsExecuting(Guid runId) => coordinator.ActiveRunId == runId;

    public async Task<IReadOnlyList<EvalRunView>> ListAsync(CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var runs = await scope.ServiceProvider.GetRequiredService<EvaluationRepository>().ListRunsAsync(_listedRuns, ct);
        return ToViews(runs);
    }

    public async Task<EvalRunDetail?> GetDetailAsync(Guid runId, Guid? baselineId, CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<EvaluationRepository>();

        var views = ToViews(await repository.ListRunsAsync(_listedRuns, ct));
        var run = views.FirstOrDefault(v => v.Id == runId);
        if (run is null)
        {
            var entity = await repository.GetRunAsync(runId, ct);
            if (entity is null)
            {
                return null;
            }

            run = ToView(entity, EvalVerdict.NoBaseline);
        }

        var cases = await LoadCasesAsync(repository, runId, ct);
        var choices = views.Where(v => v.Id != runId && v.Summary is not null).ToList();
        var baseline = baselineId is { } chosen ? choices.FirstOrDefault(c => c.Id == chosen) : PickBaseline(run, views);
        if (run.Summary is null)
        {
            return new EvalRunDetail(run, cases, null, baseline?.Id, choices);
        }

        var baselineCases = baseline is null ? [] : await LoadCasesAsync(repository, baseline.Id, ct);
        var comparison = RunComparer.Compare(
            run.Summary, run.Snapshot, cases, baseline?.Summary, baseline?.Snapshot, baselineCases);
        return new EvalRunDetail(run with { Verdict = comparison.Verdict }, cases, comparison, baseline?.Id, choices);
    }

    public async Task SetBaselineAsync(Guid runId, bool isBaseline, CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<EvaluationRepository>().SetBaselineAsync(runId, isBaseline, ct);
    }

    public async Task DeleteAsync(Guid runId, CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<EvaluationRepository>().DeleteRunAsync(runId, ct);
    }

    /// <summary>The pinned baseline, or else the latest completed run before this one.</summary>
    internal static EvalRunView? PickBaseline(EvalRunView run, IReadOnlyList<EvalRunView> runs)
    {
        var candidates = runs.Where(r => r.Id != run.Id && r.Summary is not null).ToList();
        return candidates.FirstOrDefault(r => r.IsBaseline)
            ?? candidates.Where(r => r.CreatedAt < run.CreatedAt).MaxBy(r => r.CreatedAt);
    }

    private static async Task<string?> FindMissingModelAsync(IServiceProvider provider, EvalRunSnapshot snapshot, CancellationToken ct)
    {
        var installed = await provider.GetRequiredService<OllamaModelCatalog>().ListAsync(ct);
        if (installed.Count == 0)
        {
            // Ollama did not answer; the run itself will report that properly per question.
            return null;
        }

        return new[] { snapshot.Model, snapshot.JudgeModel }
            .FirstOrDefault(m => m is not null && !installed.Contains(m) && !installed.Contains($"{m}:latest"));
    }

    private static async Task<IReadOnlyList<EvalCaseResult>> LoadCasesAsync(EvaluationRepository repository, Guid runId, CancellationToken ct)
    {
        var rows = await repository.GetResultsAsync(runId, ct);
        return rows.Select(r => EvalJson.Deserialize<EvalCaseResult>(r.Details)).ToList();
    }

    private static List<EvalRunView> ToViews(IReadOnlyList<EvaluationRunEntity> entities)
    {
        var views = entities.Select(e => ToView(e, EvalVerdict.NoBaseline)).ToList();
        return views.Select(v => v with { Verdict = VerdictOf(v, views) }).ToList();
    }

    private static EvalVerdict VerdictOf(EvalRunView run, IReadOnlyList<EvalRunView> runs)
    {
        if (run.Summary is null)
        {
            return EvalVerdict.NoBaseline;
        }

        var baseline = PickBaseline(run, runs);
        return RunComparer.Compare(run.Summary, run.Snapshot, [], baseline?.Summary, baseline?.Snapshot, []).Verdict;
    }

    private static EvalRunView ToView(EvaluationRunEntity e, EvalVerdict verdict) => new(
        e.Id, e.Note, e.Status, e.Phase, e.Done, e.Total, e.StartedBy, e.CreatedAt, e.CompletedAt, e.IsBaseline, e.Error,
        EvalJson.Deserialize<EvalRunSnapshot>(e.Snapshot),
        e.Summary is null ? null : EvalJson.Deserialize<EvalRunSummary>(e.Summary),
        verdict);
}
