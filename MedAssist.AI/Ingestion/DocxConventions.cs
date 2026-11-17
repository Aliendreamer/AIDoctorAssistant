using System.Globalization;
using System.Text.RegularExpressions;
using MedAssist.Shared.Models;

namespace MedAssist.AI.Ingestion;

/// <summary>
/// The conventions of the Bulgarian Nelson translation's Word parts (change ingest-docx-and-page-citations): the
/// translator's page lines, per-file banners and meta-notes, and part ordering. Every pattern was derived from a
/// survey of all 112 parts; the unit tests hold one case per variant found.
/// </summary>
public static partial class DocxConventions
{
    /// <summary>Paragraphs at least this long are content, never a page line.</summary>
    private const int _maxPageLineLength = 200;

    // "PDF стр. 426 · …", "Оригинал: PDF 2635 · …", "PDF страница 2557 · …", "PDF 2631 и 2633 · …",
    // "Оригинал: PDF стр. 2671, …", and figure reconstructions "Nelson Textbook of Pediatrics, 20-о изд. · PDF 2738 · …".
    // The rest after the separator may state the printed page.
    [GeneratedRegex(
        @"^(?:(?:Оригинал|Original)\s*:\s*|Nelson Textbook of Pediatrics,[^·]*·\s*)?PDF\s*(?:стр\.?|страница|страници|p\.)?\s*(?<pdf>\d+)(?:\s*(?:[–-]|и)\s*\d+)?\s*(?:[·•|,.]\s*(?<rest>.*))?$",
        RegexOptions.IgnoreCase | RegexOptions.Singleline, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PageLineRegex();

    // "печатна стр. 268", "отпечатана стр. 1807.e2", "оригинална печатна страница 1899", "печатна 1836".
    [GeneratedRegex(@"(?:отпечатан\w*|печатн\w*)\s*(?:стр\.?|страница)?\s*(?<p>\d+(?:\.e\d+)?)",
        RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PrintedPageRegex();

    // "PDF страница 2600 · 1780.e1": the printed page right after the separator, without a label.
    [GeneratedRegex(@"^(?<p>\d+(?:\.e\d+)?)(?=\s|$|[·;,.)])", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex BarePrintedPageRegex();

    // A short paragraph that starts like a page line; used to report variants the parser missed.
    [GeneratedRegex(@"^(?:(?:Оригинал|Original)\s*:\s*)?PDF\b", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PageLikeRegex();

    // Banners that never occur in medical text, removed anywhere in a part: the Part banner with its Roman numeral,
    // a line opening with the book's name, and the translation-source line.
    [GeneratedRegex(
        @"^(?:Част\s+[IVXLC]+\b\s*[·—–.-]|Nelson Textbook of Pediatrics\b|Последователен\b.*превод)",
        RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex StrongBannerRegex();

    // Banners removed only before a part's content starts, where they open the file; later, the same shapes can be
    // content: "Раздел VI · Хранене", "Част 1 — PDF страници 426–430", file summaries "Глава 44 · …".
    [GeneratedRegex(
        @"^(?:Раздел\s+[IVXLC]+\s*[·—–-]|Част\s+\d+\s*[·—–-]|Глава\s+\d+\s*·)",
        RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex OpeningBannerRegex();

    [GeneratedRegex(
        @"^(?:Контролна граница|Бележка за (?:продължението|продължение|границата|печатния оригинал)|Забележка за верността|\[Библиографията)",
        RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex MetaNoteRegex();

    [GeneratedRegex(@"^\s*(?<prefix>\d+(?:\.\d+)?)-Nelson_(?<part>[IVXLC]+)_", RegexOptions.IgnoreCase, matchTimeoutMilliseconds: 1000)]
    private static partial Regex NelsonPartNameRegex();

    [GeneratedRegex(@"PDF(?<start>\d+)-(?<end>\d+)", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FileNamePdfRangeRegex();

    /// <summary>Parses a translator page line into the PDF page and, when stated, the printed page.</summary>
    public static bool TryParsePageLine(string text, out PageRef page)
    {
        page = default;
        if (text.Length >= _maxPageLineLength)
        {
            return false;
        }

        var match = PageLineRegex().Match(text);
        if (!match.Success)
        {
            return false;
        }

        var rest = match.Groups["rest"].Value;
        var printed = PrintedPageRegex().Match(rest);
        if (!printed.Success)
        {
            printed = BarePrintedPageRegex().Match(rest);
        }

        page = new PageRef(int.Parse(match.Groups["pdf"].Value, CultureInfo.InvariantCulture), printed.Success ? printed.Groups["p"].Value : null);
        return true;
    }

    /// <summary>A short paragraph that looks like a page line but did not parse as one.</summary>
    public static bool IsUnrecognisedPageLine(string text) =>
        text.Length < _maxPageLineLength && PageLikeRegex().IsMatch(text) && !TryParsePageLine(text, out _);

    /// <summary>
    /// A translator banner (Part/Section line, translation-source line, file summary). Opening banners count only at
    /// the start of a part, before its content.
    /// </summary>
    public static bool IsBanner(string text, bool atStartOfPart) =>
        StrongBannerRegex().IsMatch(text) || (atStartOfPart && OpeningBannerRegex().IsMatch(text));

    /// <summary>A translator meta-note about the translation itself, not medical content.</summary>
    public static bool IsMetaNote(string text) => MetaNoteRegex().IsMatch(text);

    /// <summary>The first PDF page in a part's file name (<c>…_PDF2636-2640.docx</c>), if any.</summary>
    public static int? FileNamePdfStart(string fileName) =>
        FileNamePdfRangeRegex().Match(fileName) is { Success: true } m ? int.Parse(m.Groups["start"].Value, CultureInfo.InvariantCulture) : null;

    /// <summary>The last PDF page in a part's file name (<c>…_PDF2636-2640.docx</c>), if any.</summary>
    public static int? FileNamePdfEnd(string fileName) =>
        FileNamePdfRangeRegex().Match(fileName) is { Success: true } m ? int.Parse(m.Groups["end"].Value, CultureInfo.InvariantCulture) : null;

    /// <summary>
    /// Reading order: <c>&lt;prefix&gt;-Nelson_&lt;Part&gt;_…</c> names by Part numeral, then numeric prefix (prefixes restart in
    /// each Part); any other name after them, by its first PDF page, then by name.
    /// </summary>
    public static IReadOnlyList<string> OrderParts(IEnumerable<string> fileNames) =>
        fileNames
            .Select(name => (Name: name, Key: SortKey(name)))
            .OrderBy(x => x.Key.Group)
            .ThenBy(x => x.Key.Major)
            .ThenBy(x => x.Key.Minor)
            .ThenBy(x => x.Name, StringComparer.Ordinal)
            .Select(x => x.Name)
            .ToList();

    private static (int Group, int Major, decimal Minor) SortKey(string name)
    {
        var match = NelsonPartNameRegex().Match(name);
        if (match.Success)
        {
            return (0, Roman(match.Groups["part"].Value), decimal.Parse(match.Groups["prefix"].Value, CultureInfo.InvariantCulture));
        }

        return (1, FileNamePdfStart(name) ?? int.MaxValue, 0m);
    }

    private static int Roman(string numeral)
    {
        static int Value(char c) => char.ToUpperInvariant(c) switch
        {
            'I' => 1, 'V' => 5, 'X' => 10, 'L' => 50, 'C' => 100, _ => 0,
        };

        var total = 0;
        for (var i = 0; i < numeral.Length; i++)
        {
            var value = Value(numeral[i]);
            total += i + 1 < numeral.Length && Value(numeral[i + 1]) > value ? -value : value;
        }

        return total;
    }
}
