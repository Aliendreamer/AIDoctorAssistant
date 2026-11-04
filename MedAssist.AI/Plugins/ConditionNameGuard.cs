using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MedAssist.AI.Plugins;

/// <summary>
/// Refuses questions about a named condition the excerpts never mention. The reranker cannot do this:
/// it scores invented diseases as high as real ones, because the excerpts still look medically relevant.
/// Questions usually spell a name in Cyrillic while the books use Latin (<c>Гоше</c> and <c>Gaucher</c>),
/// so names are compared by sound. Only questions that name a condition are checked, and a name passes
/// when any of its parts is found: refusing a real disease is worse than letting a rare invented one through.
/// </summary>
public static partial class ConditionNameGuard
{
    private const int _minPartLength = 3;
    private const int _shortNameLength = 6;
    private const int _longNameLength = 9;
    private const int _stemLength = 5;
    private const int _stemSuffix = 2;
    private const char _anyVowel = '*';
    private const char _firstNonLatin = '\u0400';
    private const string _vowels = "aeiou";

    private const string _word = @"\p{L}[\p{L}'’]*";

    private static readonly Dictionary<char, string> _cyrillic = new()
    {
        ['а'] = "a", ['б'] = "b", ['в'] = "v", ['г'] = "g", ['д'] = "d", ['е'] = "e", ['ж'] = "j", ['з'] = "z",
        ['и'] = "i", ['й'] = "i", ['к'] = "k", ['л'] = "l", ['м'] = "m", ['н'] = "n", ['о'] = "o", ['п'] = "p",
        ['р'] = "r", ['с'] = "z", ['т'] = "t", ['у'] = "u", ['ф'] = "f", ['х'] = "h", ['ц'] = "z", ['ч'] = "ch",
        ['ш'] = "sh", ['щ'] = "sht", ['ъ'] = "*", ['ь'] = "", ['ю'] = "iu", ['я'] = "ia",
    };

    private static readonly (string From, string To)[] _latinSounds =
    [
        ("sch", "sh"), ("sj", "sh"), ("ph", "f"), ("th", "t"), ("ck", "k"), ("ow", "au"), ("oo", "u"),
        ("qu", "kv"), ("x", "ks"), ("w", "v"), ("y", "i"), ("hn", "n"),
    ];

    // "болест на Кавазаки", "синдрома на Chiari": in Bulgarian the name follows the word.
    [GeneratedRegex(@"(?:синдром|болест)\p{L}*\s+на\s+(?<name>" + _word + @"(?:\s*-\s*" + _word + ")*)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex BulgarianName();

    // "Kawasaki disease": in English it precedes it, and only capitals tell a name from "heart disease".
    [GeneratedRegex(@"(?<name>\p{Lu}[\p{L}'’]*(?:\s*-\s*" + _word + @")*(?:\s+\p{Lu}[\p{L}'’]*)*)\s+(?:syndrome|disease)\b",
        RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex EnglishName();

    [GeneratedRegex(@"[^\p{L}]+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex NonLetters();

    [GeneratedRegex(@"\b\p{Lu}\p{L}{2,}", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex CapitalisedWord();

    [GeneratedRegex(@"(.)\1+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RepeatedLetter();

    [GeneratedRegex(@"g(?=[ei])", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SoftG();

    [GeneratedRegex(@"[rdt]$", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SilentFrenchEnding();

    [GeneratedRegex(@"(?<!s)ch", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex GermanCh();

    [GeneratedRegex(@"c(?!h)", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex HardC();

    [GeneratedRegex(@"s(?!h)", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex SoftS();

    /// <summary>The conditions named in the question, each as its lowercase name parts.</summary>
    public static IReadOnlyList<IReadOnlyList<string>> ExtractNames(string question)
    {
        var english = EnglishName().Matches(question)
            // A capital on the first word says nothing, unless a hyphen marks it as a name.
            .Where(m => m.Index > 0 || m.Groups["name"].Value.Contains('-'));

        return BulgarianName().Matches(question)
            .Concat(english)
            .Select(m => Parts(m.Groups["name"].Value))
            .Where(parts => parts.Count > 0)
            .ToList();
    }

    /// <summary>
    /// The Latin words from the books' vocabulary that spell the named conditions, so a search for
    /// <c>Гудпасчър</c> can reach text that says <c>Goodpasture</c>. Far stricter than
    /// <see cref="AllAppearIn"/>: a loose match would turn an invented name into a real word, retrieve
    /// that word's chunks and then satisfy the guard.
    /// </summary>
    public static IReadOnlyList<string> FindBookSpellings(
        IReadOnlyList<IReadOnlyList<string>> names, IEnumerable<string> vocabulary)
    {
        var parts = names.SelectMany(n => n).Select(Sounds).ToList();
        if (parts.Count == 0)
        {
            return [];
        }

        return vocabulary
            .Where(word => word.Length >= _minPartLength && word.All(c => c < _firstNonLatin))
            .Where(word =>
            {
                var sounds = Sounds(word);
                return parts.Any(part => part.Any(a => sounds.Any(b => IsSameName(a, b))));
            })
            .ToList();
    }

    /// <summary>A Cyrillic word in the Latin letters <see cref="BookTermLookup"/> compares, reading <c>ъ</c> as <c>a</c>.</summary>
    internal static string SpellInLatin(string word) =>
        Squeeze(string.Concat(word.Select(c => _cyrillic.GetValueOrDefault(c, c.ToString()))).Replace(_anyVowel, 'a'));

    /// <summary>True when every named condition has a part mentioned somewhere in the texts.</summary>
    public static bool AllAppearIn(IReadOnlyList<IReadOnlyList<string>> names, IEnumerable<string> texts)
    {
        if (names.Count == 0)
        {
            return true;
        }

        var passages = texts.ToList();
        var capitalised = passages
            .SelectMany(t => CapitalisedWord().Matches(t).Select(m => m.Value))
            .Distinct()
            .Select(Sounds)
            .ToList();

        return names.All(name => name.Any(part => IsMentioned(part, passages, capitalised)));
    }

    private static List<string> Parts(string name) =>
        NonLetters().Split(StripPossessive(name).ToLowerInvariant())
            .Where(p => p.Length >= _minPartLength)
            .ToList();

    private static string StripPossessive(string name) => name.Replace("'s", string.Empty).Replace("’s", string.Empty);

    private static bool IsMentioned(string part, List<string> passages, List<HashSet<string>> capitalised)
    {
        // Everyday words in descriptive names ("синдром на раздразненото черво") are inflected, not transliterated.
        var stem = part.Length > _stemLength ? part[..^_stemSuffix] : part;
        if (passages.Any(t => t.Contains(stem, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var sounds = Sounds(part);
        return capitalised.Any(word => sounds.Any(a => word.Any(b => SoundsAlike(a, b))));
    }

    private static bool SoundsAlike(string a, string b)
    {
        var allowed = a.Length < _shortNameLength ? 1 : a.Length < _longNameLength ? 2 : 3;
        return EditDistance(a, b, allowed) <= allowed;
    }

    private static bool IsSameName(string a, string b)
    {
        var allowed = a.Length < _longNameLength ? 0 : 1;
        return EditDistance(a, b, allowed) <= allowed;
    }

    // A Latin name has no single reading, so each plausible one is compared: English (Down), German
    // (Henoch) and French (Gaucher).
    private static HashSet<string> Sounds(string word)
    {
        var folded = Fold(word);
        if (folded.Any(_cyrillic.ContainsKey))
        {
            // "уи" at the start is how Bulgarian spells an English W: Уилсън, Уискот.
            var spoken = folded.StartsWith("уи", StringComparison.Ordinal) ? "ви" + folded[2..] : folded;
            return [Squeeze(string.Concat(spoken.Select(c => _cyrillic.GetValueOrDefault(c, c.ToString()))))];
        }

        var english = Latin(SoftG().Replace(folded, "j").Replace("j", "dj"));
        var german = GermanCh().Replace(folded, "h").Replace("ei", "ai").Replace("ie", "i");
        var french = SilentFrenchEnding().Replace(folded, string.Empty)
            .Replace("eau", "o").Replace("au", "o").Replace("ch", "sh");
        return
        [
            english.EndsWith("ture", StringComparison.Ordinal) ? english[..^4] + "char" : english,
            Latin(folded),
            Latin(german),
            Latin(french),
        ];
    }

    private static string Latin(string word)
    {
        foreach (var (from, to) in _latinSounds)
        {
            word = word.Replace(from, to);
        }

        return Squeeze(SoftS().Replace(HardC().Replace(word, "k"), "z"));
    }

    private static string Squeeze(string word) => RepeatedLetter().Replace(word, "$1");

    private static string Fold(string word)
    {
        var folded = new StringBuilder(word.Length);
        foreach (var c in StripPossessive(word).ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                folded.Append(c);
            }
        }

        return folded.ToString();
    }

    // Bulgarian "ъ" stands for whichever vowel the foreign name had: Търнър, Хърлър.
    private static bool SameSound(char a, char b) =>
        a == b || (a == _anyVowel && _vowels.Contains(b)) || (b == _anyVowel && _vowels.Contains(a));

    private static int EditDistance(string a, string b, int limit)
    {
        if (Math.Abs(a.Length - b.Length) > limit)
        {
            return limit + 1;
        }

        var previous = Enumerable.Range(0, b.Length + 1).ToArray();
        for (var i = 1; i <= a.Length; i++)
        {
            var current = new int[b.Length + 1];
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var substitution = previous[j - 1] + (SameSound(a[i - 1], b[j - 1]) ? 0 : 1);
                current[j] = Math.Min(Math.Min(previous[j] + 1, current[j - 1] + 1), substitution);
            }

            previous = current;
        }

        return previous[b.Length];
    }
}
