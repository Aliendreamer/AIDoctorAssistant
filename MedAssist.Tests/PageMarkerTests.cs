using MedAssist.Shared.Models;

namespace MedAssist.Tests;

public sealed class PageMarkerTests
{
    [Theory]
    [InlineData(2635, "1804")]
    [InlineData(2635, "1804.e1")]
    [InlineData(530, null)]
    public void Format_ThenParse_RoundTrips(int pdfPage, string? printedPage)
    {
        var line = PageMarker.Format(new PageRef(pdfPage, printedPage));

        Assert.True(PageMarker.TryParse(line, out var parsed));
        Assert.Equal(pdfPage, parsed.PdfPage);
        Assert.Equal(printedPage, parsed.PrintedPage);
    }

    [Fact]
    public void Format_WithPrintedPage_IsHtmlComment()
    {
        Assert.Equal("<!-- page pdf=2635 printed=1804.e1 -->", PageMarker.Format(new PageRef(2635, "1804.e1")));
    }

    [Fact]
    public void Format_WithUncertainStretch_RoundTripsUntil()
    {
        var line = PageMarker.Format(new PageRef(2637, "1805.e1", 2640));

        Assert.Equal("<!-- page pdf=2637 printed=1805.e1 until=2640 -->", line);
        Assert.True(PageMarker.TryParse(line, out var parsed));
        Assert.Equal(new PageRef(2637, "1805.e1", 2640), parsed);
        Assert.Equal([new PageRef(2637, "1805.e1", 2640)], PageMarker.FindAll("text " + line).Select(f => f.Page));
        Assert.Equal("text", PageMarker.Strip("text\n" + line));
    }

    [Fact]
    public void Format_WithoutPrintedPage_OmitsIt()
    {
        Assert.Equal("<!-- page pdf=530 -->", PageMarker.Format(new PageRef(530, null)));
    }

    [Theory]
    [InlineData("Some text about page 12")]
    [InlineData("<!-- comment -->")]
    [InlineData("")]
    public void TryParse_RejectsNonMarkers(string line)
    {
        Assert.False(PageMarker.TryParse(line, out _));
    }

    [Fact]
    public void Strip_RemovesMarkerLinesOnly()
    {
        var text = "Before\n<!-- page pdf=2 printed=10 -->\nAfter";

        Assert.Equal("Before\nAfter", PageMarker.Strip(text));
    }

    [Fact]
    public void Strip_RemovesInlineMarker()
    {
        // Sentence splitting joins lines with spaces, so a marker can end up mid-line.
        Assert.Equal("End of page. Next page", PageMarker.Strip("End of page. <!-- page pdf=3 --> Next page"));
    }

    [Fact]
    public void FindAll_ReturnsMarkersInOrderAndPosition()
    {
        var found = PageMarker.FindAll("a <!-- page pdf=3 --> b\n<!-- page pdf=4 printed=9 -->").ToList();

        Assert.Equal([new PageRef(3, null), new PageRef(4, "9")], found.Select(f => f.Page));
        Assert.Equal(2, found[0].Index);
    }
}

public sealed class PageLabelTests
{
    [Theory]
    [InlineData(2635, 2636, "1804", "1805", "pp. 1804–1805")]
    [InlineData(2635, 2635, "1804", "1804", "p. 1804")]
    [InlineData(2635, 2635, "1804.e1", null, "p. 1804.e1")]
    [InlineData(2635, 2635, null, null, "PDF p. 2635")]
    [InlineData(2635, 2637, null, null, "PDF pp. 2635–2637")]
    [InlineData(0, 0, null, null, "")]
    public void Format_PicksBestKnownPage(int pdfStart, int pdfEnd, string? printedStart, string? printedEnd, string expected)
    {
        Assert.Equal(expected, PageLabel.Format(pdfStart, pdfEnd, printedStart, printedEnd));
    }

    [Fact]
    public void Format_NullPdfPages_IsEmpty()
    {
        Assert.Equal(string.Empty, PageLabel.Format(null, null, null, null));
    }
}
