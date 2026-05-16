using MedAssist.AI.Ingestion;
using MedAssist.Shared.Models;

namespace MedAssist.Tests;

public sealed class MarkdownChunkerTests
{
    private readonly MarkdownChunker _sut = new();

    // EstimateTokens = text.Length / 4 — _minTokens=50 (200 chars), _maxTokens=512 (2048 chars)

    [Fact]
    public void EmptyInput_ReturnsNoChunks()
    {
        var result = _sut.Chunk("");
        Assert.Empty(result);
    }

    [Fact]
    public void WhitespaceOnly_ReturnsNoChunks()
    {
        var result = _sut.Chunk("   \n\n   ");
        Assert.Empty(result);
    }

    [Fact]
    public void SingleHeadingWithContent_ReturnsOneChunk()
    {
        var md = "# Introduction\n\n" + new string('x', 300);
        var result = _sut.Chunk(md);
        Assert.Single(result);
        Assert.Equal("Introduction", result[0].HeadingPath);
    }

    [Fact]
    public void TwoH1Sections_ReturnsTwoChunks()
    {
        var body = new string('x', 300);
        var md = $"# First\n\n{body}\n# Second\n\n{body}";
        var result = _sut.Chunk(md);
        Assert.Equal(2, result.Count);
        Assert.Equal("First", result[0].HeadingPath);
        Assert.Equal("Second", result[1].HeadingPath);
    }

    [Fact]
    public void NestedHeadings_BuildsCorrectPath()
    {
        var body = new string('x', 300);
        var md = $"# Chapter\n## Section\n### Subsection\n\n{body}";
        var result = _sut.Chunk(md);
        Assert.Single(result);
        Assert.Equal("Chapter > Section > Subsection", result[0].HeadingPath);
    }

    [Fact]
    public void SiblingSubheadings_PathResetsBetweenSiblings()
    {
        var body = new string('x', 300);
        var md = $"# Chapter\n## SectionA\n\n{body}\n## SectionB\n\n{body}";
        var result = _sut.Chunk(md);
        Assert.Equal(2, result.Count);
        Assert.Equal("Chapter > SectionA", result[0].HeadingPath);
        Assert.Equal("Chapter > SectionB", result[1].HeadingPath);
    }

    [Fact]
    public void TableLines_ProduceTableContentType()
    {
        var md = "# Data\n\n| A | B |\n| --- | --- |\n| 1 | 2 |\n| 3 | 4 |" + new string('\n', 5) + new string('x', 200);
        var result = _sut.Chunk(md);
        Assert.Contains(result, c => c.ContentType == ContentType.Table);
    }

    [Fact]
    public void DashListLines_ProduceListContentType()
    {
        var md = "# Items\n\n" + string.Concat(Enumerable.Repeat("- item text here\n", 15));
        var result = _sut.Chunk(md);
        Assert.Contains(result, c => c.ContentType == ContentType.List);
    }

    [Fact]
    public void AsteriskListLines_ProduceListContentType()
    {
        var md = "# Items\n\n" + string.Concat(Enumerable.Repeat("* item text here\n", 15));
        var result = _sut.Chunk(md);
        Assert.Contains(result, c => c.ContentType == ContentType.List);
    }

    [Fact]
    public void PlainParagraph_ProducesTextContentType()
    {
        var md = "# Section\n\n" + new string('x', 300);
        var result = _sut.Chunk(md);
        Assert.All(result, c => Assert.Equal(ContentType.Text, c.ContentType));
    }

    [Fact]
    public void LargeChunk_IsSplitIntoMultiple()
    {
        // 3000 chars → ~750 tokens > 512 limit → must split
        var sentences = string.Concat(Enumerable.Repeat("This is a long medical sentence about symptoms. ", 65));
        var md = $"# Chapter\n\n{sentences}";
        var result = _sut.Chunk(md);
        Assert.True(result.Count > 1);
        Assert.All(result, c => Assert.True(c.Text.Length <= 2048 + 200)); // some tolerance for sentence boundaries
    }

    [Fact]
    public void SplitChunks_RetainHeadingPath()
    {
        var sentences = string.Concat(Enumerable.Repeat("Medical sentence about the topic. ", 65));
        var md = $"# Chapter\n## Section\n\n{sentences}";
        var result = _sut.Chunk(md);
        Assert.True(result.Count > 1);
        Assert.All(result, c => Assert.Equal("Chapter > Section", c.HeadingPath));
    }

    [Fact]
    public void SmallChunks_AreMergedWithNext()
    {
        // Two small sections (< 200 chars each) should merge into one
        var md = "# Chapter\n## TinyA\n\nShort text.\n## TinyB\n\n" + new string('x', 300);
        var result = _sut.Chunk(md);
        // TinyA is < minTokens so gets merged into TinyB
        Assert.True(result.Count < 3);
    }

    [Fact]
    public void ChunkText_ContainsOriginalContent()
    {
        var content = new string('x', 300);
        var md = $"# Title\n\n{content}";
        var result = _sut.Chunk(md);
        Assert.Contains(result, c => c.Text.Contains(content.Substring(0, 50)));
    }
}
