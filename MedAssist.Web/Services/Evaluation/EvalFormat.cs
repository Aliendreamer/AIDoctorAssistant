using System.Globalization;
using MedAssist.Shared.Models.Evaluation;

namespace MedAssist.Web.Services.Evaluation;

/// <summary>Display text for the evaluation pages.</summary>
public static class EvalFormat
{
    private const string _none = "—";

    public static string Value(MetricUnit unit, double? value) => value is not { } v
        ? _none
        : unit switch
        {
            MetricUnit.Rate => v.ToString("P0", CultureInfo.InvariantCulture).Replace(" ", string.Empty, StringComparison.Ordinal),
            MetricUnit.Seconds => $"{v.ToString("0.0", CultureInfo.InvariantCulture)} s",
            MetricUnit.Tokens => v.ToString("N0", CultureInfo.InvariantCulture),
            _ => v.ToString("0", CultureInfo.InvariantCulture),
        };

    public static string Percent(double? value) => Value(MetricUnit.Rate, value);

    public static string Seconds(double? value) => Value(MetricUnit.Seconds, value);

    public static string Delta(MetricDelta metric)
    {
        if (metric.Change == MetricChange.NotMeasured)
        {
            return "not measured";
        }

        if (metric.Current is not { } now || metric.Baseline is not { } before)
        {
            return string.Empty;
        }

        var delta = now - before;
        var sign = delta > 0 ? "+" : string.Empty;
        return metric.Unit == MetricUnit.Rate
            ? $"{sign}{(delta * 100).ToString("0", CultureInfo.InvariantCulture)} pts"
            : $"{sign}{Value(metric.Unit, delta)}";
    }

    public static string VerdictLabel(EvalVerdict verdict) => verdict switch
    {
        EvalVerdict.Regressed => "Regressed",
        EvalVerdict.NoRegression => "No regression",
        EvalVerdict.Inconclusive => "Inconclusive",
        _ => "No baseline",
    };

    public static string VerdictClass(EvalVerdict verdict) => verdict switch
    {
        EvalVerdict.Regressed => "is-failed",
        EvalVerdict.NoRegression => "is-ok",
        EvalVerdict.Inconclusive => "is-failed",
        _ => "is-working",
    };

    public static string StatusClass(EvalRunStatus status) => status switch
    {
        EvalRunStatus.Completed => "is-ok",
        EvalRunStatus.Failed => "is-failed",
        EvalRunStatus.Cancelled => "is-failed",
        _ => "is-working",
    };

    public static string ChangeClass(MetricChange change) => change switch
    {
        MetricChange.Improved => "eval-better",
        MetricChange.Worse or MetricChange.NotMeasured => "eval-worse",
        _ => "eval-same",
    };

    public static bool IsActive(EvalRunStatus status) => status is EvalRunStatus.Queued or EvalRunStatus.Running;

    public static int ProgressPercent(int done, int total) => total > 0 ? done * 100 / total : 0;
}
