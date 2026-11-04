using MedAssist.AI.Evaluation;
using MedAssist.Shared.Constants;
using MedAssist.Shared.Models;
using MedAssist.Shared.Models.Evaluation;
using MedAssist.Web.Options;
using Microsoft.Extensions.Options;

namespace MedAssist.Web.Services.Evaluation;

/// <summary>Resolves what an admin chose against production defaults into the run's recorded configuration.</summary>
public sealed class EvaluationSnapshotFactory(
    IConfiguration configuration,
    IOptions<RagOptions> ragOptions,
    IOptions<ModelsOptions> modelsOptions,
    BookCatalogService catalog,
    GoldenSet goldenSet)
{
    private const string _embedder = "multilingual-e5-large";

    public string DefaultModel => configuration["AI:Ollama:ModelName"] ?? string.Empty;

    public async Task<EvalRunSnapshot> CreateAsync(EvalRunRequest request, CancellationToken cancellationToken)
    {
        var books = await catalog.GetAllBooksAsync(cancellationToken);
        var overrides = request.Overrides with { BookOnly = true };
        var rag = ragOptions.Value;

        return new EvalRunSnapshot
        {
            Model = overrides.ModelId ?? DefaultModel,
            JudgeModel = request.RunJudge ? request.JudgeModel ?? DefaultModel : null,
            ContextWindowTokens = overrides.ContextWindowTokens ?? PromptConstants.ContextWindowTokens,
            AnswerReserveTokens = overrides.AnswerReserveTokens ?? PromptConstants.AnswerReserveTokens,
            MaxExcerpts = overrides.MaxExcerpts ?? PromptConstants.MaxExcerpts,
            InitialTopK = overrides.InitialTopK ?? PromptConstants.InitialTopK,
            HistoryCharBudget = overrides.HistoryCharBudget ?? PromptConstants.HistoryCharBudget,
            Temperature = overrides.Temperature,
            BookOnly = overrides.BookOnly,
            ConfidenceThreshold = rag.ConfidenceThreshold,
            MinAnswerScore = rag.MinAnswerScore,
            MinRetryScore = rag.MinRetryScore,
            MaxIterations = rag.MaxIterations,
            PromptFingerprint = PromptFingerprint.Compute(),
            Embedder = _embedder,
            Reranker = Path.GetFileName(modelsOptions.Value.RerankerPath),
            IndexedBooks = books.Count(b => b.TotalChunks > 0),
            IndexedChunks = books.Sum(b => b.TotalChunks),
            AppVersion = typeof(EvaluationSnapshotFactory).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            GoldenSetHash = goldenSet.Hash,
            ReviewedCases = goldenSet.ReviewedCount,
            TotalCases = goldenSet.Cases.Count,
            Overrides = overrides,
            RunJudge = request.RunJudge,
        };
    }
}
