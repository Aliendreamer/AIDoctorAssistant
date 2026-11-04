using System.Text;
using MedAssist.AI.Evaluation;
using MedAssist.AI.Plugins;
using MedAssist.Shared.Models;
using MedAssist.Shared.Models.Evaluation;

namespace MedAssist.Tests;

public sealed class GoldenSetTests
{
    private static GoldenSet Parse(string json) => GoldenSetLoader.Parse(Encoding.UTF8.GetBytes(json));

    [Fact]
    public void Embedded_LoadsAndKeepsTheOriginalQuestions()
    {
        var golden = GoldenSetLoader.Embedded;
        var ids = golden.Cases.Select(c => c.Id).ToHashSet();

        Assert.True(ids.IsSupersetOf(Enumerable.Range(1, 12).Select(i => $"G{i}")));
        Assert.True(ids.IsSupersetOf(Enumerable.Range(1, 6).Select(i => $"E{i}")));
        Assert.Equal(12, golden.Hash.Length);
    }

    [Fact]
    public void Embedded_G12FollowsG11()
    {
        var followUp = GoldenSetLoader.Embedded.Cases.Single(c => c.Id == "G12");

        Assert.Equal("G11", followUp.FollowUpOf);
        Assert.Equal(QueryType.Treatment, followUp.QueryType);
    }

    [Fact]
    public void Embedded_HasUnanswerableQuestionsInBothLanguages()
    {
        var abstain = GoldenSetLoader.Embedded.Cases.Where(c => c.Expectation == EvalExpectation.Abstain).ToList();

        Assert.Contains(abstain, c => c.Language == "BG");
        Assert.Contains(abstain, c => c.Language == "EN");
    }

    [Fact]
    public void Embedded_RefusalsCoverInventedDrugsAndRealRareDiseasesGuardAgainstOverRefusal()
    {
        var cases = GoldenSetLoader.Embedded.Cases;

        Assert.Equal(10, cases.Count(c => c.Expectation == EvalExpectation.Abstain));
        Assert.Contains(cases, c => c.Id == "N9" && ConditionNameGuard.ExtractNames(c.Question).Count == 0);
        Assert.All(cases.Where(c => c.Id.StartsWith('R')), c => Assert.Equal(EvalExpectation.Answer, c.Expectation));
    }

    [Theory]
    [InlineData("E1", "Системен васкулит. Болест на Kawasaki")]
    [InlineData("G1", "Болест на Kawasaki")]
    [InlineData("E6", "Отравяне с paracetamol")]
    public void Embedded_RelevanceRecognisesLatinEponymsUsedByTheBooks(string caseId, string text)
    {
        var evalCase = GoldenSetLoader.Embedded.Cases.Single(c => c.Id == caseId);

        Assert.True(CaseScorer.IsRelevant(evalCase, new TracedChunk { Text = text }));
    }

    [Fact]
    public void Embedded_NothingIsMarkedReviewedYet()
    {
        // These references were drafted without the books; only a physician may flip this.
        Assert.Equal(0, GoldenSetLoader.Embedded.ReviewedCount);
    }

    [Fact]
    public void Hash_ChangesWithTheContent()
    {
        var one = Parse("""{"cases":[]}""");
        var two = Parse("""{"cases": []}""");

        Assert.NotEqual(one.Hash, two.Hash);
    }

    [Theory]
    [InlineData("""{"cases":[{"id":"A","language":"EN","question":"q","relevantTerms":[["x"]],"requiredFacts":["f"]},{"id":"A","language":"EN","question":"q","relevantTerms":[["x"]],"requiredFacts":["f"]}]}""")]
    [InlineData("""{"cases":[{"id":"A","language":"EN","question":"q","followUpOf":"B","relevantTerms":[["x"]],"requiredFacts":["f"]}]}""")]
    [InlineData("""{"cases":[{"id":"A","language":"FR","question":"q","relevantTerms":[["x"]],"requiredFacts":["f"]}]}""")]
    [InlineData("""{"cases":[{"id":"A","language":"EN","question":"q"}]}""")]
    [InlineData("""{"cases":[{"id":"A","language":"EN","question":"q","relevantTerms":[["Upper"]],"requiredFacts":["f"]}]}""")]
    public void Parse_RejectsInvalidSets(string json)
    {
        Assert.Throws<InvalidDataException>(() => Parse(json));
    }
}
