namespace MedAssist.Shared.Models.Evaluation;

public enum EvalVerdict
{
    /// <summary>No earlier run to compare with.</summary>
    NoBaseline,
    NoRegression,
    Regressed,

    /// <summary>Nothing got worse, but too much evidence is missing to say nothing regressed.</summary>
    Inconclusive,
}

public enum MetricChange
{
    Unchanged,
    Improved,
    Worse,

    /// <summary>The baseline measured this and this run did not.</summary>
    NotMeasured,
}

public enum MetricUnit
{
    Rate,
    Count,
    Seconds,
    Tokens,
}

public sealed record MetricDelta(
    string Group,
    string Label,
    string Description,
    MetricUnit Unit,
    double? Current,
    double? Baseline,
    MetricChange Change,
    bool Gates);

public sealed record SettingChange(string Label, string Baseline, string Current);

public sealed record CaseChange(string CaseId, string Question, bool NowPasses, IReadOnlyList<string> Reasons);

public sealed record EvalComparison
{
    public EvalVerdict Verdict { get; init; }
    public IReadOnlyList<MetricDelta> Metrics { get; init; } = [];
    public IReadOnlyList<SettingChange> Settings { get; init; } = [];
    public IReadOnlyList<CaseChange> Cases { get; init; } = [];

    /// <summary>The reference answers differ, so score movement may come from the references.</summary>
    public bool GoldenSetChanged { get; init; }

    /// <summary>The judge differs between the runs, so judged scores used different instruments.</summary>
    public bool JudgeChanged { get; init; }
}
