using System.Text;
using MedAssist.AI.Ingestion;
using MedAssist.Shared.Models;

namespace MedAssist.Tests;

/// <summary>Page ranges resolved by <see cref="MarkdownChunker"/> from page markers (change ingest-docx-and-page-citations).</summary>
public sealed class MarkdownChunkerPageTests
{
    private static string Marker(int pdf, string? printed = null) => PageMarker.Format(new PageRef(pdf, printed));

    // 34 chars each with the joining space; the splitter fits 60 of these into one 512-token chunk.
    private static string Sentences(string word, int count)
    {
        var sb = new StringBuilder();
        for (var i = 1; i <= count; i++)
        {
            sb.Append($"{word} sentence padding text {i:D4}. ");
        }

        return sb.ToString().TrimEnd();
    }

    [Fact]
    public void ChunkWithinOnePage_HasThatPage()
    {
        var md = Marker(10, "5") + "\n# Heading\n\n" + Sentences("Alpha", 10);

        var chunk = Assert.Single(new MarkdownChunker().Chunk(md));

        Assert.Equal(new PageRange(10, 10, "5", "5"), chunk.Pages);
    }

    [Fact]
    public void ChunkCrossingMarker_SpansBothPages_AndHidesMarkers()
    {
        var md = "# Heading\n" + Marker(1, "1804") + "\n" + Sentences("Alpha", 10) + "\n" + Marker(2, "1805") + "\n" + Sentences("Beta", 10);

        var chunk = Assert.Single(new MarkdownChunker().Chunk(md));

        Assert.Equal(new PageRange(1, 2, "1804", "1805"), chunk.Pages);
        Assert.DoesNotContain("<!--", chunk.Text);
        Assert.Contains("Alpha", chunk.Text);
        Assert.Contains("Beta", chunk.Text);
    }

    [Fact]
    public void LargeSectionSplitAtPageBreak_EachPieceHasItsOwnPage_OverlapDoesNotWiden()
    {
        var md = "# Heading\n" + Marker(1) + "\n" + Sentences("Alpha", 60) + "\n" + Marker(2) + "\n" + Sentences("Beta", 30);

        var chunks = new MarkdownChunker().Chunk(md);

        Assert.Equal(2, chunks.Count);
        Assert.Equal(new PageRange(1, 1, null, null), chunks[0].Pages);
        // The second piece carries overlap text from page 1, but its own text starts on page 2.
        Assert.Contains("Alpha", chunks[1].Text);
        Assert.Equal(new PageRange(2, 2, null, null), chunks[1].Pages);
        Assert.All(chunks, c => Assert.DoesNotContain("<!--", c.Text));
    }

    [Fact]
    public void PageCarriesAcrossHeadings()
    {
        var md = Marker(7) + "\n# One\n\n" + Sentences("Alpha", 10) + "\n# Two\n\n" + Sentences("Beta", 10);

        var chunks = new MarkdownChunker().Chunk(md);

        Assert.Equal(2, chunks.Count);
        Assert.All(chunks, c => Assert.Equal(new PageRange(7, 7, null, null), c.Pages));
    }

    [Fact]
    public void MergedSmallChunk_CombinesRanges()
    {
        var md = "# Heading\n" + Marker(1, "10") + "\nShort opening line.\n# Heading\n" + Marker(2, "11") + "\n" + Sentences("Beta", 10);

        var chunk = Assert.Single(new MarkdownChunker(overlapChars: 0).Chunk(md));

        Assert.Equal(new PageRange(1, 2, "10", "11"), chunk.Pages);
    }

    [Fact]
    public void UncertainStretch_CitesPdfRangeInsteadOfPrintedPage()
    {
        // The translator noted PDF 2637 (printed 1805.e1) and nothing after it, though the file runs to PDF 2640.
        var md = "# Глава 332\n" + PageMarker.Format(new PageRef(2637, "1805.e1", 2640)) + "\n" + Sentences("Alpha", 10);

        var chunk = Assert.Single(new MarkdownChunker().Chunk(md));

        Assert.Equal(new PageRange(2637, 2640, null, null), chunk.Pages);
    }

    [Fact]
    public void MergeWithUncertainChunk_DropsPrintedPages()
    {
        var md = "# Heading\n" + Marker(1, "10") + "\nShort opening line.\n# Heading\n"
            + PageMarker.Format(new PageRef(2, "11", 4)) + "\n" + Sentences("Beta", 10);

        var chunk = Assert.Single(new MarkdownChunker(overlapChars: 0).Chunk(md));

        Assert.Equal(new PageRange(1, 4, null, null), chunk.Pages);
    }

    [Fact]
    public void MarkdownWithoutMarkers_HasUnknownPages()
    {
        var chunks = new MarkdownChunker().Chunk("# Heading\n\n" + Sentences("Alpha", 10));

        Assert.All(chunks, c => Assert.Equal(PageRange.Unknown, c.Pages));
    }
}
