using MedAssist.Data.Services;
using MedAssist.Data;
using MedAssist.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MedAssist.Tests;

// GetByIcdAsync must run against a REAL relational provider. The EF InMemory provider executes
// LINQ in-process, so it silently accepts query shapes that a SQL provider cannot translate —
// exactly the class of bug this suite guards. SQLite (in-memory) translates to SQL like Postgres
// does, so an untranslatable expression throws here just as it does in production.
public sealed class MedicalDictionaryLookupTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MedAssistDbContext _db;
    private readonly MedicalDictionaryService _sut;

    public MedicalDictionaryLookupTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<MedAssistDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new MedAssistDbContext(options);

        // The model carries Postgres-only store defaults (gen_random_uuid(), now()) that SQLite's
        // EnsureCreated cannot emit, so create just the tables these tests touch, by hand.
        _db.Database.ExecuteSqlRaw(
            "CREATE TABLE illnesses (id TEXT NOT NULL PRIMARY KEY, icd_code TEXT NOT NULL, " +
            "name_en TEXT NOT NULL, name_bg TEXT NOT NULL, created_at TEXT NOT NULL);");
        _db.Database.ExecuteSqlRaw(
            "CREATE TABLE illness_aliases (id TEXT NOT NULL PRIMARY KEY, illness_id TEXT NOT NULL, " +
            "alias TEXT NOT NULL, language TEXT NOT NULL);");

        _sut = new MedicalDictionaryService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private async Task SeedGravesAsync()
    {
        _db.Illnesses.Add(new IllnessEntity
        {
            Id = Guid.NewGuid(),
            IcdCode = "E05.0",
            NameEn = "Graves disease",
            NameBg = "Болест на Гравес",
            CreatedAt = DateTimeOffset.UtcNow,
            Aliases = [new IllnessAliasEntity { Id = Guid.NewGuid(), Alias = "Базедова болест", Language = "bg" }]
        });
        await _db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetByIcdAsync_ExistingCode_ReturnsIllnessWithAliases()
    {
        await SeedGravesAsync();

        var result = await _sut.GetByIcdAsync("E05.0");

        Assert.NotNull(result);
        Assert.Equal("E05.0", result!.IcdCode);
        Assert.Equal("Graves disease", result.NameEn);
        Assert.Contains("Базедова болест", result.Aliases);
    }

    [Fact]
    public async Task GetByIcdAsync_LowerCaseInput_ResolvesToSameIllness()
    {
        await SeedGravesAsync();

        var result = await _sut.GetByIcdAsync("e05.0");

        Assert.NotNull(result);
        Assert.Equal("E05.0", result!.IcdCode);
    }

    [Fact]
    public async Task GetByIcdAsync_UnknownCode_ReturnsNull()
    {
        await SeedGravesAsync();

        var result = await _sut.GetByIcdAsync("Z99.9");

        Assert.Null(result);
    }
}
