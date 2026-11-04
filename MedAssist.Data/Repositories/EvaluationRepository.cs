using MedAssist.Data.Entities;
using MedAssist.Shared.Models.Evaluation;
using Microsoft.EntityFrameworkCore;

namespace MedAssist.Data.Repositories;

public sealed class EvaluationRepository(MedAssistDbContext db)
{
    public async Task AddRunAsync(EvaluationRunEntity run, CancellationToken ct = default)
    {
        db.EvaluationRuns.Add(run);
        await db.SaveChangesAsync(ct);
        db.Entry(run).State = EntityState.Detached;
    }

    public Task<EvaluationRunEntity?> GetRunAsync(Guid id, CancellationToken ct = default) =>
        db.EvaluationRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<IReadOnlyList<EvaluationRunEntity>> ListRunsAsync(int limit, CancellationToken ct = default) =>
        await db.EvaluationRuns.AsNoTracking().OrderByDescending(r => r.CreatedAt).Take(limit).ToListAsync(ct);

    public async Task<IReadOnlyList<EvaluationResultEntity>> GetResultsAsync(Guid runId, CancellationToken ct = default) =>
        await db.EvaluationResults.AsNoTracking().Where(r => r.RunId == runId).OrderBy(r => r.Ordinal).ToListAsync(ct);

    /// <summary>Inserts or replaces a question's result: answering writes it, judging rewrites it.</summary>
    public async Task SaveResultAsync(Guid runId, string caseId, int ordinal, string details, CancellationToken ct = default)
    {
        var updated = await db.EvaluationResults
            .Where(r => r.RunId == runId && r.CaseId == caseId)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Details, details), ct);
        if (updated > 0)
        {
            return;
        }

        var entity = new EvaluationResultEntity { RunId = runId, CaseId = caseId, Ordinal = ordinal, Details = details };
        db.EvaluationResults.Add(entity);
        await db.SaveChangesAsync(ct);
        db.Entry(entity).State = EntityState.Detached;
    }

    public Task UpdateProgressAsync(Guid runId, EvalRunStatus status, string phase, int done, int total, CancellationToken ct = default) =>
        db.EvaluationRuns.Where(r => r.Id == runId).ExecuteUpdateAsync(s => s
            .SetProperty(r => r.Status, status)
            .SetProperty(r => r.Phase, phase)
            .SetProperty(r => r.Done, done)
            .SetProperty(r => r.Total, total), ct);

    public Task FinishAsync(Guid runId, EvalRunStatus status, string? summary, string? error, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        return db.EvaluationRuns.Where(r => r.Id == runId).ExecuteUpdateAsync(s => s
            .SetProperty(r => r.Status, status)
            .SetProperty(r => r.Phase, string.Empty)
            .SetProperty(r => r.Summary, summary)
            .SetProperty(r => r.Error, error)
            .SetProperty(r => r.CompletedAt, now), ct);
    }

    /// <summary>Marks a run as the baseline and clears the mark from any other run.</summary>
    public async Task SetBaselineAsync(Guid runId, bool isBaseline, CancellationToken ct = default)
    {
        await db.EvaluationRuns.Where(r => r.IsBaseline).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsBaseline, false), ct);
        if (isBaseline)
        {
            await db.EvaluationRuns.Where(r => r.Id == runId).ExecuteUpdateAsync(s => s.SetProperty(r => r.IsBaseline, true), ct);
        }
    }

    public Task DeleteRunAsync(Guid runId, CancellationToken ct = default) =>
        db.EvaluationRuns.Where(r => r.Id == runId).ExecuteDeleteAsync(ct);

    /// <summary>A run still marked active after a restart was interrupted: nothing is executing it.</summary>
    public Task<int> FailInterruptedAsync(string error, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        return db.EvaluationRuns
            .Where(r => r.Status == EvalRunStatus.Queued || r.Status == EvalRunStatus.Running)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, EvalRunStatus.Failed)
                .SetProperty(r => r.Phase, string.Empty)
                .SetProperty(r => r.Error, error)
                .SetProperty(r => r.CompletedAt, now), ct);
    }
}
