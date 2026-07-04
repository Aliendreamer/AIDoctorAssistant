using MedAssist.Data;
using MedAssist.Data.Entities;
using MedAssist.Data.Repositories;
using MedAssist.Shared.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MedAssist.Tests;

// Extraction status must survive an app restart (audit P1-8 follow-up), so it lives in the DB.
// Runs against real SQLite (not EF InMemory) so the enum<->text conversion and the queries the
// repository emits are actually exercised, mirroring MedicalDictionaryLookupTests.
public sealed class ExtractionStatusRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MedAssistDbContext _db;
    private readonly ExtractionStatusRepository _sut;

    public ExtractionStatusRepositoryTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<MedAssistDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new MedAssistDbContext(options);

        _db.Database.ExecuteSqlRaw(
            "CREATE TABLE extraction_status (book_db_id INTEGER NOT NULL PRIMARY KEY, book_slug TEXT NOT NULL, " +
            "state TEXT NOT NULL, started_at TEXT NOT NULL, completed_at TEXT NULL, error TEXT NULL);");

        _sut = new ExtractionStatusRepository(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task TryStart_FirstTime_CreatesRunningRecord()
    {
        var outcome = await _sut.TryStartAsync(7, "book-a");

        Assert.True(outcome.Started);
        Assert.Equal(ExtractionState.Running, outcome.Entry.State);
        Assert.Equal(7, outcome.Entry.BookDbId);
        Assert.Equal("book-a", outcome.Entry.BookId);
        Assert.Null(outcome.Entry.CompletedAt);

        Assert.True(await _sut.IsRunningAsync(7));
    }

    [Fact]
    public async Task TryStart_WhenAlreadyRunning_ReturnsNotStartedWithInFlightEntry()
    {
        await _sut.TryStartAsync(7, "book-a");

        var outcome = await _sut.TryStartAsync(7, "book-a");

        Assert.False(outcome.Started);
        Assert.Equal(ExtractionState.Running, outcome.Entry.State);
    }

    [Fact]
    public async Task TryStart_AfterFailure_RestartsFresh()
    {
        await _sut.TryStartAsync(7, "book-a");
        await _sut.MarkFailedAsync(7, "boom");

        var outcome = await _sut.TryStartAsync(7, "book-a");

        Assert.True(outcome.Started);
        Assert.Equal(ExtractionState.Running, outcome.Entry.State);
        Assert.Null(outcome.Entry.Error);
        Assert.Null(outcome.Entry.CompletedAt);
    }

    [Fact]
    public async Task MarkDone_TransitionsToDoneWithCompletionTime()
    {
        await _sut.TryStartAsync(7, "book-a");

        await _sut.MarkDoneAsync(7);

        var entry = await _sut.GetAsync(7);
        Assert.Equal(ExtractionState.Done, entry!.State);
        Assert.NotNull(entry.CompletedAt);
        Assert.False(await _sut.IsRunningAsync(7));
    }

    [Fact]
    public async Task MarkFailed_RecordsError()
    {
        await _sut.TryStartAsync(7, "book-a");

        await _sut.MarkFailedAsync(7, "marker exploded");

        var entry = await _sut.GetAsync(7);
        Assert.Equal(ExtractionState.Failed, entry!.State);
        Assert.Equal("marker exploded", entry.Error);
    }

    [Fact]
    public async Task MarkDone_WhenNotStarted_Throws()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.MarkDoneAsync(999));
    }

    [Fact]
    public async Task GetAll_OrderedByStartedAt()
    {
        _db.ExtractionStatuses.Add(new ExtractionStatusEntity
        {
            BookDbId = 2, BookSlug = "later", State = ExtractionState.Done,
            StartedAt = new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero)
        });
        _db.ExtractionStatuses.Add(new ExtractionStatusEntity
        {
            BookDbId = 1, BookSlug = "earlier", State = ExtractionState.Running,
            StartedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        });
        await _db.SaveChangesAsync();

        var all = await _sut.GetAllAsync();

        Assert.Equal(["earlier", "later"], all.Select(e => e.BookId));
    }

    [Fact]
    public async Task MarkInterruptedRunningAsFailed_OnlyAffectsRunningRows()
    {
        await _sut.TryStartAsync(1, "running");
        await _sut.TryStartAsync(2, "done");
        await _sut.MarkDoneAsync(2);

        var count = await _sut.MarkInterruptedRunningAsFailedAsync();

        Assert.Equal(1, count);
        Assert.Equal(ExtractionState.Failed, (await _sut.GetAsync(1))!.State);
        Assert.Equal(ExtractionState.Done, (await _sut.GetAsync(2))!.State);   // untouched
    }
}
