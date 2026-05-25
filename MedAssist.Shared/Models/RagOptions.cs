namespace MedAssist.Shared.Models;

public sealed class RagOptions
{
    // Raw cross-encoder logit; above this means the top result is confident enough to stop iterating.
    // ms-marco models output positive logits for relevant pairs; 0.0 is the decision boundary.
    public float ConfidenceThreshold { get; init; } = 0.0f;

    // Minimum score the top-ranked chunk must reach before we generate an answer.
    // If nothing exceeds this after all retries, we return "insufficient information" instead
    // of synthesising from low-relevance content. Tune alongside ConfidenceThreshold.
    public float MinAnswerScore { get; init; } = 1.5f;

    // If the initial retrieval scores below this, skip the retry loop entirely and signal
    // the caller to fall back to web search (CRAG "INCORRECT" branch).
    // Must be ≤ MinAnswerScore — setting it higher than MinAnswerScore would make retries unreachable.
    public float MinRetryScore { get; init; } = 1.0f;

    // Refinement passes after the initial search. Each pass widens the search space.
    // Actual iterations = Min(MaxIterations, 5).
    public int MaxIterations { get; init; } = 2;
}
