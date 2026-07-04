using System.Text.RegularExpressions;

namespace MedAssist.AI.Plugins;

/// <summary>
/// Strips the light markdown an LLM sometimes emits (headings, bold/italic, list markers) so the
/// answer reads as the continuous prose the system prompt requires. Extracted from
/// <see cref="RagPluginBase"/> as a focused collaborator (audit P2-16).
/// </summary>
internal static partial class MarkdownStripper
{
    public static string Strip(string text)
    {
        text = ThinkRegex().Replace(text, "");      // drop <think>…</think> reasoning (qwen3 et al.)
        text = HeadingRegex().Replace(text, "");   // ## Title → Title
        text = BoldRegex().Replace(text, "$1");     // **text** → text
        text = ItalicRegex().Replace(text, "$1");   // *text* → text
        text = BulletRegex().Replace(text, "");     // "- item" → "item"
        text = NumberedRegex().Replace(text, "");   // "1. item" → "item"
        return text.Trim();
    }

    // Reasoning models (e.g. qwen3) prepend a <think>…</think> block; strip it so only the answer
    // prose reaches the user. Singleline so '.' spans the multi-line reasoning body.
    [GeneratedRegex(@"<think>.*?</think>\s*", RegexOptions.Singleline)]
    private static partial Regex ThinkRegex();

    [GeneratedRegex(@"^#{1,6}\s+", RegexOptions.Multiline)]
    private static partial Regex HeadingRegex();
    [GeneratedRegex(@"\*\*(.+?)\*\*", RegexOptions.Singleline)]
    private static partial Regex BoldRegex();
    [GeneratedRegex(@"\*(.+?)\*", RegexOptions.Singleline)]
    private static partial Regex ItalicRegex();
    [GeneratedRegex(@"^[ \t]*[-*]\s+", RegexOptions.Multiline)]
    private static partial Regex BulletRegex();
    [GeneratedRegex(@"^[ \t]*\d+\.\s+", RegexOptions.Multiline)]
    private static partial Regex NumberedRegex();
}
