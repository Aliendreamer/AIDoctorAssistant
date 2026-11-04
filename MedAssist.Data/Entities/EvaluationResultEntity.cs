using MedAssist.Shared.Models.Evaluation;

namespace MedAssist.Data.Entities;

public sealed class EvaluationResultEntity
{
    public int Id { get; set; }
    public Guid RunId { get; set; }
    public string CaseId { get; set; } = string.Empty;

    /// <summary>Position in the golden set, so results list in run order.</summary>
    public int Ordinal { get; set; }

    /// <summary>JSON <see cref="EvalCaseResult"/>.</summary>
    public string Details { get; set; } = string.Empty;
}
