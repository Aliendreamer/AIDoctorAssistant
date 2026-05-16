using MedAssist.AI.Ingestion;
using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;

namespace MedAssist.Tests;

public sealed class ChunkEnricherTests
{
    private static ChunkEnricher Make(params IllnessEntry[] illnesses)
        => new(new StubDictionary(illnesses));

    [Fact]
    public async Task EmptyText_ReturnsNoIcdCodes()
    {
        var sut = Make(new IllnessEntry { IcdCode = "E05.0", NameEn = "Graves disease", NameBg = "Болест", Aliases = [] });
        var result = await sut.GetIcdCodesAsync("");
        Assert.Empty(result);
    }

    [Fact]
    public async Task TextContainsEnglishName_ReturnsMatchingCode()
    {
        var sut = Make(new IllnessEntry { IcdCode = "E05.0", NameEn = "Graves disease", NameBg = "Болест на Гравес", Aliases = [] });
        var result = await sut.GetIcdCodesAsync("The patient presents with Graves disease and exophthalmos.");
        Assert.Contains("E05.0", result);
    }

    [Fact]
    public async Task TextContainsBulgarianName_ReturnsMatchingCode()
    {
        var sut = Make(new IllnessEntry { IcdCode = "E05.0", NameEn = "Graves disease", NameBg = "Болест на Гравес", Aliases = [] });
        var result = await sut.GetIcdCodesAsync("Болест на Гравес се характеризира с хипертиреоидизъм.");
        Assert.Contains("E05.0", result);
    }

    [Fact]
    public async Task TextContainsAlias_ReturnsMatchingCode()
    {
        var sut = Make(new IllnessEntry { IcdCode = "E05.0", NameEn = "Graves disease", NameBg = "Болест", Aliases = ["toxic goiter", "Базедова болест"] });
        var result = await sut.GetIcdCodesAsync("Diagnosis: toxic goiter confirmed by scan.");
        Assert.Contains("E05.0", result);
    }

    [Fact]
    public async Task MatchIsCaseInsensitive()
    {
        var sut = Make(new IllnessEntry { IcdCode = "Q07.0", NameEn = "Arnold-Chiari malformation", NameBg = "Малформация", Aliases = [] });
        var result = await sut.GetIcdCodesAsync("ARNOLD-CHIARI MALFORMATION type II.");
        Assert.Contains("Q07.0", result);
    }

    [Fact]
    public async Task NoMatchingIllness_ReturnsEmpty()
    {
        var sut = Make(new IllnessEntry { IcdCode = "E05.0", NameEn = "Graves disease", NameBg = "Болест", Aliases = [] });
        var result = await sut.GetIcdCodesAsync("General pediatric examination, no specific findings.");
        Assert.Empty(result);
    }

    [Fact]
    public async Task MultipleMatchingIllnesses_ReturnsAllCodes()
    {
        var sut = Make(
            new IllnessEntry { IcdCode = "E05.0", NameEn = "Graves disease", NameBg = "Болест", Aliases = [] },
            new IllnessEntry { IcdCode = "Q07.0", NameEn = "Chiari malformation", NameBg = "Малформация", Aliases = [] });
        var result = await sut.GetIcdCodesAsync("Graves disease and Chiari malformation are discussed.");
        Assert.Contains("E05.0", result);
        Assert.Contains("Q07.0", result);
    }

    [Fact]
    public async Task SameIllnessMatchedByNameAndAlias_ReturnsCodeOnce()
    {
        var sut = Make(new IllnessEntry { IcdCode = "E05.0", NameEn = "Graves disease", NameBg = "Болест", Aliases = ["Graves"] });
        var result = await sut.GetIcdCodesAsync("Graves disease (Graves) is autoimmune.");
        Assert.Single(result);
        Assert.Equal("E05.0", result[0]);
    }

    [Fact]
    public async Task WhitespaceAlias_IsNotMatched()
    {
        var sut = Make(new IllnessEntry { IcdCode = "E05.0", NameEn = "Graves disease", NameBg = "Болест", Aliases = ["   "] });
        var result = await sut.GetIcdCodesAsync("Some text about  multiple conditions.");
        Assert.Empty(result);
    }

    [Fact]
    public async Task EmptyDictionary_ReturnsEmpty()
    {
        var sut = Make();
        var result = await sut.GetIcdCodesAsync("Graves disease and Chiari malformation text.");
        Assert.Empty(result);
    }

    private sealed class StubDictionary(params IllnessEntry[] illnesses) : IMedicalDictionary
    {
        public Task<IReadOnlyList<IllnessEntry>> GetAllAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<IllnessEntry>>(illnesses);

        public Task<IReadOnlyList<string>> ExpandQueryAsync(string query, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>([query]);

        public Task<IllnessEntry?> GetByIcdAsync(string icdCode, CancellationToken ct = default)
            => Task.FromResult<IllnessEntry?>(null);

        public Task<IReadOnlyList<IllnessEntry>> SearchAsync(string query, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<IllnessEntry>>([]);
    }
}
