using MedAssist.Data.Services;
using MedAssist.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MedAssist.Tests;

// The per-book BM25 contribution must fold into the global aggregates as a DELTA versus the book's
// previously stored contribution, so re-index and resume never inflate document frequencies
// (audit P2-1/P2-2). Runs against real SQLite (not EF InMemory) so the SQL the store emits is
// actually exercised, mirroring MedicalDictionaryLookupTests.
public sealed class Bm25BookContributionTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MedAssistDbContext _db;
    private readonly BM25VocabService _sut;

    public Bm25BookContributionTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<MedAssistDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new MedAssistDbContext(options);

        // Postgres-only store defaults (now(), identity-always) can't be emitted by SQLite's
        // EnsureCreated, so create the four BM25 tables by hand.
        _db.Database.ExecuteSqlRaw(
            "CREATE TABLE bm25_vocab (id INTEGER PRIMARY KEY AUTOINCREMENT, term TEXT NOT NULL UNIQUE, " +
            "document_frequency INTEGER NOT NULL DEFAULT 0, updated_at TEXT NOT NULL);");
        _db.Database.ExecuteSqlRaw(
            "CREATE TABLE bm25_stats (id INTEGER PRIMARY KEY, total_documents INTEGER NOT NULL DEFAULT 0, " +
            "updated_at TEXT NOT NULL);");
        _db.Database.ExecuteSqlRaw(
            "CREATE TABLE bm25_book_terms (book_id TEXT NOT NULL, term TEXT NOT NULL, " +
            "document_frequency INTEGER NOT NULL DEFAULT 0, PRIMARY KEY (book_id, term));");
        _db.Database.ExecuteSqlRaw(
            "CREATE TABLE bm25_book_stats (book_id TEXT NOT NULL PRIMARY KEY, chunk_count INTEGER NOT NULL DEFAULT 0, " +
            "updated_at TEXT NOT NULL);");

        _sut = new BM25VocabService(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private static Dictionary<string, int> Terms(params (string term, int df)[] terms)
        => terms.ToDictionary(t => t.term, t => t.df);

    private async Task<int> GlobalDfAsync(string term) =>
        await _db.Bm25Vocab.AsNoTracking().Where(v => v.Term == term)
            .Select(v => v.DocumentFrequency).FirstOrDefaultAsync();

    private async Task<bool> GlobalHasTermAsync(string term) =>
        await _db.Bm25Vocab.AsNoTracking().AnyAsync(v => v.Term == term);

    private async Task<int> GlobalTotalAsync() =>
        await _db.Bm25Stats.AsNoTracking().Where(s => s.Id == 1)
            .Select(s => s.TotalDocuments).FirstOrDefaultAsync();

    [Fact]
    public async Task FirstBook_PopulatesGlobalAggregatesAndPerBookContribution()
    {
        await _sut.ApplyBookContributionAsync("book-a", Terms(("fever", 3), ("cough", 2)), chunkCount: 5);

        Assert.Equal(3, await GlobalDfAsync("fever"));
        Assert.Equal(2, await GlobalDfAsync("cough"));
        Assert.Equal(5, await GlobalTotalAsync());

        var bookRows = await _db.Bm25BookTerms.AsNoTracking()
            .Where(t => t.BookId == "book-a").ToDictionaryAsync(t => t.Term, t => t.DocumentFrequency);
        Assert.Equal(3, bookRows["fever"]);
        Assert.Equal(2, bookRows["cough"]);

        var chunkCount = await _db.Bm25BookStats.AsNoTracking()
            .Where(s => s.BookId == "book-a").Select(s => s.ChunkCount).FirstOrDefaultAsync();
        Assert.Equal(5, chunkCount);
    }

    [Fact]
    public async Task ReapplyingSameContribution_IsIdempotent()
    {
        await _sut.ApplyBookContributionAsync("book-a", Terms(("fever", 3), ("cough", 2)), chunkCount: 5);
        await _sut.ApplyBookContributionAsync("book-a", Terms(("fever", 3), ("cough", 2)), chunkCount: 5);

        Assert.Equal(3, await GlobalDfAsync("fever"));
        Assert.Equal(2, await GlobalDfAsync("cough"));
        Assert.Equal(5, await GlobalTotalAsync());

        // Exactly one row per (book, term) — no duplication on re-apply.
        var rowCount = await _db.Bm25BookTerms.AsNoTracking().CountAsync(t => t.BookId == "book-a");
        Assert.Equal(2, rowCount);
    }

    [Fact]
    public async Task SecondBookAddsToGlobal_AndReindexingItAppliesDeltaLeavingFirstBookIntact()
    {
        await _sut.ApplyBookContributionAsync("book-a", Terms(("fever", 3), ("cough", 2)), chunkCount: 5);
        await _sut.ApplyBookContributionAsync("book-b", Terms(("fever", 4), ("rash", 1)), chunkCount: 6);

        Assert.Equal(7, await GlobalDfAsync("fever"));   // 3 + 4
        Assert.Equal(11, await GlobalTotalAsync());       // 5 + 6

        // Re-index book-b with changed content: fever 4->2, rash stays, itch is new.
        await _sut.ApplyBookContributionAsync("book-b", Terms(("fever", 2), ("rash", 1), ("itch", 5)), chunkCount: 6);

        Assert.Equal(5, await GlobalDfAsync("fever"));   // 7 - 2
        Assert.Equal(1, await GlobalDfAsync("rash"));    // unchanged
        Assert.Equal(5, await GlobalDfAsync("itch"));    // new
        Assert.Equal(11, await GlobalTotalAsync());       // chunk count unchanged

        // book-a's contribution is untouched.
        Assert.Equal(2, await GlobalDfAsync("cough"));   // only book-a had it
    }

    [Fact]
    public async Task ReindexDroppingTerm_RemovesGlobalRowWhenContributionReachesZero()
    {
        await _sut.ApplyBookContributionAsync("book-a", Terms(("fever", 3), ("rash", 2)), chunkCount: 5);

        // Re-index without "rash": its only contributor drops it, so the global row should go away.
        await _sut.ApplyBookContributionAsync("book-a", Terms(("fever", 3)), chunkCount: 5);

        Assert.Equal(3, await GlobalDfAsync("fever"));
        Assert.False(await GlobalHasTermAsync("rash"));

        // The per-book row for the dropped term is gone too.
        Assert.False(await _db.Bm25BookTerms.AsNoTracking().AnyAsync(t => t.BookId == "book-a" && t.Term == "rash"));
    }
}
