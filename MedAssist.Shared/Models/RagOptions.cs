namespace MedAssist.Shared.Models;

public sealed class RagOptions
{
    // Raw cross-encoder logit; above this means the top result is confident enough to stop iterating.
    // ms-marco models output positive logits for relevant pairs; 0.0 is the decision boundary.
    public float ConfidenceThreshold { get; init; } = 0.0f;

    // Refinement passes after the initial search. Each pass widens the search space.
    // Actual iterations = Min(MaxIterations, 5).
    public int MaxIterations { get; init; } = 2;
}
