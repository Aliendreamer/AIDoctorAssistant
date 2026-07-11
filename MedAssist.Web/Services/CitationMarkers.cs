using System.Text.RegularExpressions;

namespace MedAssist.Web.Services;

/// <summary>
/// One piece of a rendered answer: plain <see cref="Text"/> when <see cref="CitationNumber"/> is
/// null, or a citation marker referring to the 1-based source index when it is set.
/// </summary>
public readonly record struct AnswerSegment(string Text, int? CitationNumber);

/// <summary>
/// Splits an answer into text and citation-marker segments so the UI can render model-emitted
/// <c>[n]</c> references as superscripts tied to the numbered source list. Range-guarded: a marker
/// whose number is not a real source for that answer is left as literal text, never a broken or
/// mis-pointing reference (see change <c>cited-answer-markers</c>).
/// </summary>
public static partial class CitationMarkers
{
    // A bracket group of one or more comma-separated numbers: [2], [1,3], [1, 3].
    [GeneratedRegex(@"\[\s*\d+(?:\s*,\s*\d+)*\s*\]")]
    private static partial Regex MarkerGroupRegex();

    public static IReadOnlyList<AnswerSegment> Parse(string content, int sourceCount)
    {
        if (string.IsNullOrEmpty(content))
        {
            return [];
        }

        // No sources to map onto — markers can't be linkified, so render the text verbatim.
        if (sourceCount <= 0)
        {
            return [new AnswerSegment(content, null)];
        }

        var segments = new List<AnswerSegment>();
        var pos = 0;

        foreach (Match match in MarkerGroupRegex().Matches(content))
        {
            if (!TryParseInRange(match.Value, sourceCount, out var numbers))
            {
                // Leave a hallucinated / out-of-range group inside the surrounding plain text.
                continue;
            }

            if (match.Index > pos)
            {
                segments.Add(new AnswerSegment(content[pos..match.Index], null));
            }

            foreach (var n in numbers)
            {
                segments.Add(new AnswerSegment(string.Empty, n));
            }

            pos = match.Index + match.Length;
        }

        if (pos < content.Length)
        {
            segments.Add(new AnswerSegment(content[pos..], null));
        }

        return segments;
    }

    // A group is accepted only when every number parses and is a real 1..sourceCount source.
    private static bool TryParseInRange(string group, int sourceCount, out IReadOnlyList<int> numbers)
    {
        var parts = group.Trim('[', ']')
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var parsed = new List<int>(parts.Length);
        foreach (var part in parts)
        {
            if (!int.TryParse(part, out var n) || n < 1 || n > sourceCount)
            {
                numbers = [];
                return false;
            }
            parsed.Add(n);
        }

        numbers = parsed;
        return parsed.Count > 0;
    }
}
