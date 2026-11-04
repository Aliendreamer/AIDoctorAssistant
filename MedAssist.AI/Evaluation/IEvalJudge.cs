using MedAssist.Shared.Models.Evaluation;

namespace MedAssist.AI.Evaluation;

/// <summary>The model-graded checks. Implementations throw when they cannot produce a verdict.</summary>
public interface IEvalJudge
{
    /// <summary>For each statement, whether <paramref name="text"/> states it.</summary>
    Task<IReadOnlyList<bool>> CheckStatementsAsync(
        string text, IReadOnlyList<string> statements, CancellationToken cancellationToken);

    /// <summary>For each claim, whether the excerpts support it and whether its citations do.</summary>
    Task<IReadOnlyList<ClaimCheck>> CheckClaimsAsync(
        IReadOnlyList<EvalExcerpt> excerpts, IReadOnlyList<AnswerClaim> claims, CancellationToken cancellationToken);
}
