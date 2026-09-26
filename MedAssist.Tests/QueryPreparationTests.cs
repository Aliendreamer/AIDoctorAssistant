using MedAssist.AI.Plugins;
using MedAssist.Shared.Constants;
using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;

namespace MedAssist.Tests;

// tune-query-pipeline: questions become a Bulgarian search query (the corpus language) via one fast
// LLM call, only when needed; reranking and the Latin-term guard are language-aware.
public sealed class QueryPreparationTests
{
    private static readonly ChatMessageDto[] _history =
    [
        new("user", "Лечение на остър гастроентерит при деца"),
        new("assistant", "Лечението се състои в рехидратация [1]."),
    ];

    [Fact]
    public void BulgarianFirstQuestion_NeedsNoPreparation()
    {
        Assert.False(QueryPreparation.NeedsPreparation("Фебрилни гърчове при деца", null));
    }

    [Fact]
    public void EnglishQuestion_NeedsPreparation()
    {
        Assert.True(QueryPreparation.NeedsPreparation("What is Kawasaki disease?", null));
    }

    [Fact]
    public void ShortFollowUp_NeedsPreparation()
    {
        Assert.True(QueryPreparation.NeedsPreparation("А при кърмачета?", _history));
    }

    [Fact]
    public void RepeatedQuestion_IsNotAFollowUp()
    {
        Assert.False(QueryPreparation.NeedsPreparation(_history[0].Content, _history));
    }

    [Fact]
    public void Prompt_IncludesPreviousQuestion_OnlyForFollowUps()
    {
        var followUp = string.Join("\n", QueryPreparation.BuildPrompt("А при кърмачета?", _history).Select(m => m.Content));
        var fresh = string.Join("\n", QueryPreparation.BuildPrompt("What is Kawasaki disease?", null).Select(m => m.Content));

        Assert.Contains(_history[0].Content, followUp);
        Assert.Contains("What is Kawasaki disease?", fresh);
        Assert.DoesNotContain("Previous question", fresh);
    }

    [Fact]
    public void AnswerSettings_DisableThinking()
    {
        Assert.Equal(false, AnswerPromptBuilder.ExecutionSettings.ExtensionData!["think"]);
    }

    [Fact]
    public void PrepSettings_AreFastAndShareTheContextWindow()
    {
        var data = QueryPreparation.Settings.ExtensionData!;

        Assert.Equal(false, data["think"]);
        Assert.Equal(PromptConstants.QueryPrepMaxTokens, data["num_predict"]);
        Assert.Equal(0.0, data["temperature"]);
        // A different num_ctx than the answer calls would make Ollama reload the model.
        Assert.Equal(AnswerPromptBuilder.ExecutionSettings.ExtensionData!["num_ctx"], data["num_ctx"]);
    }

    [Fact]
    public async Task Rerank_ScoresEachChunkAgainstTheQueryInItsLanguage()
    {
        var bg = new MedicalChunk { Language = LanguageCodes.Bulgarian, Text = "бг" };
        var en = new MedicalChunk { Language = LanguageCodes.English, Text = "en" };
        var reranker = new RecordingReranker();

        var scored = await QueryPreparation.RerankByLanguageAsync(
            reranker, "Болест на Кавазаки", "What is Kawasaki disease?", [bg, en], CancellationToken.None);

        Assert.Equal([bg], reranker.Calls["Болест на Кавазаки"]);
        Assert.Equal([en], reranker.Calls["What is Kawasaki disease?"]);
        Assert.Equal(2, scored.Count);
        Assert.True(scored[0].Score >= scored[1].Score);
    }

    [Fact]
    public async Task Rerank_SingleCallWhenAllChunksBulgarian()
    {
        var reranker = new RecordingReranker();

        await QueryPreparation.RerankByLanguageAsync(reranker, "заявка", "query",
            [new MedicalChunk { Language = LanguageCodes.Bulgarian }], CancellationToken.None);

        Assert.Single(reranker.Calls);
    }

    [Fact]
    public void LatinTerms_FromTranslatedQuery_KeepOnlyEponyms()
    {
        Assert.Empty(QueryPreparation.ExtractLatinTerms("Лечение на остър среден отит при деца"));
        Assert.Equal(["chiari"], QueryPreparation.ExtractLatinTerms("Малформация на Chiari тип 2"));
    }

    private sealed class RecordingReranker : ICrossEncoderReranker
    {
        public Dictionary<string, IReadOnlyList<MedicalChunk>> Calls { get; } = [];

        public Task<IReadOnlyList<ScoredChunk>> RerankAsync(
            string query, IReadOnlyList<MedicalChunk> candidates, CancellationToken cancellationToken = default)
        {
            Calls[query] = candidates;
            IReadOnlyList<ScoredChunk> scored = candidates.Select((c, i) => new ScoredChunk(c, Calls.Count * 10 - i)).ToList();
            return Task.FromResult(scored);
        }
    }
}
