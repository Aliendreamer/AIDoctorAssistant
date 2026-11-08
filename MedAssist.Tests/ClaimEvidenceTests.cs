using MedAssist.AI.Evaluation;
using MedAssist.Shared.Models.Evaluation;

namespace MedAssist.Tests;

public sealed class ClaimEvidenceTests
{
    private static readonly EvalExcerpt[] _excerpts =
    [
        new() { Number = 1, Text = "Нито деконгестантите, нито антихистамините подобряват състоянието." },
        new() { Number = 2, Text = "Лечението включва промивка. Дозировката му е 0,14 g/kg, а след това на всеки 4 часа по 0,07 g/kg до 68-ия час.\n- груб стридор" },
    ];

    [Fact]
    public void FindExcerpts_MatchesACopiedSentenceDespiteOcrNoiseAndMarkup()
    {
        var quote = "**Нито деконгестантите, нито антихистамините подобряват   състоянието**";

        Assert.Equal([1], ClaimEvidence.FindExcerpts(quote, _excerpts));
    }

    [Fact]
    public void FindExcerpts_ToleratesOneMisreadWordInALongQuote()
    {
        var quote = "Нито деконгестантите нито антихистамините подобряват състоянието днес";

        Assert.Equal([1], ClaimEvidence.FindExcerpts(quote, _excerpts));
    }

    [Theory]
    [InlineData("It is important to note that neither decongestants nor antihistamines help")]
    [InlineData("стридор")]
    [InlineData("")]
    public void FindExcerpts_RejectsWhatTheExcerptsDoNotContain(string quote) =>
        Assert.Empty(ClaimEvidence.FindExcerpts(quote, _excerpts));

    [Fact]
    public void FindExcerpts_AcceptsAShortListItem() =>
        Assert.Equal([2], ClaimEvidence.FindExcerpts("- груб стридор", _excerpts));

    [Fact]
    public void Numbers_NormalisesDecimalsAndIgnoresDigitsInsideNames()
    {
        Assert.Equal(
            new HashSet<string> { "0.14", "0.07", "5", "1", "2" },
            ClaimEvidence.Numbers("G6PD: 0,14 g/kg, 0.07 g/kg, 5% for 1-2 h").ToHashSet());
    }

    [Fact]
    public void MissingNumbers_ListsNumbersTheEvidenceLacks()
    {
        Assert.Equal(["0.41"], ClaimEvidence.MissingNumbers("0.41 g/kg every 4 hours", ["0,14 g/kg на всеки 4 часа"]));
        Assert.Empty(ClaimEvidence.MissingNumbers("0.14 g/kg every 4 hours", ["0,14 g/kg на всеки 4 часа"]));
    }

    [Fact]
    public void Gather_DropsQuotesThatAreNotInTheExcerpts()
    {
        var quote = "Нито деконгестантите, нито антихистамините подобряват състоянието.";

        var evidence = ClaimEvidence.Gather("no numbers here", ["The model repeated the statement back."], _excerpts);
        Assert.Empty(evidence);

        evidence = ClaimEvidence.Gather("no numbers here", ["The model repeated the statement back.", quote], _excerpts);
        Assert.Equal([quote], evidence);
    }

    [Fact]
    public void Gather_AddsTheSentenceHoldingTheStatementsNumbers()
    {
        var evidence = ClaimEvidence.Gather("0.14 g/kg then 0.07 g/kg every 4 hours", ["Лечението включва промивка."], _excerpts);

        Assert.Equal(2, evidence.Count);
        Assert.Contains("0,14 g/kg", evidence[1]);
        Assert.Empty(ClaimEvidence.MissingNumbers("0.14 g/kg then 0.07 g/kg every 4 hours", evidence));
    }

    [Fact]
    public void Gather_DoesNotInventEvidenceWhenNoQuoteSurvives() =>
        Assert.Empty(ClaimEvidence.Gather("0.14 g/kg every 4 hours", [], _excerpts));
}
