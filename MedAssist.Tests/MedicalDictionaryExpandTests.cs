using MedAssist.AI.Dictionary;
using MedAssist.Data;
using MedAssist.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MedAssist.Tests;

public sealed class MedicalDictionaryExpandTests : IDisposable
{
    private readonly MedAssistDbContext _db;
    private readonly MedicalDictionaryService _sut;

    public MedicalDictionaryExpandTests()
    {
        var options = new DbContextOptionsBuilder<MedAssistDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new MedAssistDbContext(options);

        var factory = new InMemoryDbContextFactory(options);
        _sut = new MedicalDictionaryService(factory);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task EmptyDatabase_ReturnsOriginalQueryOnly()
    {
        var result = await _sut.ExpandQueryAsync("fever");
        Assert.Contains("fever", result);
    }

    [Fact]
    public async Task PhraseQuery_AddsExtractedKeywordsAsSearchTerms()
    {
        // "болест на гравес" → stopword "на" filtered, short "болест" filtered → "гравес" added
        var result = await _sut.ExpandQueryAsync("болест на гравес");
        Assert.Contains("болест на гравес", result);
        Assert.Contains("гравес", result);
    }

    [Fact]
    public async Task StopwordsAreNotAddedAsTerms()
    {
        var result = await _sut.ExpandQueryAsync("болест на гравес");
        Assert.DoesNotContain("на", result);
    }

    [Fact]
    public async Task ShortWordsUnderLengthThreshold_NotAddedAsTerms()
    {
        // "flu" is 3 chars — length check is > 3 so exactly 3 is excluded
        var result = await _sut.ExpandQueryAsync("flu на е");
        Assert.DoesNotContain("flu", result);
    }

    [Fact]
    public async Task SingleMeaningfulWord_NoExtraTerms()
    {
        var result = await _sut.ExpandQueryAsync("хипертиреоидизъм");
        // Single word is both the full query and potentially a keyword — no duplicates
        Assert.Contains("хипертиреоидизъм", result);
        Assert.Equal(result.Count, result.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public async Task DictionaryMatch_ExpandsToAllAliases()
    {
        _db.Illnesses.Add(new IllnessEntity
        {
            IcdCode = "E05.0",
            NameEn = "Graves disease",
            NameBg = "Болест на Гравес",
            Aliases = [new IllnessAliasEntity { Alias = "Базедова болест" }, new IllnessAliasEntity { Alias = "гравес" }]
        });
        await _db.SaveChangesAsync();

        var result = await _sut.ExpandQueryAsync("болест на гравес");

        Assert.Contains(result, t => t.Equals("Graves disease", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result, t => t.Equals("Болест на Гравес", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result, t => t.Equals("Базедова болест", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DictionaryMatchOnKeyword_ExpandsFromExtractedWord()
    {
        // Dictionary has entry for "гравес" as an alias
        _db.Illnesses.Add(new IllnessEntity
        {
            IcdCode = "E05.0",
            NameEn = "Graves disease",
            NameBg = "Болест на Гравес",
            Aliases = [new IllnessAliasEntity { Alias = "гравес" }]
        });
        await _db.SaveChangesAsync();

        // Query is the full phrase — keyword "гравес" is extracted, matched in DB, expands
        var result = await _sut.ExpandQueryAsync("болест на гравес");

        Assert.Contains("Graves disease", result);
    }

    [Fact]
    public async Task ResultContainsNoDuplicates()
    {
        var result = await _sut.ExpandQueryAsync("болест на гравес");
        Assert.Equal(result.Count, result.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    private sealed class InMemoryDbContextFactory(DbContextOptions<MedAssistDbContext> options)
        : IDbContextFactory<MedAssistDbContext>
    {
        public MedAssistDbContext CreateDbContext() => new(options);

        public Task<MedAssistDbContext> CreateDbContextAsync(CancellationToken ct = default)
            => Task.FromResult(new MedAssistDbContext(options));
    }
}
