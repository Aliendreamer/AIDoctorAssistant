using System.Text.RegularExpressions;

namespace MedAssist.Shared.Validation;

/// <summary>
/// Allowlist + path-containment rules for <c>BookId</c>, which is used to build filesystem paths
/// (uploaded PDF, cached markdown, the path handed to the Marker OCR service). A strict allowlist
/// — lowercase alphanumerics and hyphens, starting alphanumeric, max 64 chars — forbids path
/// separators and dots, so a crafted id can never traverse outside the books directory (audit P1-1).
/// </summary>
public static partial class BookIdRules
{
    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}$")]
    private static partial Regex Pattern();

    public static bool IsValid(string? bookId)
        => !string.IsNullOrEmpty(bookId) && Pattern().IsMatch(bookId);

    /// <summary>
    /// Builds an absolute file path for <paramref name="bookId"/> under <paramref name="baseDir"/>,
    /// validating the id and asserting the resolved path stays within the base directory.
    /// Throws <see cref="ArgumentException"/> for an invalid id or an escaping path.
    /// </summary>
    public static string ResolveWithin(string baseDir, string bookId, string extension)
    {
        if (!IsValid(bookId))
        {
            throw new ArgumentException($"Invalid BookId '{bookId}'.", nameof(bookId));
        }

        var baseFull = Path.GetFullPath(baseDir);
        var candidate = Path.GetFullPath(Path.Combine(baseFull, bookId + extension));

        var baseWithSep = baseFull.EndsWith(Path.DirectorySeparatorChar)
            ? baseFull
            : baseFull + Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(baseWithSep, StringComparison.Ordinal))
        {
            throw new ArgumentException($"BookId '{bookId}' escapes the base directory.", nameof(bookId));
        }

        return candidate;
    }
}
