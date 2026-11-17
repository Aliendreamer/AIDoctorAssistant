using System.Text.RegularExpressions;

namespace MedAssist.Shared.Models;

/// <summary>
/// A page position in a source book: the 1-based PDF page and, when the source states it, the printed page label.
/// Printed labels are text because some books number online-only pages like <c>1804.e1</c>. <paramref name="UntilPdf"/>
/// marks an uncertain stretch: the source skipped pages, so the text after this position may run on to that PDF page.
/// </summary>
public readonly record struct PageRef(int PdfPage, string? PrintedPage, int? UntilPdf = null)
{
    public bool IsUncertain => UntilPdf > PdfPage;
}

/// <summary>
/// The pages a chunk spans. <c>0</c> PDF pages and <c>null</c> printed pages mean unknown, which is what chunks
/// indexed before page tracking hold.
/// </summary>
public sealed record PageRange(int PdfStart, int PdfEnd, string? PrintedStart, string? PrintedEnd)
{
    public static readonly PageRange Unknown = new(0, 0, null, null);
}

/// <summary>
/// The page marker written into book markdown wherever the page changes (change ingest-docx-and-page-citations).
/// Both converters write it and only the chunker reads it; it is an HTML comment so it can't occur in OCR text and
/// stays invisible if the markdown is rendered.
/// </summary>
public static partial class PageMarker
{
    [GeneratedRegex(@"^\s*<!-- page pdf=(?<pdf>\d+)(?: printed=(?<printed>[\w.]+))?(?: until=(?<until>\d+))? -->\s*$")]
    private static partial Regex MarkerRegex();

    [GeneratedRegex(@"<!-- page pdf=(?<pdf>\d+)(?: printed=(?<printed>[\w.]+))?(?: until=(?<until>\d+))? -->")]
    private static partial Regex InlineMarkerRegex();

    // A marker plus the whitespace before it: removes a marker line with its line break, and an inline marker
    // (left mid-line by sentence joining) with the space before it.
    [GeneratedRegex(@"[ \t\r\n]*<!-- page pdf=\d+(?: printed=[\w.]+)?(?: until=\d+)? -->")]
    private static partial Regex StripRegex();

    public static string Format(PageRef page) =>
        $"<!-- page pdf={page.PdfPage}"
        + (page.PrintedPage is null ? string.Empty : $" printed={page.PrintedPage}")
        + (page.UntilPdf is { } until ? $" until={until}" : string.Empty)
        + " -->";

    public static bool TryParse(string line, out PageRef page)
    {
        var match = MarkerRegex().Match(line);
        if (!match.Success)
        {
            page = default;
            return false;
        }

        page = FromMatch(match);
        return true;
    }

    /// <summary>Every marker in the text, in order, with the index where it starts. Markers may be mid-line.</summary>
    public static IEnumerable<(PageRef Page, int Index)> FindAll(string text)
    {
        foreach (Match match in InlineMarkerRegex().Matches(text))
        {
            yield return (FromMatch(match), match.Index);
        }
    }

    private static PageRef FromMatch(Match match)
    {
        var printed = match.Groups["printed"];
        var until = match.Groups["until"];
        return new PageRef(
            int.Parse(match.Groups["pdf"].Value),
            printed.Success ? printed.Value : null,
            until.Success ? int.Parse(until.Value) : null);
    }

    /// <summary>Removes every marker, whether on its own line or inline.</summary>
    public static string Strip(string text) => StripRegex().Replace(text, string.Empty);
}

/// <summary>
/// The page text a citation shows: the printed page when known, otherwise the PDF page labelled as such, otherwise
/// nothing. <c>0</c> is never shown as a page.
/// </summary>
public static class PageLabel
{
    public static string Format(int? pdfStart, int? pdfEnd, string? printedStart, string? printedEnd)
    {
        if (!string.IsNullOrWhiteSpace(printedStart))
        {
            return string.IsNullOrWhiteSpace(printedEnd) || printedEnd == printedStart
                ? $"p. {printedStart}"
                : $"pp. {printedStart}–{printedEnd}";
        }

        if (pdfStart is > 0)
        {
            return pdfEnd is not { } end || end <= pdfStart
                ? $"PDF p. {pdfStart}"
                : $"PDF pp. {pdfStart}–{end}";
        }

        return string.Empty;
    }

    public static string Format(SourceCitation citation) =>
        Format(citation.PageStart, citation.PageEnd, citation.PrintedPageStart, citation.PrintedPageEnd);
}
