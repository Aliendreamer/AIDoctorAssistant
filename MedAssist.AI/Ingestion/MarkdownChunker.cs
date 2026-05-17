using System.Text;
using System.Text.RegularExpressions;
using MedAssist.Shared.Models;

namespace MedAssist.AI.Ingestion;

public sealed partial class MarkdownChunker
{
    private const int _maxTokens = 512;
    private const int _minTokens = 50;
    private readonly int _overlapChars;

    [GeneratedRegex(@"!\[[^\]]*\]\(data:[^)]+\)", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex InlineImageRegex();

    private static string StripInlineImages(string markdown) =>
        InlineImageRegex().Replace(markdown, string.Empty);

    public MarkdownChunker(int overlapChars = 512) => _overlapChars = overlapChars;

    public IReadOnlyList<(string HeadingPath, string Text, ContentType ContentType)> Chunk(string markdown)
    {
        markdown = StripInlineImages(markdown);
        var lines = markdown.Split('\n');
        var chunks = new List<(string, string, ContentType)>();
        var headingStack = new Stack<string>();
        var currentContent = new StringBuilder();
        ContentType currentType = ContentType.Text;

        foreach (var line in lines)
        {
            var headingMatch = Regex.Match(line, @"^(#{1,6})\s+(.+)");
            if (headingMatch.Success)
            {
                FlushChunk(chunks, headingStack, currentContent, currentType);
                var level = headingMatch.Groups[1].Value.Length;
                var title = headingMatch.Groups[2].Value.Trim();
                while (headingStack.Count >= level)
                {
                    headingStack.Pop();
                }

                headingStack.Push(title);
                currentContent.Clear();
                currentType = ContentType.Text;
            }
            else
            {
                if (line.TrimStart().StartsWith('|'))
                {
                    currentType = ContentType.Table;
                }
                else if (line.TrimStart().StartsWith('-') || line.TrimStart().StartsWith('*'))
                {
                    currentType = ContentType.List;
                }

                currentContent.AppendLine(line);
            }
        }

        FlushChunk(chunks, headingStack, currentContent, currentType);

        var split = SplitLargeChunks(chunks);
        var overlapped = ApplyOverlap(split);
        return MergeSmallChunks(overlapped);
    }

    private static void FlushChunk(
        List<(string, string, ContentType)> chunks,
        Stack<string> headingStack,
        StringBuilder content,
        ContentType type)
    {
        var text = content.ToString().Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var path = string.Join(" > ", headingStack.Reverse());
        chunks.Add((path, text, type));
    }

    private static IReadOnlyList<(string, string, ContentType)> SplitLargeChunks(
        IReadOnlyList<(string HeadingPath, string Text, ContentType ContentType)> chunks)
    {
        var result = new List<(string, string, ContentType)>();
        foreach (var (path, text, type) in chunks)
        {
            if (EstimateTokens(text) <= _maxTokens)
            {
                result.Add((path, text, type));
                continue;
            }

            var sentences = Regex.Split(text, @"(?<=[.!?])\s+");
            var current = new StringBuilder();
            foreach (var sentence in sentences)
            {
                if (EstimateTokens(current.ToString()) + EstimateTokens(sentence) > _maxTokens && current.Length > 0)
                {
                    result.Add((path, current.ToString().Trim(), type));
                    current.Clear();
                }

                current.Append(sentence).Append(' ');
            }

            if (current.Length > 0)
            {
                result.Add((path, current.ToString().Trim(), type));
            }
        }

        return result;
    }

    // Prepend the tail of the previous chunk as overlap prefix when consecutive
    // chunks share the same heading path. Resets at heading boundaries.
    private IReadOnlyList<(string, string, ContentType)> ApplyOverlap(
        IReadOnlyList<(string HeadingPath, string Text, ContentType ContentType)> chunks)
    {
        if (_overlapChars <= 0 || chunks.Count <= 1)
        {
            return chunks;
        }

        var result = new List<(string, string, ContentType)>(chunks.Count);
        for (var i = 0; i < chunks.Count; i++)
        {
            var (path, text, type) = chunks[i];
            if (i > 0 && chunks[i - 1].HeadingPath == path)
            {
                var prev = chunks[i - 1].Text;
                var overlap = prev.Length > _overlapChars ? prev[^_overlapChars..] : prev;
                text = overlap + "\n" + text;
            }

            result.Add((path, text, type));
        }

        return result;
    }

    private static IReadOnlyList<(string, string, ContentType)> MergeSmallChunks(
        IReadOnlyList<(string HeadingPath, string Text, ContentType ContentType)> chunks)
    {
        var result = new List<(string, string, ContentType)>();
        var pending = default((string HeadingPath, string Text, ContentType ContentType)?);

        foreach (var chunk in chunks)
        {
            if (pending is null)
            {
                pending = chunk;
                continue;
            }

            if (EstimateTokens(pending.Value.Text) < _minTokens)
            {
                pending = (pending.Value.HeadingPath, pending.Value.Text + "\n" + chunk.Text, pending.Value.ContentType);
            }
            else
            {
                result.Add(pending.Value);
                pending = chunk;
            }
        }

        if (pending.HasValue)
        {
            result.Add(pending.Value);
        }

        return result;
    }

    private static int EstimateTokens(string text) => text.Length / 4;
}
