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

    // Marker image file refs: ![](_page_N_Picture.jpeg) — no semantic value for RAG
    [GeneratedRegex(@"^\s*!\[[^\]]*\]\([^)]+\.(jpe?g|png|gif|webp|svg)\)\s*$", RegexOptions.Multiline, matchTimeoutMilliseconds: 5000)]
    private static partial Regex MarkerImageRefRegex();

    // U+00AD soft hyphen optionally followed by whitespace — scanned PDF artifact
    [GeneratedRegex(@"­[ \t]*", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex SoftHyphenRegex();

    // "word - word" mid-line hyphen with spaces — OCR line-break artifact
    [GeneratedRegex(@"([А-Яа-яЁёA-Za-z0-9]) - ([А-Яа-яёa-z])", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex SpaceHyphenSpaceRegex();

    // "word- word" trailing hyphen + space — hyphenated word broken across PDF lines. A hyphen before "и/или/and/or"
    // is a suspended hyphen ("макро- и микронутриенти") and is kept: 632 in the OCR'd books, 32 in the Nelson parts.
    [GeneratedRegex(@"([А-Яа-яЁёA-Za-z0-9])- (?!(?:и|или|and|or)[\s,])([А-Яа-яёa-z])", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex HyphenSpaceRegex();

    // Two or more non-newline whitespace chars — PDF justification spacing
    [GeneratedRegex(@"[^\S\n]{2,}", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex MultipleSpacesRegex();

    // Portuguese diacritics (ç ã ő ñ õ) embedded inside Cyrillic words — PDF font-encoding artifact
    // Removes the diacritic characters when they appear between Cyrillic characters
    [GeneratedRegex(@"(?<=[Ѐ-ӿ])[çãőñõÇÃŐÑÕ]+(?=[Ѐ-ӿ])", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex CyrillicPortugueseArtifactRegex();

    private static string StripInlineImages(string markdown)
    {
        markdown = InlineImageRegex().Replace(markdown, string.Empty);
        markdown = MarkerImageRefRegex().Replace(markdown, string.Empty);
        return markdown;
    }

    private static string NormalizeOcrArtifacts(string markdown)
    {
        markdown = SoftHyphenRegex().Replace(markdown, string.Empty);
        markdown = SpaceHyphenSpaceRegex().Replace(markdown, "$1$2");
        markdown = HyphenSpaceRegex().Replace(markdown, "$1$2");
        markdown = MultipleSpacesRegex().Replace(markdown, " ");
        markdown = CyrillicPortugueseArtifactRegex().Replace(markdown, string.Empty);
        return markdown;
    }

    public MarkdownChunker(int overlapChars = 512) => _overlapChars = overlapChars;

    public IReadOnlyList<MarkdownChunk> Chunk(string markdown)
    {
        markdown = StripInlineImages(markdown);
        markdown = NormalizeOcrArtifacts(markdown);
        var lines = markdown.Split('\n');
        var sections = new List<Section>();
        var headingStack = new Stack<string>();
        var currentContent = new StringBuilder();
        ContentType currentType = ContentType.Text;

        // Page markers (change ingest-docx-and-page-citations): a marker before any content of a section sets where
        // that section starts; a marker inside content stays inline until SplitLargeChunks resolves the ranges.
        PageRef? currentPage = null;
        PageRef? sectionStart = null;

        foreach (var line in lines)
        {
            if (PageMarker.TryParse(line, out var page))
            {
                currentPage = page;
                if (string.IsNullOrWhiteSpace(currentContent.ToString()))
                {
                    sectionStart = page;
                }
                else
                {
                    currentContent.AppendLine(line);
                }

                continue;
            }

            var headingMatch = Regex.Match(line, @"^(#{1,6})\s+(.+)");
            if (headingMatch.Success)
            {
                FlushChunk(sections, headingStack, currentContent, currentType, sectionStart);
                var level = headingMatch.Groups[1].Value.Length;
                var title = headingMatch.Groups[2].Value.Trim();
                while (headingStack.Count >= level)
                {
                    headingStack.Pop();
                }

                headingStack.Push(title);
                currentContent.Clear();
                currentType = ContentType.Text;
                sectionStart = currentPage;
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

        FlushChunk(sections, headingStack, currentContent, currentType, sectionStart);

        // Ranges are fixed and markers removed before overlap, so overlap copied from the previous chunk never
        // widens a chunk's page range.
        var split = SplitLargeChunks(sections);
        var overlapped = ApplyOverlap(split);
        return MergeSmallChunks(overlapped);
    }

    private sealed record Section(string HeadingPath, string Text, ContentType ContentType, PageRef? StartPage);

    private static void FlushChunk(
        List<Section> sections,
        Stack<string> headingStack,
        StringBuilder content,
        ContentType type,
        PageRef? startPage)
    {
        var text = content.ToString().Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var path = string.Join(" > ", headingStack.Reverse());
        sections.Add(new Section(path, text, type, startPage));
    }

    private static IReadOnlyList<MarkdownChunk> SplitLargeChunks(IReadOnlyList<Section> sections)
    {
        var result = new List<MarkdownChunk>();
        foreach (var section in sections)
        {
            var carry = section.StartPage;
            foreach (var piece in SplitText(section.Text))
            {
                PageRange pages;
                (pages, carry) = ResolvePages(piece, carry);
                var text = PageMarker.Strip(piece).Trim();
                if (text.Length > 0)
                {
                    result.Add(new MarkdownChunk(section.HeadingPath, text, section.ContentType, pages));
                }
            }
        }

        return result;
    }

    private static IEnumerable<string> SplitText(string text)
    {
        if (EstimateTokens(PageMarker.Strip(text)) <= _maxTokens)
        {
            yield return text;
            yield break;
        }

        var sentences = Regex.Split(text, @"(?<=[.!?])\s+");
        var current = new StringBuilder();
        foreach (var sentence in sentences)
        {
            if (EstimateTokens(current.ToString()) + EstimateTokens(sentence) > _maxTokens && current.Length > 0)
            {
                yield return current.ToString().Trim();
                current.Clear();
            }

            current.Append(sentence).Append(' ');
        }

        if (current.Length > 0)
        {
            yield return current.ToString().Trim();
        }
    }

    // The range runs from the page in effect where the piece starts to the last marker inside it. A marker with
    // no text before it moves the start, so a piece that begins at a page break doesn't claim the previous page.
    private static (PageRange Pages, PageRef? Carry) ResolvePages(string piece, PageRef? carry)
    {
        var spanned = new List<PageRef>();
        if (carry is { } inEffect)
        {
            spanned.Add(inEffect);
        }

        foreach (var (page, index) in PageMarker.FindAll(piece))
        {
            if (string.IsNullOrWhiteSpace(PageMarker.Strip(piece[..index])))
            {
                spanned.Clear();
            }

            spanned.Add(page);
        }

        return (ToRange(spanned), spanned.Count > 0 ? spanned[^1] : carry);
    }

    // Where the source skipped pages (an uncertain stretch), the printed page can't be trusted, so the range falls
    // back to PDF pages running to the end of the stretch: less exact, but not wrong.
    private static PageRange ToRange(IReadOnlyList<PageRef> spanned)
    {
        if (spanned.Count == 0)
        {
            return PageRange.Unknown;
        }

        var pdfStart = spanned.Min(p => p.PdfPage);
        var pdfEnd = spanned.Max(p => Math.Max(p.PdfPage, p.UntilPdf ?? 0));
        return spanned.Any(p => p.IsUncertain)
            ? new PageRange(pdfStart, pdfEnd, null, null)
            : new PageRange(pdfStart, pdfEnd, spanned[0].PrintedPage, spanned[^1].PrintedPage);
    }

    private static PageRange Combine(PageRange first, PageRange second)
    {
        if (first == PageRange.Unknown)
        {
            return second;
        }

        if (second == PageRange.Unknown)
        {
            return first;
        }

        // Printed pages survive a merge only when both sides have them; otherwise the PDF range is cited.
        var printed = first.PrintedStart is not null && second.PrintedStart is not null;
        return new PageRange(
            Math.Min(first.PdfStart, second.PdfStart),
            Math.Max(first.PdfEnd, second.PdfEnd),
            printed ? first.PrintedStart : null,
            printed ? second.PrintedEnd : null);
    }

    // Prepend the tail of the previous chunk as overlap prefix when consecutive
    // chunks share the same heading path. Resets at heading boundaries.
    private IReadOnlyList<MarkdownChunk> ApplyOverlap(IReadOnlyList<MarkdownChunk> chunks)
    {
        if (_overlapChars <= 0 || chunks.Count <= 1)
        {
            return chunks;
        }

        var result = new List<MarkdownChunk>(chunks.Count);
        for (var i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            var text = chunk.Text;
            if (i > 0 && chunks[i - 1].HeadingPath == chunk.HeadingPath)
            {
                // Cap the overlap so prepending it can't push the chunk past the token budget, which
                // would make the embedder silently truncate the chunk's own tail (audit P3-2).
                var available = (_maxTokens * 4) - text.Length - 1;
                if (available > 0)
                {
                    var overlapLen = Math.Min(_overlapChars, available);
                    var prev = chunks[i - 1].Text;
                    var overlap = prev.Length > overlapLen ? prev[^overlapLen..] : prev;
                    text = overlap + "\n" + text;
                }
            }

            result.Add(chunk with { Text = text });
        }

        return result;
    }

    private static IReadOnlyList<MarkdownChunk> MergeSmallChunks(IReadOnlyList<MarkdownChunk> chunks)
    {
        var result = new List<MarkdownChunk>();
        MarkdownChunk? pending = null;

        foreach (var chunk in chunks)
        {
            if (pending is null)
            {
                pending = chunk;
                continue;
            }

            // Only merge a small chunk into the next when they share a heading — merging across a
            // heading boundary kept the previous heading path and mislabeled the content (audit P3-1).
            if (EstimateTokens(pending.Text) < _minTokens && pending.HeadingPath == chunk.HeadingPath)
            {
                pending = pending with
                {
                    Text = pending.Text + "\n" + chunk.Text,
                    Pages = Combine(pending.Pages, chunk.Pages),
                };
            }
            else
            {
                result.Add(pending);
                pending = chunk;
            }
        }

        if (pending is not null)
        {
            result.Add(pending);
        }

        return result;
    }

    private static int EstimateTokens(string text) => text.Length / 4;
}

/// <summary>One chunk of book markdown: its heading path, text, content type and the pages it spans.</summary>
public sealed record MarkdownChunk(string HeadingPath, string Text, ContentType ContentType, PageRange Pages);
