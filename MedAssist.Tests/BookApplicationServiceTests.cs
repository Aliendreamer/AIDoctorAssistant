using System.Text;
using MedAssist.Data;
using MedAssist.Data.Entities;
using MedAssist.Data.Repositories;
using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;
using MedAssist.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace MedAssist.Tests;

// Characterization tests for the upload + index-trigger logic extracted out of the endpoints into
// BookApplicationService (audit P1-12). Real SQLite + temp directories exercise the file write, the
// %PDF- validation, the DB upsert, and the atomic in-progress claim; a fake vector store stands in
// for Qdrant. Without MapEnum (SQLite), BookStatus persists as its int value (Pending=0, InProgress=1).
public sealed class BookApplicationServiceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly MedAssistDbContext _db;
    private readonly string _tempRoot;
    private readonly string _pdfDir;
    private readonly string _mdDir;
    private readonly IngestionQueue _queue = new();
    private readonly BookApplicationService _sut;

    public BookApplicationServiceTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        _db = new MedAssistDbContext(new DbContextOptionsBuilder<MedAssistDbContext>().UseSqlite(_connection).Options);

        _db.Database.ExecuteSqlRaw(
            "CREATE TABLE books (id INTEGER PRIMARY KEY AUTOINCREMENT, book_id TEXT NOT NULL UNIQUE, title TEXT NOT NULL, " +
            "author TEXT NOT NULL, language TEXT NOT NULL, edition TEXT NOT NULL DEFAULT '', file_path TEXT NOT NULL DEFAULT '', " +
            "total_chunks INTEGER NOT NULL DEFAULT 0, status INTEGER NOT NULL DEFAULT 0, indexed_at TEXT NULL, \"Outline\" TEXT NULL);");
        _db.Database.ExecuteSqlRaw(
            "CREATE TABLE ingestion_checkpoints (book_id TEXT NOT NULL PRIMARY KEY, total_chunks INTEGER NOT NULL DEFAULT 0, " +
            "indexed_chunks INTEGER NOT NULL DEFAULT 0, last_chunk_index INTEGER NOT NULL DEFAULT -1, status INTEGER NOT NULL, " +
            "updated_at TEXT NOT NULL);");

        _tempRoot = Path.Combine(Path.GetTempPath(), "medassist-tests-" + Guid.NewGuid().ToString("N"));
        _pdfDir = Path.Combine(_tempRoot, "pdf");
        _mdDir = Path.Combine(_tempRoot, "md");
        Directory.CreateDirectory(_pdfDir);
        Directory.CreateDirectory(_mdDir);

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Books:PdfPath"] = _pdfDir,
            ["Books:MdPath"] = _mdDir
        }).Build();

        _sut = new BookApplicationService(
            _db, new BookRepository(_db), new CheckpointRepository(_db),
            new FakeVectorStore(), _queue, config, NullLogger<BookApplicationService>.Instance);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private static Stream Bytes(string content) => new MemoryStream(Encoding.ASCII.GetBytes(content));

    [Fact]
    public async Task Upload_InvalidBookId_RejectedWithoutWritingAnything()
    {
        var result = await _sut.UploadAsync(new UploadBookInput(
            "../evil", "T", "A", "en", "", Bytes("%PDF-1.4 body"), "x.pdf"));

        Assert.False(result.Success);
        Assert.Equal(UploadError.InvalidBookId, result.Error);
        Assert.Empty(await _db.Books.ToListAsync());
        Assert.Empty(Directory.GetFiles(_pdfDir));
    }

    [Fact]
    public async Task Upload_NonPdfContent_Rejected()
    {
        var result = await _sut.UploadAsync(new UploadBookInput(
            "book-a", "T", "A", "en", "", Bytes("not a pdf at all"), "x.pdf"));

        Assert.False(result.Success);
        Assert.Equal(UploadError.NotPdf, result.Error);
        Assert.Empty(await _db.Books.ToListAsync());
    }

    [Fact]
    public async Task Upload_ValidPdf_CreatesBookRowAndWritesFile()
    {
        var result = await _sut.UploadAsync(new UploadBookInput(
            "book-a", "Pediatrics", "House", "en", "3rd", Bytes("%PDF-1.4\nfake pdf body"), "peds.pdf"));

        Assert.True(result.Success);
        Assert.True(result.Id > 0);

        var book = await _db.Books.SingleAsync();
        Assert.Equal("book-a", book.BookId);
        Assert.Equal(BookStatus.Pending, book.Status);
        Assert.True(File.Exists(Path.Combine(_pdfDir, "book-a.pdf")));
    }

    [Fact]
    public async Task TriggerIndex_MissingBook_ReturnsNotFound()
    {
        var result = await _sut.TriggerIndexAsync(999, force: false);

        Assert.Equal(TriggerIndexOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task TriggerIndex_PendingBookWithPdf_StartsAndEnqueuesJobAndClaimsBook()
    {
        var id = await SeedBookAsync("book-a", BookStatus.Pending, withPdf: true);

        var result = await _sut.TriggerIndexAsync(id, force: false);

        Assert.Equal(TriggerIndexOutcome.Started, result.Outcome);

        // Book was atomically claimed (Pending -> InProgress).
        var status = await _db.Books.Where(b => b.Id == id).Select(b => b.Status).SingleAsync();
        Assert.Equal(BookStatus.InProgress, status);

        // Exactly one index job was enqueued for this book.
        var job = await DequeueOneAsync();
        Assert.NotNull(job);
        Assert.Equal("book-a", job!.BookSlug);
        Assert.Equal(IngestionJobKind.Index, job.Kind);
    }

    [Fact]
    public async Task TriggerIndex_AlreadyInProgress_ReturnsConflict()
    {
        var id = await SeedBookAsync("book-a", BookStatus.InProgress, withPdf: true);

        var result = await _sut.TriggerIndexAsync(id, force: false);

        Assert.Equal(TriggerIndexOutcome.AlreadyInProgress, result.Outcome);
    }

    private async Task<int> SeedBookAsync(string bookId, BookStatus status, bool withPdf)
    {
        var filePath = Path.Combine(_pdfDir, bookId + ".pdf");
        if (withPdf)
        {
            await File.WriteAllTextAsync(filePath, "%PDF-1.4\nseed");
        }

        var entity = new BookEntity
        {
            BookId = bookId, Title = "T", Author = "A", Language = "en", Edition = "",
            FilePath = filePath, Status = status
        };
        _db.Books.Add(entity);
        await _db.SaveChangesAsync();
        return entity.Id;
    }

    private async Task<IngestionJob?> DequeueOneAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try
        {
            await foreach (var job in _queue.DequeueAllAsync(cts.Token))
            {
                return job;
            }
        }
        catch (OperationCanceledException)
        {
        }
        return null;
    }

    private sealed class FakeVectorStore : IVectorStore
    {
        public Task DeleteByBookAsync(string bookId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task UpsertAsync(MedicalChunk chunk, float[] denseVector, SparseVector sparseVector, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MedicalChunk>> SearchAsync(float[] denseQueryVector, SparseVector? sparseQueryVector, LanguageFilter language, IReadOnlyList<string>? bookIds, int topK = 5, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MedicalChunk>> ScrollSectionAsync(string chapterTitle, string sectionTitle, string bookId, int limit = 50, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteCollectionAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
