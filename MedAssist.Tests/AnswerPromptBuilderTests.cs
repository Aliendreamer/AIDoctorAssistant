using MedAssist.AI.Plugins;
using MedAssist.Shared.Constants;
using MedAssist.Shared.Models;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace MedAssist.Tests;

// streamline-rag-prompts: every instruction reaches the model exactly once, in the placement that
// works for the local model (citation + language at the end of the final user message).
public sealed class AnswerPromptBuilderTests
{
    private static readonly MedicalChunk[] _chunks =
    [
        new() { BookTitle = "Endocrinology", ChapterTitle = "Thyroid", SectionTitle = "Graves", Text = "Graves text." },
        new() { BookTitle = "Harrison", ChapterTitle = "Immunology", SectionTitle = "TSI", Text = "TSI text." },
    ];

    private static string AllText(ChatHistory history) => string.Join("\n", history.Select(m => m.Content));

    private static int Count(string haystack, string needle)
    {
        var count = 0;
        for (var i = haystack.IndexOf(needle, StringComparison.Ordinal); i >= 0;
             i = haystack.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }
        return count;
    }

    public static TheoryData<string> Profiles => new() { "default", "global", "ddx" };

    private static PromptProfile Profile(string name) => name switch
    {
        "global" => PromptProfiles.GlobalSearch,
        "ddx" => PromptProfiles.DifferentialDiagnosis,
        _ => PromptProfiles.Default,
    };

    [Theory]
    [MemberData(nameof(Profiles))]
    public void CitationInstruction_AppearsOnce_InFinalUserMessage_AfterExcerpts(string profile)
    {
        var history = AnswerPromptBuilder.BuildBookAnswer(Profile(profile), "What is Graves' disease?", _chunks, null);

        Assert.Equal(1, Count(AllText(history), AnswerPromptBuilder.CitationInstruction));
        var last = history[^1];
        Assert.Equal(AuthorRole.User, last.Role);
        Assert.True(last.Content!.IndexOf(AnswerPromptBuilder.CitationInstruction, StringComparison.Ordinal)
                    > last.Content.IndexOf("TSI text.", StringComparison.Ordinal));
        Assert.DoesNotContain("square brackets", history[0].Content);
    }

    [Theory]
    [MemberData(nameof(Profiles))]
    public void PersonaAndProseRule_AppearOnce(string profile)
    {
        var text = AllText(AnswerPromptBuilder.BuildBookAnswer(Profile(profile), "q", _chunks, null));

        Assert.Equal(1, Count(text, AnswerPromptBuilder.Persona));
        Assert.Equal(1, Count(text, AnswerPromptBuilder.ProseRule));
    }

    [Fact]
    public void LanguageInstruction_AppearsOnce_AndMatchesScript()
    {
        var bg = AllText(AnswerPromptBuilder.BuildBookAnswer(PromptProfiles.Default, "Какво е Грейвс?", _chunks, null));
        var en = AllText(AnswerPromptBuilder.BuildBookAnswer(PromptProfiles.Default, "What is Graves?", _chunks, null));

        Assert.Equal(1, Count(bg, "ВАЖНО"));
        Assert.DoesNotContain("Respond entirely in English", bg);
        Assert.Equal(1, Count(en, "Respond entirely in English"));
        Assert.DoesNotContain("same language", en);
    }

    // Ollama drops the START of an over-long prompt, so the language line must sit after the excerpts
    // (next to the citation instruction) to survive truncation.
    [Fact]
    public void LanguageInstruction_SitsAfterExcerpts_BeforeCitationInstruction()
    {
        var last = AnswerPromptBuilder.BuildBookAnswer(PromptProfiles.Default, "Какво е Грейвс?", _chunks, null)[^1].Content!;

        var language = last.IndexOf("ВАЖНО", StringComparison.Ordinal);
        Assert.True(language > last.IndexOf("TSI text.", StringComparison.Ordinal));
        Assert.True(language < last.IndexOf(AnswerPromptBuilder.CitationInstruction, StringComparison.Ordinal));
    }

    [Fact]
    public void ExecutionSettings_RequestTheConfiguredContextWindow()
    {
        Assert.Equal(PromptConstants.ContextWindowTokens, AnswerPromptBuilder.ExecutionSettings.ExtensionData!["num_ctx"]);
        Assert.True(PromptConstants.ContextWindowTokens > 4096);
    }

    [Fact]
    public void Excerpts_CarryProvenance_AndNoSearchedBooksList()
    {
        var text = AllText(AnswerPromptBuilder.BuildBookAnswer(PromptProfiles.Default, "q", _chunks, null));

        Assert.DoesNotContain("Sources searched", text);
        Assert.Contains("[1] (Endocrinology — Thyroid › Graves)", text);
        Assert.Contains("[2] (Harrison — Immunology › TSI)", text);
    }

    [Fact]
    public void DdxProfile_AddsItsDeltaOnce()
    {
        var text = AllText(AnswerPromptBuilder.BuildBookAnswer(PromptProfiles.DifferentialDiagnosis, "q", _chunks, null));

        Assert.Equal(1, Count(text, "likeliest first"));
        Assert.DoesNotContain("likeliest first",
            AllText(AnswerPromptBuilder.BuildBookAnswer(PromptProfiles.Default, "q", _chunks, null)));
    }

    [Fact]
    public void WebSystemPrompt_ReusesCoreOnce()
    {
        var prompt = AnswerPromptBuilder.WebSystemPrompt("Answer using only the web excerpts provided.");

        Assert.Equal(1, Count(prompt, AnswerPromptBuilder.Persona));
        Assert.Equal(1, Count(prompt, AnswerPromptBuilder.ProseRule));
        Assert.Equal(1, Count(prompt, "<web_source>"));
    }

    [Fact]
    public void History_StripsMarkersFromAssistantTurns_Only()
    {
        var result = AnswerPromptBuilder.PrepareHistory(
        [
            new("user", "Is [1] a marker here?"),
            new("assistant", "Graves' disease is autoimmune [1][3]. It is common [2, 4]."),
        ], budgetChars: 10_000);

        Assert.Equal("Is [1] a marker here?", result[0].Content);
        Assert.Equal("Graves' disease is autoimmune. It is common.", result[1].Content);
    }

    [Fact]
    public void History_OverBudget_DropsOldestWholePairs()
    {
        var old = new string('a', 400);
        var result = AnswerPromptBuilder.PrepareHistory(
        [
            new("user", old), new("assistant", old),
            new("user", "recent q"), new("assistant", "recent a"),
        ], budgetChars: 100);

        Assert.Equal(["recent q", "recent a"], result.Select(m => m.Content));
    }

    [Fact]
    public void History_LatestPairKept_EvenWhenItAloneExceedsBudget()
    {
        var big = new string('b', 500);
        var result = AnswerPromptBuilder.PrepareHistory(
        [
            new("user", "old"), new("assistant", "old"),
            new("user", big), new("assistant", big),
        ], budgetChars: 100);

        Assert.Equal([big, big], result.Select(m => m.Content));
    }

    [Fact]
    public void PromptTokens_PrefersReportedUsage_ElseEstimates()
    {
        var prompt = new ChatHistory();
        prompt.AddUserMessage(new string('x', 400));

        var reported = new ChatMessageContent(AuthorRole.Assistant, "a",
            metadata: new Dictionary<string, object?> { ["Usage"] = new UsageDetails { InputTokenCount = 321 } });
        var bare = new ChatMessageContent(AuthorRole.Assistant, "a");

        Assert.Equal((321L, false), PromptMetrics.PromptTokens(prompt, reported));
        Assert.Equal((100L, true), PromptMetrics.PromptTokens(prompt, bare));
    }

    private static MedicalChunk BigChunk(int i) => new()
    {
        BookTitle = $"Book {i}", ChapterTitle = "Ch", SectionTitle = "Sec", Text = new string('ж', 3_000),
    };

    private static ChatMessageDto[] BigHistory() =>
        [new("user", new string('q', 1_500)), new("assistant", new string('a', 1_500))];

    [Fact]
    public void Budget_UnderLimit_KeepsEverything()
    {
        var (chunks, history) = AnswerPromptBuilder.FitToBudget(PromptProfiles.Default, "q", _chunks,
            [new("user", "u"), new("assistant", "a")], budgetTokens: 10_000);

        Assert.Equal(_chunks, chunks);
        Assert.Equal(2, history.Count);
    }

    [Fact]
    public void Budget_TrimsHistoryBeforeExcerpts()
    {
        var five = Enumerable.Range(1, 5).Select(BigChunk).ToArray();
        // At 2 chars/token: 5 × 3,000 chars of excerpts ≈ 7,500 tokens + ~1,250 of core; history adds ~1,500.
        var (chunks, history) = AnswerPromptBuilder.FitToBudget(PromptProfiles.Default, "q", five, BigHistory(), budgetTokens: 9_000);

        Assert.Empty(history);
        Assert.Equal(5, chunks.Count);
    }

    [Fact]
    public void Budget_DropsLowestRankedExcerpts_KeepsAtLeastOne()
    {
        var five = Enumerable.Range(1, 5).Select(BigChunk).ToArray();

        var (fitted, _) = AnswerPromptBuilder.FitToBudget(PromptProfiles.Default, "q", five, null, budgetTokens: 3_000);
        var (minimum, _) = AnswerPromptBuilder.FitToBudget(PromptProfiles.Default, "q", five, null, budgetTokens: 10);

        Assert.Equal(five.Take(fitted.Count), fitted);
        Assert.InRange(fitted.Count, 1, 4);
        Assert.Equal([five[0]], minimum);
    }

    [Fact]
    public void Budget_Default_IsWindowMinusAnswerReserve()
    {
        Assert.Equal(PromptConstants.ContextWindowTokens - PromptConstants.AnswerReserveTokens,
            AnswerPromptBuilder.PromptBudgetTokens);
    }

    [Fact]
    public void History_IsSentBetweenSystemAndFinalUserMessage()
    {
        var history = AnswerPromptBuilder.BuildBookAnswer(PromptProfiles.Default, "follow-up", _chunks,
            [new("user", "first q"), new("assistant", "first a [1].")]);

        Assert.Equal(4, history.Count);
        Assert.Equal(AuthorRole.System, history[0].Role);
        Assert.Equal("first q", history[1].Content);
        Assert.Equal("first a.", history[2].Content);
    }
}
