using System.Text.RegularExpressions;
using MedAssist.Shared.Constants;

namespace MedAssist.AI.Plugins;

/// <summary>
/// Recognises reference lists and book indexes. They rank well on a disease name but give the model nothing
/// to reason from, and they take excerpt slots and prompt space from real text. Section titles cannot be
/// trusted for this: OCR made "ПОЛЗВАНА ЛИТЕРАТУРА" the parent heading of whole chapters in one book.
/// </summary>
internal static partial class ReferenceMatter
{
    [GeneratedRegex(@"(?<!\d)(?:1[89]\d\d|20[0-2]\d)(?!\d)")]
    private static partial Regex YearRegex();

    [GeneratedRegex(@"^\s*(?:[-*•>]\s*)*(?:\*\*)?\s*\d{1,3}\s*[.)]\s+\S")]
    private static partial Regex NumberedRegex();

    [GeneratedRegex(
        @"et al|Eds?\.|\b[EeЕе]d\.?\s*\d|стр\.|\bpp?\.\s*\d|\bс\.\s*\d|\bкн\.?\b|\bJ\.|Journal|Press|издат|София|London|" +
        @"Boston|Philadelphia|New York|Oxford|Elsevier|Springer|Wiley|Mosby|Saunders|Lippincott|\d+\s*[-–]\s*\d+\.?\s*$|https?://|doi",
        RegexOptions.IgnoreCase)]
    private static partial Regex CitationCueRegex();

    [GeneratedRegex(@"^\s*(?:[-*•]\s*)*[^\d\n]{2,80}?[\s,]+\d{1,4}(?:\s*[,–-]\s*\d{1,4})*\s*\.?\s*$")]
    private static partial Regex IndexLineRegex();

    [GeneratedRegex(@"\S+")]
    private static partial Regex WordRegex();

    [GeneratedRegex(@"^\d{1,4}[,;.]?$")]
    private static partial Regex PageNumberRegex();

    [GeneratedRegex(@"(?<!\w)(?:mg|мг|ml|мл|kg|кг|mmol|ммол|mm|мм|cm|см|g|г|h|ч|дни|ден|мес|год)(?!\w)|%|°", RegexOptions.IgnoreCase)]
    private static partial Regex UnitRegex();

    public static bool IsReferenceMatter(string text)
    {
        var lines = text.Split('\n').Where(l => l.Trim().Length > 2).ToList();
        if (lines.Count >= 2)
        {
            var total = lines.Sum(l => l.Length);
            if (Dominates(lines.Where(IsCitation).ToList(), total, RetrievalConstants.MinCitationLines, RetrievalConstants.MinCitationShare)
                || Dominates(lines.Where(l => IndexLineRegex().IsMatch(l)).ToList(), total, RetrievalConstants.MinIndexLines, RetrievalConstants.MinIndexShare))
            {
                return true;
            }
        }

        return IsRunOnIndex(text);
    }

    private static bool Dominates(List<string> matches, int total, int minLines, double minShare) =>
        matches.Count >= minLines && matches.Sum(l => l.Length) >= total * minShare;

    private static bool IsCitation(string line) =>
        (YearRegex().IsMatch(line) && (NumberedRegex().IsMatch(line) || CitationCueRegex().IsMatch(line)))
        || line.Contains("http", StringComparison.OrdinalIgnoreCase);

    private static bool IsRunOnIndex(string text)
    {
        if (text.Contains('|'))
        {
            return false;
        }

        var words = WordRegex().Matches(text).Select(m => m.Value).ToList();
        if (words.Count < RetrievalConstants.MinRunOnIndexWords)
        {
            return false;
        }

        var pageNumbers = words.Count(w => PageNumberRegex().IsMatch(w));
        var sentenceBreaks = text.Split(". ").Length - 1;
        return pageNumbers >= words.Count * RetrievalConstants.MinPageNumberShare
            && sentenceBreaks <= words.Count / RetrievalConstants.WordsPerSentenceBreak
            && UnitRegex().Matches(text).Count <= RetrievalConstants.MaxUnitsInIndex;
    }
}
