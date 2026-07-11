using MedAssist.Web.Services;

namespace MedAssist.Tests;

public sealed class CitationMarkersTests
{
    private static IEnumerable<int> Markers(IEnumerable<AnswerSegment> segments)
        => segments.Where(s => s.CitationNumber.HasValue).Select(s => s.CitationNumber!.Value);

    private static string PlainText(IEnumerable<AnswerSegment> segments)
        => string.Concat(segments.Where(s => !s.CitationNumber.HasValue).Select(s => s.Text));

    [Fact]
    public void InRangeMarker_BecomesMarkerSegment()
    {
        var segments = CitationMarkers.Parse("Amoxicillin is first line [2].", sourceCount: 3);

        Assert.Equal([2], Markers(segments));
        Assert.Equal("Amoxicillin is first line .", PlainText(segments));
    }

    [Fact]
    public void OutOfRangeMarker_RendersAsPlainText()
    {
        var segments = CitationMarkers.Parse("See the guideline [9].", sourceCount: 3);

        Assert.Empty(Markers(segments));
        Assert.Equal("See the guideline [9].", PlainText(segments));
    }

    [Fact]
    public void CommaGroup_AllInRange_YieldsMultipleMarkers()
    {
        var segments = CitationMarkers.Parse("Both apply [1, 3] here.", sourceCount: 3);

        Assert.Equal([1, 3], Markers(segments));
    }

    [Fact]
    public void ConsecutiveMarkers_YieldMultipleMarkers()
    {
        var segments = CitationMarkers.Parse("Established [1][2] widely.", sourceCount: 3);

        Assert.Equal([1, 2], Markers(segments));
    }

    [Fact]
    public void CommaGroup_WithOutOfRangeNumber_RendersWholeGroupAsText()
    {
        var segments = CitationMarkers.Parse("Mixed [1, 9] group.", sourceCount: 3);

        Assert.Empty(Markers(segments));
        Assert.Contains("[1, 9]", PlainText(segments));
    }

    [Fact]
    public void NoMarkers_ReturnsSingleTextSegment()
    {
        var segments = CitationMarkers.Parse("A plain clinical answer.", sourceCount: 3);

        Assert.Empty(Markers(segments));
        Assert.Equal("A plain clinical answer.", PlainText(segments));
    }

    [Fact]
    public void ZeroSources_RendersMarkersAsPlainText()
    {
        var segments = CitationMarkers.Parse("Has a [1] marker.", sourceCount: 0);

        Assert.Empty(Markers(segments));
        Assert.Equal("Has a [1] marker.", PlainText(segments));
    }

    [Fact]
    public void EmptyContent_ReturnsNoSegments()
    {
        var segments = CitationMarkers.Parse("", sourceCount: 3);

        Assert.Empty(segments);
    }
}
