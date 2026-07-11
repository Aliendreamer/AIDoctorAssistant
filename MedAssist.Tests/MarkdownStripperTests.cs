using MedAssist.AI.Plugins;

namespace MedAssist.Tests;

// Guard: the cited-answer-markers change relies on Strip() leaving [n] citation markers intact
// (they must reach the UI to be rendered). These lock that behaviour in.
public sealed class MarkdownStripperTests
{
    [Fact]
    public void Strip_PreservesSingleAndConsecutiveMarkers()
    {
        var result = MarkdownStripper.Strip("Amoxicillin is first line [1]. It is reserved [2][3].");

        Assert.Contains("[1]", result);
        Assert.Contains("[2][3]", result);
    }

    [Fact]
    public void Strip_PreservesCommaGroupedMarkers()
    {
        var result = MarkdownStripper.Strip("Both apply [1, 3] here.");

        Assert.Contains("[1, 3]", result);
    }

    [Fact]
    public void Strip_RemovesBoldButKeepsMarker()
    {
        var result = MarkdownStripper.Strip("**Amoxicillin** is first line [1].");

        Assert.DoesNotContain("**", result);
        Assert.Contains("Amoxicillin", result);
        Assert.Contains("[1]", result);
    }
}
