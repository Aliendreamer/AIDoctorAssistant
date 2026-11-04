using MedAssist.AI.Evaluation;
using MedAssist.Shared.Constants;
using MedAssist.Shared.Models;
using MedAssist.Shared.Models.Evaluation;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace MedAssist.Web.Services.Evaluation;

/// <summary>The system under evaluation.</summary>
public interface IEvaluationTarget
{
    Task<QueryResult> AskAsync(QueryRequest request, string userId, CancellationToken cancellationToken);
}

/// <summary>Sends golden questions through the same <see cref="QueryService"/> doctors use.</summary>
public sealed class QueryServiceEvaluationTarget(QueryService queryService) : IEvaluationTarget
{
    public Task<QueryResult> AskAsync(QueryRequest request, string userId, CancellationToken cancellationToken) =>
        queryService.ExecuteAsync(request, userId, cancellationToken);
}

public interface IEvalJudgeFactory
{
    IEvalJudge Create(EvalRunSnapshot snapshot);
}

public sealed class OllamaEvalJudgeFactory(Kernel kernel) : IEvalJudgeFactory
{
    public IEvalJudge Create(EvalRunSnapshot snapshot) => new OllamaEvalJudge(
        kernel.GetRequiredService<IChatCompletionService>(),
        snapshot.JudgeModel,
        snapshot.ContextWindowTokens + EvaluationConstants.JudgeContextHeadroomTokens);
}
