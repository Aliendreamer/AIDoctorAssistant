using MedAssist.Shared.Models;
using MedAssist.Shared.Models.Evaluation;

namespace MedAssist.AI.Evaluation;

/// <summary>Runs the judged checks for one question and folds them into its result.</summary>
public static class CaseJudge
{
    private const string _abstainStatement =
        "The text says the available information is insufficient and gives no substantive clinical answer.";

    /// <summary>
    /// A judge failure leaves the question unjudged (never scored as zero), so a flaky judge cannot
    /// look like a quality regression. Cancellation propagates.
    /// </summary>
    public static async Task<EvalCaseResult> JudgeAsync(
        EvalCase evalCase, EvalCaseResult result, IEvalJudge judge, CancellationToken cancellationToken)
    {
        if (result.Error is not null || result.Outcome != RagOutcome.Answered)
        {
            return CaseScorer.Decide(result);
        }

        try
        {
            var judged = evalCase.Expectation == EvalExpectation.Abstain
                ? await JudgeAbstentionAsync(result, judge, cancellationToken)
                : await JudgeAnswerAsync(evalCase, result, judge, cancellationToken);
            return CaseScorer.Decide(judged with { Judged = true });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CaseScorer.Decide(result with { JudgeError = ex.Message });
        }
    }

    private static async Task<EvalCaseResult> JudgeAbstentionAsync(
        EvalCaseResult result, IEvalJudge judge, CancellationToken cancellationToken)
    {
        var verdicts = await judge.CheckStatementsAsync(result.Answer, [_abstainStatement], cancellationToken);
        return result with { Abstained = verdicts[0] };
    }

    private static async Task<EvalCaseResult> JudgeAnswerAsync(
        EvalCase evalCase, EvalCaseResult result, IEvalJudge judge, CancellationToken cancellationToken)
    {
        var facts = evalCase.RequiredFacts;
        var statements = facts.Concat(evalCase.ForbiddenClaims).ToList();
        var inAnswer = await judge.CheckStatementsAsync(result.Answer, statements, cancellationToken);

        var excerptText = string.Join("\n\n", result.Excerpts.Select(e => e.Text));
        var inExcerpts = await judge.CheckStatementsAsync(excerptText, facts, cancellationToken);

        var claims = await judge.CheckClaimsAsync(result.Excerpts, CaseScorer.ExtractClaims(result.Answer), cancellationToken);

        return result with
        {
            Facts = facts.Select((fact, i) => new FactCheck { Fact = fact, InAnswer = inAnswer[i], InExcerpts = inExcerpts[i] }).ToList(),
            ForbiddenStated = evalCase.ForbiddenClaims.Where((_, i) => inAnswer[facts.Count + i]).ToList(),
            Claims = claims,
        };
    }
}
