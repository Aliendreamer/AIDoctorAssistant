using System.Diagnostics;
using MedAssist.AI.Evaluation;
using MedAssist.AI.Plugins;
using MedAssist.Data.Repositories;
using MedAssist.Shared.Constants;
using MedAssist.Shared.Models;
using MedAssist.Shared.Models.Evaluation;

namespace MedAssist.Web.Services.Evaluation;

/// <summary>
/// Executes one run. Every answer is generated before any is judged: latency stays free of judge
/// interference, and Ollama swaps models once per run instead of twice per question.
/// </summary>
public sealed class EvaluationRunner(
    IEvaluationTarget target,
    IEvalJudgeFactory judgeFactory,
    EvaluationRepository runs,
    ChatHistoryRepository chatHistory,
    GoldenSet goldenSet,
    ILogger<EvaluationRunner> logger)
{
    public const string AnsweringPhase = "Answering";
    public const string JudgingPhase = "Judging";

    public async Task RunAsync(Guid runId, CancellationToken cancellationToken)
    {
        try
        {
            // Not the run's token: a run cancelled before it started must still reach the Cancelled state below.
            var run = await runs.GetRunAsync(runId, CancellationToken.None)
                ?? throw new InvalidOperationException($"Evaluation run {runId} does not exist.");
            var snapshot = EvalJson.Deserialize<EvalRunSnapshot>(run.Snapshot);

            var results = await AnswerAllAsync(runId, snapshot, cancellationToken);
            if (snapshot.RunJudge)
            {
                results = await JudgeAllAsync(runId, snapshot, results, cancellationToken);
            }

            var summary = RunSummarizer.Summarize(results, snapshot.ContextWindowTokens);
            await runs.FinishAsync(runId, EvalRunStatus.Completed, EvalJson.Serialize(summary), null, CancellationToken.None);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await runs.FinishAsync(runId, EvalRunStatus.Cancelled, null, null, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Evaluation run {RunId} failed", runId);
            await runs.FinishAsync(runId, EvalRunStatus.Failed, null, ex.Message, CancellationToken.None);
        }
        finally
        {
            await chatHistory.ClearByUserPrefixAsync(RunUserPrefix(runId), CancellationToken.None);
        }
    }

    private static string RunUserPrefix(Guid runId) => $"{EvaluationConstants.EvalUserPrefix}{runId}:";

    private async Task<List<EvalCaseResult>> AnswerAllAsync(Guid runId, EvalRunSnapshot snapshot, CancellationToken cancellationToken)
    {
        var cases = goldenSet.Cases;
        var total = snapshot.RunJudge ? cases.Count * 2 : cases.Count;
        var results = new List<EvalCaseResult>(cases.Count);

        for (var i = 0; i < cases.Count; i++)
        {
            await runs.UpdateProgressAsync(runId, EvalRunStatus.Running, AnsweringPhase, i, total, cancellationToken);
            var result = CaseScorer.Decide(await AskAsync(runId, cases[i], snapshot, cancellationToken));
            results.Add(result);
            await runs.SaveResultAsync(runId, result.CaseId, i, EvalJson.Serialize(result), cancellationToken);
        }

        return results;
    }

    private async Task<List<EvalCaseResult>> JudgeAllAsync(
        Guid runId, EvalRunSnapshot snapshot, List<EvalCaseResult> answers, CancellationToken cancellationToken)
    {
        var cases = goldenSet.Cases;
        var judge = judgeFactory.Create(snapshot);
        var judged = new List<EvalCaseResult>(answers.Count);

        for (var i = 0; i < answers.Count; i++)
        {
            await runs.UpdateProgressAsync(runId, EvalRunStatus.Running, JudgingPhase, cases.Count + i, cases.Count * 2, cancellationToken);
            var result = await CaseJudge.JudgeAsync(cases[i], answers[i], judge, cancellationToken);
            judged.Add(result);
            await runs.SaveResultAsync(runId, result.CaseId, i, EvalJson.Serialize(result), cancellationToken);
        }

        return judged;
    }

    private async Task<EvalCaseResult> AskAsync(Guid runId, EvalCase evalCase, EvalRunSnapshot snapshot, CancellationToken cancellationToken)
    {
        // Each question gets a fresh conversation, so an earlier answer cannot change a later score. A
        // follow-up joins its parent's conversation, which is the behaviour it exists to test.
        var conversation = evalCase.FollowUpOf ?? evalCase.Id;
        var request = new QueryRequest { Query = evalCase.Question, QueryType = evalCase.QueryType };

        using var scope = RagRunScope.Begin(snapshot.Overrides);
        var watch = Stopwatch.StartNew();
        var answer = await target.AskAsync(request, RunUserPrefix(runId) + conversation, cancellationToken);
        watch.Stop();

        // QueryService turns every exception into a normal-looking answer, cancellation included.
        cancellationToken.ThrowIfCancellationRequested();

        return ToResult(evalCase, answer, scope.Trace, watch.Elapsed.TotalSeconds);
    }

    private static EvalCaseResult ToResult(EvalCase evalCase, QueryResult answer, RagTrace trace, double seconds)
    {
        var bookSources = answer.Sources.Count(s => s.SourceType == SourceType.Book);
        var rankedFlags = trace.Ranked.Select(c => CaseScorer.IsRelevant(evalCase, c)).ToList();
        var excerptFlags = trace.Excerpts.Select(c => CaseScorer.IsRelevant(evalCase, c)).ToList();

        return new EvalCaseResult
        {
            CaseId = evalCase.Id,
            Language = evalCase.Language,
            QueryType = evalCase.QueryType,
            Question = evalCase.Question,
            Expectation = evalCase.Expectation,
            Answer = answer.Answer,
            Error = trace.Error,
            Outcome = trace.Outcome,
            SearchQuery = trace.SearchQuery,
            LatencySeconds = Math.Round(seconds, 2),
            PromptTokens = trace.PromptTokens,
            PromptTokensEstimated = trace.PromptTokensEstimated,
            CompletionTokens = trace.CompletionTokens,
            ExcerptsDropped = trace.ExcerptsDropped,
            Excerpts = trace.Excerpts.Select((c, i) => new EvalExcerpt
            {
                Number = i + 1,
                Source = Describe(c),
                Relevant = excerptFlags[i],
                Text = c.Text,
            }).ToList(),
            Ranked = trace.Ranked.Take(EvaluationConstants.StoredRankedChunks).Select((c, i) => new EvalRankedChunk
            {
                Source = Describe(c),
                Score = c.Score,
                Relevant = rankedFlags[i],
            }).ToList(),
            Format = CaseScorer.ScoreFormat(evalCase.Language, answer.Answer, bookSources),
            Retrieval = evalCase.Expectation == EvalExpectation.Answer ? CaseScorer.ScoreRetrieval(rankedFlags, excerptFlags) : null,
        };
    }

    private static string Describe(TracedChunk chunk) =>
        $"{chunk.BookTitle} — {chunk.SectionTitle} (pp. {chunk.PageStart}–{chunk.PageEnd})";
}
