namespace MedAssist.Shared.Constants;

/// <summary>
/// Thresholds for recognising reference lists and book indexes, which are kept out of the excerpts the model
/// reads. Tuned on the indexed books: about 3% of chunks match, and sampled matches were all reference matter.
/// </summary>
public static class RetrievalConstants
{
    /// <summary>Fewer citation lines than this is a passage that cites its source, not a reference list.</summary>
    public const int MinCitationLines = 2;

    /// <summary>Share of a chunk's characters that must sit on citation lines.</summary>
    public const double MinCitationShare = 0.6;

    /// <summary>Index pages with one entry per line: at least this many entries...</summary>
    public const int MinIndexLines = 5;

    /// <summary>...holding at least this share of the characters.</summary>
    public const double MinIndexShare = 0.6;

    /// <summary>Index pages that OCR ran together into one paragraph need at least this many words to judge.</summary>
    public const int MinRunOnIndexWords = 40;

    /// <summary>Share of words that are bare page numbers in a run-on index.</summary>
    public const double MinPageNumberShare = 0.25;

    /// <summary>A run-on index has at most one sentence break per this many words.</summary>
    public const int WordsPerSentenceBreak = 40;

    /// <summary>Clinical text full of numbers carries units; an index carries at most this many.</summary>
    public const int MaxUnitsInIndex = 2;
}
