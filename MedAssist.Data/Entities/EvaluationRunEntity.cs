using MedAssist.Shared.Models.Evaluation;

namespace MedAssist.Data.Entities;

public sealed class EvaluationRunEntity
{
    public Guid Id { get; set; }
    public string Note { get; set; } = string.Empty;
    public EvalRunStatus Status { get; set; }
    public string Phase { get; set; } = string.Empty;
    public int Done { get; set; }
    public int Total { get; set; }
    public string StartedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public bool IsBaseline { get; set; }

    /// <summary>JSON <see cref="EvalRunSnapshot"/>.</summary>
    public string Snapshot { get; set; } = string.Empty;

    /// <summary>JSON <see cref="EvalRunSummary"/>; set when the run completes.</summary>
    public string? Summary { get; set; }

    public string? Error { get; set; }
}
