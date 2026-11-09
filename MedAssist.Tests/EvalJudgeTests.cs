using MedAssist.AI.Evaluation;
using MedAssist.Shared.Constants;
using MedAssist.Shared.Models;
using MedAssist.Shared.Models.Evaluation;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace MedAssist.Tests;

public sealed class EvalJudgeTests
{
    private const string BrokenStream = "(the stream breaks here)";

    private sealed class ScriptedChat(params string[] replies) : IChatCompletionService
    {
        private int _next;

        public int Calls => _next;
        public PromptExecutionSettings? LastSettings { get; private set; }
        public string LastPrompt { get; private set; } = string.Empty;
        public List<string> Prompts { get; } = [];
        public List<bool> JsonModes { get; } = [];
        public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?>();

        public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
            ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null,
            Microsoft.SemanticKernel.Kernel? kernel = null, CancellationToken cancellationToken = default)
        {
            LastSettings = executionSettings;
            LastPrompt = chatHistory[^1].Content ?? string.Empty;
            Prompts.Add(LastPrompt);
            JsonModes.Add(executionSettings?.ExtensionData?.ContainsKey("response_format") == true);
            var text = replies[Math.Min(_next, replies.Length - 1)];
            _next++;
            if (text == BrokenStream)
            {
                throw new InvalidOperationException("The stream might be corrupted or incomplete.");
            }

            IReadOnlyList<ChatMessageContent> reply = [new(AuthorRole.Assistant, text)];
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

    private static readonly EvalExcerpt[] _claimExcerpts =
    [
        new() { Number = 1, Text = "Парацетамолът се резорбира бързо след поемането му." },
        new() { Number = 2, Text = "Дозировката му е 0,14 g/kg на всеки 4 часа." },
    ];

    private const string _quoteOne = """{"quotes":["Парацетамолът се резорбира бързо след поемането му."]}""";
    private const string _quoteTwo = """{"quotes":["Дозировката му е 0,14 g/kg на всеки 4 часа."]}""";
    private const string _allStated = """{"facts":[{"fact":"a","stated":"yes"},{"fact":"b","stated":"yes"}]}""";

    [Fact]
    public async Task CheckClaims_SupportedWhenEveryFactIsStatedInARealQuote()
    {
        var chat = new ScriptedChat(_quoteOne, _allStated);
        var sut = new OllamaEvalJudge(chat, null, 8_192);

        var checks = await sut.CheckClaimsAsync(_claimExcerpts, [new AnswerClaim("rapidly absorbed", [1])], CancellationToken.None);

        Assert.True(checks[0].Supported);
        Assert.True(checks[0].CitationOk);
        Assert.Single(checks[0].Evidence);
        Assert.Empty(checks[0].UnstatedFacts);
    }

    [Fact]
    public async Task CheckClaims_OneUnstatedFactFailsTheClaimAndIsRecorded()
    {
        const string oneUnstated = """{"facts":[{"fact":"a","stated":"yes"},{"fact":"b","stated":"no"}]}""";
        var chat = new ScriptedChat(_quoteOne, oneUnstated, _quoteOne, oneUnstated);
        var sut = new OllamaEvalJudge(chat, null, 8_192);

        var checks = await sut.CheckClaimsAsync(_claimExcerpts, [new AnswerClaim("claim", [1])], CancellationToken.None);

        Assert.False(checks[0].Supported);
        Assert.False(checks[0].CitationOk);
        Assert.Equal(["b"], checks[0].UnstatedFacts);
    }

    [Fact]
    public async Task CheckClaims_CitationIsDecidedByWhereTheQuoteActuallyIs()
    {
        var chat = new ScriptedChat(_quoteOne, _allStated);
        var sut = new OllamaEvalJudge(chat, null, 8_192);

        var checks = await sut.CheckClaimsAsync(_claimExcerpts, [new AnswerClaim("claim", [2])], CancellationToken.None);

        Assert.True(checks[0].Supported);
        Assert.False(checks[0].CitationOk);
    }

    [Fact]
    public async Task CheckClaims_UncitedClaimHasNoCitationVerdict()
    {
        var chat = new ScriptedChat(_quoteOne, _allStated);
        var sut = new OllamaEvalJudge(chat, null, 8_192);

        var checks = await sut.CheckClaimsAsync(_claimExcerpts, [new AnswerClaim("claim", [])], CancellationToken.None);

        Assert.True(checks[0].Supported);
        Assert.Null(checks[0].CitationOk);
    }

    [Theory]
    [InlineData("""{"quotes":[]}""")]
    [InlineData("""{"quotes":["The model made this sentence up entirely."]}""")]
    public async Task CheckClaims_NoRealQuoteFailsWithoutASecondCall(string reply)
    {
        var chat = new ScriptedChat(reply);
        var sut = new OllamaEvalJudge(chat, null, 8_192);

        var checks = await sut.CheckClaimsAsync(_claimExcerpts, [new AnswerClaim("claim", [])], CancellationToken.None);

        Assert.False(checks[0].Supported);
        Assert.Null(checks[0].CitationOk);
        Assert.Equal(1, chat.Calls);
    }

    [Fact]
    public async Task CheckClaims_ANumberMissingFromTheEvidenceFailsEvenIfTheModelWouldPassIt()
    {
        var chat = new ScriptedChat(_quoteTwo, _allStated);
        var sut = new OllamaEvalJudge(chat, null, 8_192);

        var checks = await sut.CheckClaimsAsync(_claimExcerpts, [new AnswerClaim("0.41 g/kg every 4 hours", [2])], CancellationToken.None);

        Assert.False(checks[0].Supported);
        Assert.Equal(["0.41"], checks[0].UnstatedFacts);
        Assert.Equal(1, chat.Calls);
    }

    [Fact]
    public async Task CheckClaims_FindsAllEvidenceFirstSoTheExcerptPrefixStaysCached()
    {
        var chat = new ScriptedChat(_quoteOne, _quoteTwo, _allStated, _allStated);
        var sut = new OllamaEvalJudge(chat, null, 8_192);

        await sut.CheckClaimsAsync(_claimExcerpts, [new AnswerClaim("one", [1]), new AnswerClaim("two", [2])], CancellationToken.None);

        Assert.Equal(4, chat.Calls);
        Assert.All(chat.Prompts.Take(2), p => Assert.StartsWith("<excerpt number=\"1\">", p));
        Assert.All(chat.Prompts.Skip(2), p => Assert.StartsWith("<evidence>", p));
        Assert.DoesNotContain("Дозировката", chat.Prompts[2]);
    }

    [Fact]
    public async Task CheckClaims_EvidenceCarriesTheHeadingOfItsExcerpt()
    {
        var chat = new ScriptedChat(_quoteOne, _allStated);
        var sut = new OllamaEvalJudge(chat, null, 8_192);
        EvalExcerpt[] excerpts = [_claimExcerpts[0] with { Section = "Отравяне с парацетамол" }, _claimExcerpts[1]];

        await sut.CheckClaimsAsync(excerpts, [new AnswerClaim("Paracetamol is absorbed quickly", [1])], CancellationToken.None);

        var check = chat.Prompts[1];
        Assert.StartsWith("<evidence>", check);
        Assert.Contains("Section: Отравяне с парацетамол", check);
        Assert.True(check.IndexOf("Section: Отравяне с парацетамол", StringComparison.Ordinal)
                    < check.IndexOf(_claimExcerpts[0].Text, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("""{"other":true}""")]
    [InlineData("""{"quotes":"not a list"}""")]
    public async Task CheckClaims_MalformedQuoteReplyIsRetried(string bad)
    {
        var chat = new ScriptedChat(bad, _quoteOne, _allStated);
        var sut = new OllamaEvalJudge(chat, null, 8_192);

        var checks = await sut.CheckClaimsAsync(_claimExcerpts, [new AnswerClaim("claim", [1])], CancellationToken.None);

        Assert.True(checks[0].Supported);
        Assert.Equal(3, chat.Calls);
    }

    [Theory]
    [InlineData("""{"facts":[]}""")]
    [InlineData("""{"facts":[{"fact":"a","stated":"yes or no"}]}""")]
    [InlineData("""{"facts":[{"fact":"a"}]}""")]
    public async Task CheckClaims_UnreadableFactVerdictIsRetriedNotTreatedAsAnAnswer(string bad)
    {
        var chat = new ScriptedChat(_quoteOne, bad, _allStated);
        var sut = new OllamaEvalJudge(chat, null, 8_192);

        var checks = await sut.CheckClaimsAsync(_claimExcerpts, [new AnswerClaim("claim", [1])], CancellationToken.None);

        Assert.True(checks[0].Supported);
        Assert.Equal(3, chat.Calls);
    }

    [Fact]
    public async Task CheckClaims_NoClaimsMakesNoCall()
    {
        var chat = new ScriptedChat("unused");
        var sut = new OllamaEvalJudge(chat, null, 8_192);

        Assert.Empty(await sut.CheckClaimsAsync(_claimExcerpts, [], CancellationToken.None));
        Assert.Equal(0, chat.Calls);
    }

    private const string _nothingStated = """{"facts":[{"fact":"a","stated":"no"}]}""";

    [Fact]
    public async Task CheckClaims_RejectedClaimIsSearchedAgainInsideItsCitedExcerpts()
    {
        var chat = new ScriptedChat(_quoteTwo, _nothingStated, _quoteOne, _allStated);
        var sut = new OllamaEvalJudge(chat, null, 8_192);

        var checks = await sut.CheckClaimsAsync(_claimExcerpts, [new AnswerClaim("claim", [1])], CancellationToken.None);

        Assert.True(checks[0].Supported);
        Assert.True(checks[0].CitationOk);
        Assert.True(checks[0].Rechecked);
        Assert.Empty(checks[0].UnstatedFacts);
        Assert.Contains(_claimExcerpts[0].Text, chat.Prompts[2]);
        Assert.DoesNotContain(_claimExcerpts[1].Text, chat.Prompts[2]);
        Assert.StartsWith("<evidence>", chat.LastPrompt);
    }

    [Fact]
    public async Task CheckClaims_NoQuoteStillGetsTheRecheckWhenTheClaimCitesAnExcerpt()
    {
        var chat = new ScriptedChat("""{"quotes":[]}""", _quoteOne, _allStated);
        var sut = new OllamaEvalJudge(chat, null, 8_192);

        var checks = await sut.CheckClaimsAsync(_claimExcerpts, [new AnswerClaim("claim", [1])], CancellationToken.None);

        Assert.True(checks[0].Supported);
        Assert.Equal(3, chat.Calls);
    }

    [Fact]
    public async Task CheckClaims_RecheckThatAlsoFailsKeepsTheRejection()
    {
        var chat = new ScriptedChat(_quoteTwo, _nothingStated, _quoteOne, """{"facts":[{"fact":"z","stated":"no"}]}""");
        var sut = new OllamaEvalJudge(chat, null, 8_192);

        var checks = await sut.CheckClaimsAsync(_claimExcerpts, [new AnswerClaim("claim", [1])], CancellationToken.None);

        Assert.False(checks[0].Supported);
        Assert.False(checks[0].CitationOk);
        Assert.False(checks[0].Rechecked);
        Assert.Equal(["a"], checks[0].UnstatedFacts);
    }

    [Fact]
    public async Task CheckClaims_RecheckQuoteFromAnUncitedExcerptDoesNotCount()
    {
        var chat = new ScriptedChat(_quoteTwo, _nothingStated, _quoteTwo);
        var sut = new OllamaEvalJudge(chat, null, 8_192);

        var checks = await sut.CheckClaimsAsync(_claimExcerpts, [new AnswerClaim("claim", [1])], CancellationToken.None);

        Assert.False(checks[0].Supported);
        Assert.Equal(3, chat.Calls);
    }

    [Fact]
    public async Task CheckClaims_RecheckCannotRescueANumberTheCitedExcerptLacks()
    {
        var chat = new ScriptedChat(_quoteTwo, _allStated);
        var sut = new OllamaEvalJudge(chat, null, 8_192);

        var checks = await sut.CheckClaimsAsync(_claimExcerpts, [new AnswerClaim("0.41 g/kg", [2])], CancellationToken.None);

        Assert.False(checks[0].Supported);
        Assert.Equal(1, chat.Calls);
    }

    [Fact]
    public async Task CheckClaims_ACitationOutsideTheExcerptsIsNotRechecked()
    {
        var chat = new ScriptedChat(_quoteTwo, _nothingStated);
        var sut = new OllamaEvalJudge(chat, null, 8_192);

        var checks = await sut.CheckClaimsAsync(_claimExcerpts, [new AnswerClaim("claim", [9])], CancellationToken.None);

        Assert.False(checks[0].Supported);
        Assert.Equal(2, chat.Calls);
    }

    [Fact]
    public async Task BrokenStream_IsRetried()
    {
        var chat = new ScriptedChat(BrokenStream, """{"results":[{"id":1,"stated":true}]}""");
        var sut = new OllamaEvalJudge(chat, null, 8_192);

        Assert.Equal([true], await sut.CheckStatementsAsync("text", ["a"], CancellationToken.None));
        Assert.Equal(2, chat.Calls);
    }

    [Fact]
    public async Task Retry_DropsJsonMode()
    {
        var chat = new ScriptedChat(BrokenStream, """{"results":[{"id":1,"stated":true}]}""");
        var sut = new OllamaEvalJudge(chat, null, 8_192);

        await sut.CheckStatementsAsync("text", ["a"], CancellationToken.None);

        Assert.Equal([true, false], chat.JsonModes);
    }

    [Fact]
    public async Task CheckClaims_PlainQuotesInExcerptsReachTheJudgeAsTypographicOnes()
    {
        var chat = new ScriptedChat(
            """{"quotes":["Може да причини болестта \u201Dръка, крак и уста\u201D при деца."]}""", _allStated);
        var sut = new OllamaEvalJudge(chat, null, 8_192);
        EvalExcerpt[] excerpts = [new() { Number = 1, Text = "Може да причини болестта \"ръка, крак и уста\" при деца." }];

        var checks = await sut.CheckClaimsAsync(
            excerpts, [new AnswerClaim("Може да причини болестта ръка, крак и уста при деца.", [1])], CancellationToken.None);

        Assert.Contains("\u201Dръка, крак и уста\u201D", chat.Prompts[0]);
        Assert.DoesNotContain("\"ръка", chat.Prompts[0]);
        Assert.True(checks[0].Supported);
    }

    [Fact]
    public async Task Reply_WithAStrayClosingBraceIsStillRead()
    {
        var chat = new ScriptedChat("""{"results":[{"id":1,"stated":true}]}}""");
        var sut = new OllamaEvalJudge(chat, null, 8_192);

        Assert.Equal([true], await sut.CheckStatementsAsync("text", ["a"], CancellationToken.None));
        Assert.Equal(1, chat.Calls);
    }

    [Fact]
    public async Task CheckClaims_OneFailedSentenceIsLeftUnjudgedAndTheRestStand()
    {
        var chat = new ScriptedChat("nope", "nope", _quoteOne, _allStated);
        var sut = new OllamaEvalJudge(chat, null, 8_192);

        var checks = await sut.CheckClaimsAsync(_claimExcerpts, [new AnswerClaim("bad", [1]), new AnswerClaim("good", [1])], CancellationToken.None);

        Assert.NotNull(checks[0].JudgeError);
        Assert.False(checks[0].Supported);
        Assert.Null(checks[0].CitationOk);
        Assert.Null(checks[1].JudgeError);
        Assert.True(checks[1].Supported);
    }

    [Fact]
    public async Task CheckClaims_FailedRecheckLeavesTheSentenceUnjudged()
    {
        var chat = new ScriptedChat(_quoteTwo, _quoteOne, _nothingStated, _allStated, "nope", "nope");
        var sut = new OllamaEvalJudge(chat, null, 8_192);

        var checks = await sut.CheckClaimsAsync(_claimExcerpts, [new AnswerClaim("bad", [1]), new AnswerClaim("good", [1])], CancellationToken.None);

        Assert.NotNull(checks[0].JudgeError);
        Assert.True(checks[1].Supported);
    }

    [Fact]
    public async Task CheckClaims_EverySentenceFailingThrowsSoTheQuestionStaysUnjudged()
    {
        var sut = new OllamaEvalJudge(new ScriptedChat("nope"), null, 8_192);

        await Assert.ThrowsAsync<FormatException>(
            () => sut.CheckClaimsAsync(_claimExcerpts, [new AnswerClaim("one", [1]), new AnswerClaim("two", [1])], CancellationToken.None));
    }

    [Fact]
    public async Task CheckClaims_CancellationIsNotSwallowed()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var sut = new OllamaEvalJudge(new CancellingChat(), null, 8_192);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sut.CheckClaimsAsync(_claimExcerpts, [new AnswerClaim("one", [1])], cancelled.Token));
    }

    private sealed class CancellingChat : IChatCompletionService
    {
        public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?>();

        public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
            ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null,
            Microsoft.SemanticKernel.Kernel? kernel = null, CancellationToken cancellationToken = default)
            => throw new OperationCanceledException(cancellationToken);

        public IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
            ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null,
            Microsoft.SemanticKernel.Kernel? kernel = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
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
