using MedAssist.AI.Evaluation;
using MedAssist.AI.Plugins;
using MedAssist.Data;
using MedAssist.Data.Entities;
using MedAssist.Data.Repositories;
using MedAssist.Shared.Models;
using MedAssist.Shared.Models.Evaluation;
using MedAssist.Web.Services.Evaluation;
using Microsoft.Extensions.Logging.Abstractions;

namespace MedAssist.Tests;

public sealed class EvaluationRunnerTests : IDisposable
{
    private static readonly GoldenSet _golden = new(
    [
        new EvalCase
        {
            Id = "G1", Language = "EN", Question = "first", RelevantTerms = [["kawasaki"]],
            RequiredFacts = ["fact"], QueryType = QueryType.Disease,
        },
        new EvalCase
        {
            Id = "G2", Language = "EN", Question = "follow-up", FollowUpOf = "G1", RelevantTerms = [["kawasaki"]],
            RequiredFacts = ["fact"], QueryType = QueryType.Disease,
        },
        new EvalCase { Id = "N1", Language = "EN", Question = "unknown", Expectation = EvalExpectation.Abstain },
    ], "hash");

    private readonly EvalTestDb _testDb = new();
    private readonly MedAssistDbContext _db;
    private readonly EvaluationRepository _runs;
    private readonly ChatHistoryRepository _chat;

    public EvaluationRunnerTests()
    {
        _db = _testDb.CreateContext();
        _runs = new EvaluationRepository(_db);
        _chat = new ChatHistoryRepository(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _testDb.Dispose();
    }

    private sealed class FakeTarget(ChatHistoryRepository chat) : IEvaluationTarget
    {
        public List<(string Question, string User, RagRunOverrides Overrides)> Calls { get; } = [];
        public Func<QueryRequest, Task<QueryResult>>? Behaviour { get; init; }

        public async Task<QueryResult> AskAsync(QueryRequest request, string userId, CancellationToken cancellationToken)
        {
            var scope = RagRunScope.Current ?? throw new InvalidOperationException("No run scope reached the pipeline.");
            Calls.Add((request.Query, userId, scope.Overrides));
            await chat.AddMessageAsync(new ChatMessageEntity
            {
                UserId = userId, QueryType = "disease", Role = "user", Content = request.Query, CreatedAt = DateTimeOffset.UtcNow,
            }, cancellationToken);

            if (Behaviour is not null)
            {
                return await Behaviour(request);
            }

            if (request.Query == "unknown")
            {
                scope.Trace.Outcome = RagOutcome.BelowAnswerFloor;
                return new QueryResult { Answer = "Insufficient." };
            }

            var chunk = new TracedChunk { BookTitle = "Book", SectionTitle = "Kawasaki", Text = "kawasaki text" };
            scope.Trace.Outcome = RagOutcome.Answered;
            scope.Trace.Ranked = [chunk];
            scope.Trace.Excerpts = [chunk];
            scope.Trace.PromptTokens = 900;
            return new QueryResult
            {
                Answer = "This is a long enough clinical sentence about the disease [1].",
                Sources = [new SourceCitation { SourceType = SourceType.Book }],
            };
        }
    }

    private sealed class FakeJudge : IEvalJudge
    {
        public int Calls { get; private set; }
        public Action? OnCall { get; init; }

        public Task<IReadOnlyList<bool>> CheckStatementsAsync(string text, IReadOnlyList<string> statements, CancellationToken cancellationToken)
        {
            Calls++;
            OnCall?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<bool>>(statements.Select(_ => true).ToList());
        }

        public Task<IReadOnlyList<ClaimCheck>> CheckClaimsAsync(IReadOnlyList<EvalExcerpt> excerpts, IReadOnlyList<AnswerClaim> claims, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<IReadOnlyList<ClaimCheck>>(claims.Select(c => new ClaimCheck { Claim = c.Text, Supported = true, CitationOk = true }).ToList());
        }
    }

    private sealed class FakeJudgeFactory(IEvalJudge judge) : IEvalJudgeFactory
    {
        public EvalRunSnapshot? Snapshot { get; private set; }

        public IEvalJudge Create(EvalRunSnapshot snapshot)
        {
            Snapshot = snapshot;
            return judge;
        }
    }

    private EvaluationRunner Runner(IEvaluationTarget target, IEvalJudge? judge = null) => new(
        target, new FakeJudgeFactory(judge ?? new FakeJudge()), _runs, _chat, _golden, NullLogger<EvaluationRunner>.Instance);

    private async Task<Guid> AddRunAsync(bool runJudge = true, RagRunOverrides? overrides = null)
    {
        var id = Guid.NewGuid();
        await _runs.AddRunAsync(new EvaluationRunEntity
        {
            Id = id,
            Status = EvalRunStatus.Queued,
            CreatedAt = DateTimeOffset.UtcNow,
            Snapshot = EvalJson.Serialize(new EvalRunSnapshot
            {
                Model = "m",
                ContextWindowTokens = 8_192,
                RunJudge = runJudge,
                Overrides = overrides ?? new RagRunOverrides { BookOnly = true },
            }),
        });
        return id;
    }

    private async Task<IReadOnlyList<EvalCaseResult>> ResultsAsync(Guid run) =>
        (await _runs.GetResultsAsync(run)).Select(r => EvalJson.Deserialize<EvalCaseResult>(r.Details)).ToList();

    [Fact]
    public async Task Run_CompletesWithAllQuestionsScored()
    {
        var run = await AddRunAsync();

        await Runner(new FakeTarget(_chat)).RunAsync(run, CancellationToken.None);

        var entity = await _runs.GetRunAsync(run);
        var summary = EvalJson.Deserialize<EvalRunSummary>(entity!.Summary!);
        Assert.Equal(EvalRunStatus.Completed, entity.Status);
        Assert.Equal(3, summary.Cases);
        Assert.Equal(3, summary.Passed);
        Assert.Equal(1, summary.FactRecall);
        Assert.Equal(1, summary.HitAtK);
        Assert.Equal(1, summary.AbstainAccuracy);
        Assert.Equal(3, (await ResultsAsync(run)).Count);
    }

    [Fact]
    public async Task Run_OverridesReachThePipeline()
    {
        var run = await AddRunAsync(overrides: new RagRunOverrides { ModelId = "other:7b", MaxExcerpts = 8, BookOnly = true });
        var target = new FakeTarget(_chat);

        await Runner(target).RunAsync(run, CancellationToken.None);

        Assert.All(target.Calls, c => Assert.Equal("other:7b", c.Overrides.ModelId));
        Assert.All(target.Calls, c => Assert.True(c.Overrides.BookOnly));
        Assert.Null(RagRunScope.Current);
    }

    [Fact]
    public async Task Run_IsolatesQuestionsExceptFollowUps()
    {
        var run = await AddRunAsync();
        var target = new FakeTarget(_chat);

        await Runner(target).RunAsync(run, CancellationToken.None);

        var users = target.Calls.ToDictionary(c => c.Question, c => c.User);
        Assert.Equal(users["first"], users["follow-up"]);
        Assert.NotEqual(users["first"], users["unknown"]);
    }

    [Fact]
    public async Task Run_RemovesItsChatHistoryAndNobodyElses()
    {
        await _chat.AddMessageAsync(new ChatMessageEntity
        {
            UserId = "dr.ivanova", QueryType = "disease", Role = "user", Content = "keep", CreatedAt = DateTimeOffset.UtcNow,
        });
        var run = await AddRunAsync();
        var target = new FakeTarget(_chat);

        await Runner(target).RunAsync(run, CancellationToken.None);

        Assert.Empty(await _chat.GetRecentAsync(target.Calls[0].User, "disease"));
        Assert.Single(await _chat.GetRecentAsync("dr.ivanova", "disease"));
    }

    [Fact]
    public async Task Run_WithoutJudgeNeverCallsIt_AndStillScoresRetrieval()
    {
        var run = await AddRunAsync(runJudge: false);
        var judge = new FakeJudge();

        await Runner(new FakeTarget(_chat), judge).RunAsync(run, CancellationToken.None);

        var summary = EvalJson.Deserialize<EvalRunSummary>((await _runs.GetRunAsync(run))!.Summary!);
        Assert.Equal(0, judge.Calls);
        Assert.Null(summary.FactRecall);
        Assert.Equal(1, summary.HitAtK);
    }

    [Fact]
    public async Task Run_AnswersEverythingBeforeJudgingAnything()
    {
        var run = await AddRunAsync();
        var target = new FakeTarget(_chat);
        var answeredAtFirstJudgeCall = -1;
        var judge = new FakeJudge
        {
            OnCall = () =>
            {
                if (answeredAtFirstJudgeCall < 0)
                {
                    answeredAtFirstJudgeCall = target.Calls.Count;
                }
            },
        };

        await Runner(target, judge).RunAsync(run, CancellationToken.None);

        Assert.Equal(3, answeredAtFirstJudgeCall);
    }

    [Fact]
    public async Task Run_SwallowedPipelineErrorCountsAsAnError()
    {
        // QueryService reports failures as a normal answer plus Trace.Error, never as an exception.
        var run = await AddRunAsync();
        var target = new FakeTarget(_chat)
        {
            Behaviour = _ =>
            {
                RagRunScope.Current!.Trace.Error = "connection refused";
                return Task.FromResult(new QueryResult { Answer = "An error occurred while processing your query." });
            },
        };

        await Runner(target).RunAsync(run, CancellationToken.None);

        var results = await ResultsAsync(run);
        var summary = EvalJson.Deserialize<EvalRunSummary>((await _runs.GetRunAsync(run))!.Summary!);
        Assert.All(results, r => Assert.False(r.Passed));
        Assert.Equal(3, summary.Errors);
    }

    [Fact]
    public async Task Run_CancelledWhileAnsweringKeepsPartialResultsWithoutASummary()
    {
        var run = await AddRunAsync();
        using var cancellation = new CancellationTokenSource();
        var target = new FakeTarget(_chat)
        {
            Behaviour = _ =>
            {
                cancellation.Cancel();
                return Task.FromResult(new QueryResult { Answer = "An error occurred." });
            },
        };

        await Runner(target).RunAsync(run, cancellation.Token);

        var entity = await _runs.GetRunAsync(run);
        Assert.Equal(EvalRunStatus.Cancelled, entity!.Status);
        Assert.Null(entity.Summary);
        Assert.Empty(await _chat.GetRecentAsync(target.Calls[0].User, "disease"));
    }

    [Fact]
    public async Task Run_CancelledBeforeItStartsDoesNotStayQueued()
    {
        var run = await AddRunAsync();
        var target = new FakeTarget(_chat);

        await Runner(target).RunAsync(run, new CancellationToken(canceled: true));

        Assert.Equal(EvalRunStatus.Cancelled, (await _runs.GetRunAsync(run))!.Status);
        Assert.Empty(target.Calls);
    }

    [Fact]
    public async Task Run_CancelledWhileJudging()
    {
        var run = await AddRunAsync();
        using var cancellation = new CancellationTokenSource();
        var judge = new FakeJudge { OnCall = cancellation.Cancel };

        await Runner(new FakeTarget(_chat), judge).RunAsync(run, cancellation.Token);

        Assert.Equal(EvalRunStatus.Cancelled, (await _runs.GetRunAsync(run))!.Status);
    }

    [Fact]
    public async Task Run_UnexpectedFailureMarksTheRunFailed()
    {
        var run = await AddRunAsync();
        var target = new FakeTarget(_chat) { Behaviour = _ => throw new InvalidOperationException("boom") };

        await Runner(target).RunAsync(run, CancellationToken.None);

        var entity = await _runs.GetRunAsync(run);
        Assert.Equal(EvalRunStatus.Failed, entity!.Status);
        Assert.Equal("boom", entity.Error);
    }

    [Fact]
    public async Task Run_MissingRunDoesNotThrowOutOfTheRunner()
    {
        await Runner(new FakeTarget(_chat)).RunAsync(Guid.NewGuid(), CancellationToken.None);
    }

    [Fact]
    public async Task Run_CorruptSnapshotFailsTheRunInsteadOfLeavingItQueued()
    {
        var id = Guid.NewGuid();
        await _runs.AddRunAsync(new EvaluationRunEntity
        {
            Id = id, Status = EvalRunStatus.Queued, CreatedAt = DateTimeOffset.UtcNow, Snapshot = "not json",
        });

        await Runner(new FakeTarget(_chat)).RunAsync(id, CancellationToken.None);

        Assert.Equal(EvalRunStatus.Failed, (await _runs.GetRunAsync(id))!.Status);
    }

    [Fact]
    public async Task Run_ReportsProgressAcrossBothPhases()
    {
        var run = await AddRunAsync();
        var seen = new List<(string Phase, int Done, int Total)>();
        var judge = new FakeJudge
        {
            OnCall = () =>
            {
                var snapshot = _testDb.CreateContext();
                var entity = new EvaluationRepository(snapshot).GetRunAsync(run).GetAwaiter().GetResult()!;
                seen.Add((entity.Phase, entity.Done, entity.Total));
                snapshot.Dispose();
            },
        };

        await Runner(new FakeTarget(_chat), judge).RunAsync(run, CancellationToken.None);

        Assert.Contains(seen, s => s == (EvaluationRunner.JudgingPhase, 3, 6));
    }
}
