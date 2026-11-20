using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using MedAssist.Shared.Models;
using Microsoft.Extensions.Logging;

namespace MedAssist.AI.Ingestion;

/// <summary>One Word part of a book: its file name (which carries its order and PDF range) and its content.</summary>
public sealed record DocxPart(string FileName, Func<Stream> Open);

/// <summary>The converted markdown, plus page-like lines the parser did not recognise (they stay in the text).</summary>
public sealed record DocxConversionResult(string Markdown, IReadOnlyList<string> UnrecognisedPageLines);

/// <summary>
/// Converts a book delivered as Word parts into one markdown document with page markers, without OCR (change
/// ingest-docx-and-page-citations). Heading levels come from the style definitions, so the Bulgarian-locale numeric
/// style ids (<c>1</c>, <c>21</c>, <c>31</c>) work like <c>Heading1</c>–<c>Heading3</c>. Tables become markdown
/// tables, images are skipped, and the translator's banners, meta-notes and page lines are removed, the page lines
/// becoming page markers.
/// </summary>
public sealed partial class DocxBookConverter(ILogger<DocxBookConverter> logger)
{
    private const int _maxStyleDepth = 10;

    [GeneratedRegex(@"\s+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex WhitespaceRegex();

    /// <summary>Converts every <c>.docx</c> part in a book's parts directory.</summary>
    public DocxConversionResult ConvertDirectory(string partsDirectory) =>
        Convert(Directory.EnumerateFiles(partsDirectory, "*.docx")
            .Select(path => new DocxPart(Path.GetFileName(path), () => File.OpenRead(path)))
            .ToList());

    public DocxConversionResult Convert(IReadOnlyList<DocxPart> parts)
    {
        var byName = parts.ToDictionary(p => p.FileName, StringComparer.Ordinal);
        var blocks = new List<string>();
        var unrecognised = new List<string>();

        foreach (var name in DocxConventions.OrderParts(byName.Keys))
        {
            if (DocxConventions.FileNamePdfStart(name) is { } pdfStart)
            {
                blocks.Add(PageMarker.Format(new PageRef(pdfStart, null)));
            }

            var partStart = blocks.Count;
            using (var stream = byName[name].Open())
            {
                ConvertPart(stream, name, blocks, unrecognised);
            }

            MarkSkippedPages(blocks, partStart, DocxConventions.FileNamePdfEnd(name));
        }

        foreach (var line in unrecognised)
        {
            logger.LogWarning("Unrecognised page-like line kept as text: {Line}", line);
        }

        logger.LogInformation("Converted {Parts} Word parts to {Chars} markdown chars", parts.Count, blocks.Sum(b => b.Length));
        return new DocxConversionResult(string.Join("\n\n", blocks), unrecognised);
    }

    private static void ConvertPart(Stream stream, string fileName, List<string> blocks, List<string> unrecognised)
    {
        using var doc = WordprocessingDocument.Open(stream, isEditable: false);
        var main = doc.MainDocumentPart ?? throw new InvalidDataException($"Word part '{fileName}' has no document body.");
        var body = main.Document?.Body;
        if (body is null)
        {
            return;
        }

        var headingLevels = HeadingLevels(main.StyleDefinitionsPart?.Styles);
        var leading = true;

        foreach (var element in Blocks(body))
        {
            if (element is Table table)
            {
                if (TableToMarkdown(table) is { Length: > 0 } markdown)
                {
                    blocks.Add(markdown);
                    leading = false;
                }

                continue;
            }

            var paragraph = (Paragraph)element;
            var text = ParagraphText(paragraph);
            if (text.Length == 0)
            {
                continue;
            }

            if (DocxConventions.TryParsePageLine(text, out var page))
            {
                blocks.Add(PageMarker.Format(page));
                continue;
            }

            if (DocxConventions.IsBanner(text, atStartOfPart: leading) || DocxConventions.IsMetaNote(text))
            {
                continue;
            }

            if (DocxConventions.IsUnrecognisedPageLine(text))
            {
                unrecognised.Add(text);
            }

            leading = false;
            var level = HeadingLevel(paragraph, headingLevels);
            if (level > 0)
            {
                // Chapters are always top level and nothing else is, so sections nest under their chapter even
                // where the translator styled the chapter one level down.
                level = DocxConventions.IsChapterHeading(text) ? 1 : Math.Max(level, 2);
            }

            if (level > 0)
            {
                blocks.Add($"{new string('#', Math.Min(level, 6))} {text}");
            }
            else if (paragraph.ParagraphProperties?.NumberingProperties is not null)
            {
                blocks.Add($"- {text}");
            }
            else
            {
                blocks.Add(text);
            }
        }
    }

    /// <summary>
    /// The translator does not note every page: a part covering PDF 2636–2640 may note only 2635 and 2637. Text after
    /// a note runs on to the page before the next note, or to the part's last page, so such a note gets an
    /// <c>until</c> page and the chunker cites the PDF range there instead of a printed page that may be wrong.
    /// </summary>
    private static void MarkSkippedPages(List<string> blocks, int partStart, int? partPdfEnd)
    {
        var markers = new List<(int Index, PageRef Page)>();
        for (var i = partStart; i < blocks.Count; i++)
        {
            if (PageMarker.TryParse(blocks[i], out var page))
            {
                markers.Add((i, page));
            }
        }

        for (var m = 0; m < markers.Count; m++)
        {
            var (index, page) = markers[m];
            var runsTo = m + 1 < markers.Count ? markers[m + 1].Page.PdfPage - 1 : partPdfEnd;
            if (runsTo > page.PdfPage)
            {
                blocks[index] = PageMarker.Format(page with { UntilPdf = runsTo });
            }
        }
    }

    /// <summary>Body-level paragraphs and tables in order, looking inside content controls.</summary>
    private static IEnumerable<OpenXmlElement> Blocks(OpenXmlElement container)
    {
        foreach (var child in container.ChildElements)
        {
            switch (child)
            {
                case Paragraph or Table:
                    yield return child;
                    break;
                case SdtBlock sdt when sdt.SdtContentBlock is { } content:
                    foreach (var inner in Blocks(content))
                    {
                        yield return inner;
                    }

                    break;
            }
        }
    }

    private static string ParagraphText(Paragraph paragraph)
    {
        var sb = new StringBuilder();
        foreach (var run in paragraph.Descendants<Run>())
        {
            foreach (var child in run.ChildElements)
            {
                switch (child)
                {
                    case Text t:
                        sb.Append(t.Text);
                        break;
                    case TabChar or Break or CarriageReturn:
                        sb.Append(' ');
                        break;
                    case NoBreakHyphen:
                        sb.Append('-');
                        break;
                }
            }
        }

        return WhitespaceRegex().Replace(sb.ToString(), " ").Trim();
    }

    private static string TableToMarkdown(Table table)
    {
        var rows = table.Elements<TableRow>()
            .Select(row => row.Elements<TableCell>()
                .Select(cell => string.Join(' ', cell.Descendants<Paragraph>().Select(ParagraphText).Where(t => t.Length > 0))
                    .Replace("|", "\\|", StringComparison.Ordinal))
                .ToList())
            .Where(cells => cells.Any(c => c.Length > 0))
            .ToList();
        if (rows.Count == 0)
        {
            return string.Empty;
        }

        var columns = rows.Max(r => r.Count);
        var lines = rows.Select(r => "| " + string.Join(" | ", r.Concat(Enumerable.Repeat(string.Empty, columns - r.Count))) + " |").ToList();
        lines.Insert(1, "| " + string.Join(" | ", Enumerable.Repeat("---", columns)) + " |");
        return string.Join('\n', lines);
    }

    private static int HeadingLevel(Paragraph paragraph, IReadOnlyDictionary<string, int> headingLevels)
    {
        var properties = paragraph.ParagraphProperties;
        if (properties?.OutlineLevel?.Val?.Value is { } outline && outline < 9)
        {
            return outline + 1;
        }

        return properties?.ParagraphStyleId?.Val?.Value is { } styleId && headingLevels.TryGetValue(styleId, out var level)
            ? level
            : 0;
    }

    /// <summary>
    /// Heading level per paragraph style id, from the style's built-in name (<c>heading 2</c>, <c>Title</c>) or outline
    /// level, following <c>basedOn</c>. Resolving by name rather than id is what makes the locale's numeric ids work.
    /// </summary>
    private static Dictionary<string, int> HeadingLevels(Styles? styles)
    {
        var byId = styles?.Elements<Style>()
            .Where(s => s.StyleId?.Value is not null)
            .GroupBy(s => s.StyleId!.Value!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal) ?? [];

        var levels = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var id in byId.Keys)
        {
            if (Resolve(id, 0) is > 0 and var level)
            {
                levels[id] = level;
            }
        }

        return levels;

        int Resolve(string id, int depth)
        {
            if (depth > _maxStyleDepth || !byId.TryGetValue(id, out var style))
            {
                return 0;
            }

            var name = style.StyleName?.Val?.Value?.Trim().ToLowerInvariant() ?? string.Empty;
            if (name == "title")
            {
                return 1;
            }

            if (name.StartsWith("heading ", StringComparison.Ordinal) && int.TryParse(name["heading ".Length..], out var n) && n is >= 1 and <= 9)
            {
                return n;
            }

            if (style.StyleParagraphProperties?.OutlineLevel?.Val?.Value is { } outline && outline < 9)
            {
                return outline + 1;
            }

            return style.BasedOn?.Val?.Value is { } parent ? Resolve(parent, depth + 1) : 0;
        }
    }
}
