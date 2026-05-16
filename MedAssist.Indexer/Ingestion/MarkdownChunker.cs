using MedAssist.Shared.Models;
using System.Text;
using System.Text.RegularExpressions;

namespace MedAssist.Indexer.Ingestion;

public sealed class MarkdownChunker
{
    private const int _maxTokens = 512;
    private const int _minTokens = 50;

    public IReadOnlyList<(string HeadingPath, string Text, ContentType ContentType)> Chunk(string markdown)
    {
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

                // Trim stack to current level
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
        return MergeSmallChunks(SplitLargeChunks(chunks));
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
            var tokens = EstimateTokens(text);
            if (tokens <= _maxTokens)
            {
                result.Add((path, text, type));
                continue;
            }

            // Split at sentence boundaries
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
                // Merge small chunk with next
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
