using MedAssist.AI.Evaluation;
using MedAssist.Shared.Constants;
using MedAssist.Shared.Models;
using MedAssist.Shared.Models.Evaluation;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace MedAssist.Tests;

public sealed class EvalJudgeTests
{
    private sealed class ScriptedChat(params string[] replies) : IChatCompletionService
    {
        private int _next;

        public int Calls => _next;
        public PromptExecutionSettings? LastSettings { get; private set; }
        public string LastPrompt { get; private set; } = string.Empty;
        public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?>();

        public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
            ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null,
            Microsoft.SemanticKernel.Kernel? kernel = null, CancellationToken cancellationToken = default)
        {
            LastSettings = executionSettings;
            LastPrompt = chatHistory[^1].Content ?? string.Empty;
            IReadOnlyList<ChatMessageContent> reply = [new(AuthorRole.Assistant, replies[Math.Min(_next, replies.Length - 1)])];
            _next++;
            return Task.FromResult(reply);
        }

        public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
            ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null,
            Microsoft.SemanticKernel.Kernel? kernel = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class StubJudge : IEvalJudge
    {
        public Func<string, IReadOnlyList<string>, IReadOnlyList<bool>> Statements { get; init; } = (_, s) => s.Select(_ => true).ToList();
        public Exception? Failure { get; init; }
        public int Calls { get; private set; }

        public Task<IReadOnlyList<bool>> CheckStatementsAsync(string text, IReadOnlyList<string> statements, CancellationToken cancellationToken)
        {
            Calls++;
            return Failure is not null ? throw Failure : Task.FromResult(Statements(text, statements));
        }

        public Task<IReadOnlyList<ClaimCheck>> CheckClaimsAsync(IReadOnlyList<EvalExcerpt> excerpts, IReadOnlyList<AnswerClaim> claims, CancellationToken cancellationToken)
        {
            Calls++;
            IReadOnlyList<ClaimCheck> checks = claims.Select(c => new ClaimCheck { Claim = c.Text, Supported = true }).ToList();
            return Task.FromResult(checks);
        }
    }

    private static readonly EvalCase _case = new()
    {
        Id = "G1",
        RequiredFacts = ["fact one", "fact two"],
        ForbiddenClaims = ["bad claim"],
    };

    private static EvalCaseResult Answered() => new()
    {
        CaseId = "G1",
        Answer = "This sentence is long enough to be treated as a claim [1].",
        Outcome = RagOutcome.Answered,
        Excerpts = [new EvalExcerpt { Number = 1, Text = "excerpt body" }],
        Format = new FormatScore { HasMarkers = true, MarkersInRange = true, LanguageOk = true },
        Retrieval = new RetrievalScore { HitAtK = true },
    };

    [Fact]
    public async Task CheckStatements_ReadsVerdictsInOrder()
    {
        var chat = new ScriptedChat("""{"results":[{"id":2,"stated":false},{"id":1,"stated":true}]}""");
        var sut = new OllamaEvalJudge(chat, "judge:7b", 12_288);

        var verdicts = await sut.CheckStatementsAsync("text", ["a", "b"], CancellationToken.None);

        Assert.Equal([true, false], verdicts);
    }

    [Fact]
    public async Task Requests_UseTheJudgeModelAtTemperatureZeroAsJson()
    {
        var chat = new ScriptedChat("""{"results":[{"id":1,"stated":true}]}""");
        var sut = new OllamaEvalJudge(chat, "judge:7b", 12_288);

        await sut.CheckStatementsAsync("text", ["a"], CancellationToken.None);

        var settings = chat.LastSettings!;
        Assert.Equal("judge:7b", settings.ModelId);
        Assert.Equal(12_288, settings.ExtensionData!["num_ctx"]);
        Assert.Equal(0.0, settings.ExtensionData["temperature"]);
        Assert.Equal("json_object", settings.ExtensionData["response_format"]);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("""{"results":[{"id":1}]}""")]
    [InlineData("""{"results":[{"id":1,"stated":"yes"}]}""")]
    [InlineData("""{"results":[]}""")]
    [InlineData("""{"other":true}""")]
    public async Task MalformedReply_IsRetriedOnce(string bad)
    {
        var chat = new ScriptedChat(bad, """{"results":[{"id":1,"stated":true}]}""");
        var sut = new OllamaEvalJudge(chat, null, 8_192);

        var verdicts = await sut.CheckStatementsAsync("text", ["a"], CancellationToken.None);

        Assert.Equal([true], verdicts);
        Assert.Equal(2, chat.Calls);
    }

    [Fact]
    public async Task RepeatedlyMalformedReply_Throws()
    {
        var chat = new ScriptedChat("nope");
        var sut = new OllamaEvalJudge(chat, null, 8_192);

        await Assert.ThrowsAsync<FormatException>(() => sut.CheckStatementsAsync("text", ["a"], CancellationToken.None));
        Assert.Equal(EvaluationConstants.JudgeAttempts, chat.Calls);
    }

    [Fact]
    public async Task ThinkBlockAndFences_AreIgnored()
    {
        var chat = new ScriptedChat("""
            <think>{"results":[{"id":1,"stated":false}]}</think>
            ```json
            {"results":[{"id":1,"stated":true}]}
            ```
            """);
        var sut = new OllamaEvalJudge(chat, null, 8_192);

        Assert.Equal([true], await sut.CheckStatementsAsync("text", ["a"], CancellationToken.None));
    }

    [Fact]
    public async Task NoStatements_MakesNoCall()
    {
        var chat = new ScriptedChat("unused");
        var sut = new OllamaEvalJudge(chat, null, 8_192);

        Assert.Empty(await sut.CheckStatementsAsync("text", [], CancellationToken.None));
        Assert.Equal(0, chat.Calls);
    }

    [Fact]
    public async Task CheckClaims_ReportsCitationOnlyForCitedClaims()
    {
        var chat = new ScriptedChat("""{"results":[{"id":1,"supported":true,"citation_ok":false},{"id":2,"supported":false}]}""");
        var sut = new OllamaEvalJudge(chat, null, 8_192);
        var excerpts = new[] { new EvalExcerpt { Number = 1, Text = "body" } };

        var checks = await sut.CheckClaimsAsync(excerpts, [new AnswerClaim("cited", [1]), new AnswerClaim("uncited", [])], CancellationToken.None);

        Assert.True(checks[0].Supported);
        Assert.False(checks[0].CitationOk);
        Assert.False(checks[1].Supported);
        Assert.Null(checks[1].CitationOk);
        Assert.Contains("(cites: 1) cited", chat.LastPrompt);
        Assert.Contains("(cites: none) uncited", chat.LastPrompt);
    }

    [Fact]
    public async Task CaseJudge_SeparatesAnswerAndExcerptChecks()
    {
        var judge = new StubJudge
        {
            Statements = (text, statements) => text == "excerpt body"
                ? statements.Select(s => s == "fact two").ToList()
                : statements.Select(s => s is "fact one" or "bad claim").ToList(),
        };

        var result = await CaseJudge.JudgeAsync(_case, Answered(), judge, CancellationToken.None);

        Assert.True(result.Judged);
        Assert.Equal(true, result.Facts[0].InAnswer);
        Assert.Equal(false, result.Facts[0].InExcerpts);
        Assert.Equal(false, result.Facts[1].InAnswer);
        Assert.Equal(true, result.Facts[1].InExcerpts);
        Assert.Equal(["bad claim"], result.ForbiddenStated);
        Assert.False(result.Passed);
    }

    [Fact]
    public async Task CaseJudge_NoAnswerSkipsTheJudge()
    {
        var judge = new StubJudge();

        var result = await CaseJudge.JudgeAsync(_case, Answered() with { Outcome = RagOutcome.WebFallback }, judge, CancellationToken.None);

        Assert.Equal(0, judge.Calls);
        Assert.False(result.Judged);
    }

    [Fact]
    public async Task CaseJudge_JudgeFailureLeavesTheCaseUnjudgedNotFailed()
    {
        var judge = new StubJudge { Failure = new FormatException("bad reply") };

        var result = await CaseJudge.JudgeAsync(_case, Answered(), judge, CancellationToken.None);

        Assert.False(result.Judged);
        Assert.Equal("bad reply", result.JudgeError);
        Assert.Empty(result.Facts);
        Assert.True(result.Inconclusive);
        Assert.False(result.ConfirmedPass);
    }

    [Fact]
    public async Task CaseJudge_CancellationPropagates()
    {
        var judge = new StubJudge { Failure = new OperationCanceledException() };

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => CaseJudge.JudgeAsync(_case, Answered(), judge, CancellationToken.None));
    }

    [Fact]
    public async Task CaseJudge_AbstainCaseAsksOnlyWhetherItAbstained()
    {
        var evalCase = new EvalCase { Id = "N1", Expectation = EvalExpectation.Abstain };
        var answered = Answered() with { Expectation = EvalExpectation.Abstain, Retrieval = null };
        var judge = new StubJudge { Statements = (_, s) => s.Select(_ => false).ToList() };

        var result = await CaseJudge.JudgeAsync(evalCase, answered, judge, CancellationToken.None);

        Assert.Equal(1, judge.Calls);
        Assert.Equal(false, result.Abstained);
        Assert.False(result.Passed);
    }
}
