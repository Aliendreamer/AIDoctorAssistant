using System.Text.RegularExpressions;

namespace MedAssist.AI.Plugins;

/// <summary>
/// Finds the books' Bulgarian term for the medical words in an English question. The local model invents
/// Bulgarian medical words when it translates (<c>отитис медиалин</c>) and sometimes swaps the concept
/// (bronchiolitis became <c>Пневмония</c>), yet most medical terms share a Greek or Latin root in both
/// languages. Each English word is rewritten by those shared rules and matched against the start of the
/// words in the books. Only words the books use are returned, so a mistaken match stays a real term.
/// Everyday words without a shared root, such as <c>poisoning</c>, are out of reach.
/// </summary>
public static partial class BookTermLookup
{
    private const int _minWordLength = 7;
    private const int _minKeyLength = 4;
    private const int _minBookWordLength = 4;
    private const int _maxInflectionLength = 5;
    private const int _maxFormsPerWord = 2;
    private const int _minStemLength = 2;

    // Short words collide with unrelated ones (severe, северна) unless a medical ending vouches for them.
    private static readonly string[] _medicalEndings = ["itis", "emia", "osis", "asis", "penia", "algia", "opathy"];

    // Same letters but a different meaning, or so common in the books that they only add noise.
    private static readonly HashSet<string> _unhelpfulWords = ["infant", "general", "hospital", "syndrome", "symptom"];

    private static readonly (string English, string Bulgarian)[] _endings =
    [
        ("emia", "emi"), ("itis", "it"), ("osis", "oz"), ("asis", "az"), ("ysis", "iz"), ("ectomy", "ektomi"),
        ("opathy", "opati"), ("algia", "algi"), ("penia", "peni"), ("plasia", "plazi"), ("tion", "zi"), ("sion", "zi"),
        ("ism", "iz"), ("ical", "i"), ("ic", "i"), ("ia", "i"), ("y", "i"), ("um", string.Empty), ("us", string.Empty),
        ("a", "a"), ("e", string.Empty),
    ];

    private static readonly (string From, string To)[] _sounds =
    [
        ("thyro", "tireo"), ("ph", "f"), ("th", "t"), ("ch", "h"), ("qu", "kv"), ("x", "ks"), ("y", "i"), ("w", "v"),
        ("ju", "iu"), ("eu", "ev"), ("c", "k"), ("s", "z"),
    ];

    [GeneratedRegex(@"\p{L}+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Word();

    [GeneratedRegex(@"c(?=[eiy])", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SoftC();

    [GeneratedRegex(@"(.)\1+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RepeatedLetter();

    /// <summary>Only an English question goes through translation; a Bulgarian one is searched as written.</summary>
    public static bool Applies(string question) => !question.Any(IsCyrillic);

    /// <summary>
    /// The commonest book forms of each medical word in the question. Named conditions are skipped: they
    /// have their own strict lookup, and here an invented name could turn into a real word (Kraven, кръвен).
    /// </summary>
    public static IReadOnlyList<string> Find(
        string question,
        IReadOnlyList<IReadOnlyList<string>> names,
        IReadOnlyDictionary<string, float> wordWeights)
    {
        if (!Applies(question))
        {
            return [];
        }

        var skipped = names.SelectMany(n => n).ToHashSet();
        var keys = Word().Matches(question.ToLowerInvariant())
            .Select(m => m.Value)
            .Where(word => !skipped.Contains(word) && IsCandidate(word))
            .Select(EnglishKey)
            .Where(key => key.Length >= _minKeyLength)
            .Distinct()
            .ToList();
        if (keys.Count == 0)
        {
            return [];
        }

        var bookWords = wordWeights
            .Where(w => w.Key.Length >= _minBookWordLength && w.Key.All(IsCyrillic))
            .Select(w => (Word: w.Key, Weight: w.Value, Key: ConditionNameGuard.SpellInLatin(w.Key)))
            .ToList();

        return keys
            .SelectMany(key => bookWords
                .Where(b => b.Key.StartsWith(key, StringComparison.Ordinal)
                    && b.Key.Length - key.Length <= _maxInflectionLength)
                .OrderBy(b => b.Weight)
                .ThenBy(b => b.Word, StringComparer.Ordinal)
                .Take(_maxFormsPerWord)
                .Select(b => b.Word))
            .Distinct()
            .ToList();
    }

    private static bool IsCandidate(string word) =>
        !_unhelpfulWords.Contains(Singular(word))
        && (word.Length >= _minWordLength || _medicalEndings.Any(e => word.EndsWith(e, StringComparison.Ordinal)));

    private static string EnglishKey(string word)
    {
        var spelled = Singular(word.Replace('é', 'e').Replace("ae", "e"));
        spelled = spelled.StartsWith("oe", StringComparison.Ordinal) ? "e" + spelled[2..] : spelled;
        spelled = SoftC().Replace(spelled, "z");

        var ending = Array.Find(
            _endings,
            e => spelled.EndsWith(e.English, StringComparison.Ordinal)
                && spelled.Length - e.English.Length >= _minStemLength);
        var stem = ending.English is null ? spelled : spelled[..^ending.English.Length];
        foreach (var (from, to) in _sounds)
        {
            stem = stem.Replace(from, to);
        }

        return RepeatedLetter().Replace(stem + ending.Bulgarian, "$1");
    }

    private static string Singular(string word) =>
        word.EndsWith('s') && !word.EndsWith("ss", StringComparison.Ordinal)
            && !word.EndsWith("is", StringComparison.Ordinal) && !word.EndsWith("us", StringComparison.Ordinal)
            ? word[..^1]
            : word;

    private static bool IsCyrillic(char c) => c is >= 'Ѐ' and <= 'ӿ';
}
