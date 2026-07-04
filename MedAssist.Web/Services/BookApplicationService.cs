using MedAssist.AI.Ingestion;
using MedAssist.Data;
using MedAssist.Data.Entities;
using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;
using MedAssist.Shared.Validation;
using MedAssist.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace MedAssist.Web.Services;

public enum UploadError { None, InvalidBookId, NotPdf }

public sealed record BookUploadResult(bool Success, int Id, UploadError Error, string Message);

public enum TriggerIndexOutcome { Started, NotFound, InvalidBookId, AlreadyInProgress }

public sealed record TriggerIndexResult(TriggerIndexOutcome Outcome, string Message);

public sealed record UploadBookInput(
    string BookId,
    string Title,
    string Author,
    string Language,
    string Edition,
    Stream Content,
    string FileName);

/// <summary>
/// In-process application service owning book upload (PDF validation + file write + upsert) and the
/// index-trigger workflow (validate → atomically claim → optional force-clear → enqueue). Both the
/// REST endpoints and the Blazor admin pages (via <see cref="AdminBookService"/>) call this, so the
/// logic that used to live in the endpoint bodies has one home and no loopback API hop (audit P1-12).
/// </summary>
public sealed class BookApplicationService(
    MedAssistDbContext db,
    BookRepository bookRepo,
    CheckpointRepository checkpointRepo,
    IVectorStore vectorStore,
    IngestionQueue queue,
    IConfiguration configuration,
    ILogger<BookApplicationService> logger)
{
    public async Task<BookUploadResult> UploadAsync(UploadBookInput input, CancellationToken cancellationToken = default)
    {
        // Defense-in-depth (the endpoint validator also runs for API callers): reject invalid ids so
        // the resolved write path can't escape the books directory (audit P1-1).
        if (!BookIdRules.IsValid(input.BookId))
        {
            return new BookUploadResult(false, 0, UploadError.InvalidBookId, $"Book id '{input.BookId}' is invalid.");
        }

        var pdfPath = configuration["Books:PdfPath"]
            ?? throw new InvalidOperationException("Books:PdfPath is not configured.");
        Directory.CreateDirectory(pdfPath);
        var destPath = BookIdRules.ResolveWithin(pdfPath, input.BookId, ".pdf");

        // Verify the content is actually a PDF before persisting it (audit P2-10). Read the header
        // from the (possibly non-seekable) upload stream, then stream header + remainder to disk.
        var header = new byte[5];
        var read = await input.Content.ReadAtLeastAsync(header, 5, throwOnEndOfStream: false, cancellationToken);
        if (read < 5 || header[0] != (byte)'%' || header[1] != (byte)'P' || header[2] != (byte)'D' || header[3] != (byte)'F' || header[4] != (byte)'-')
        {
            return new BookUploadResult(false, 0, UploadError.NotPdf, "Uploaded file is not a valid PDF (missing %PDF- header).");
        }

        await using (var fs = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await fs.WriteAsync(header.AsMemory(0, read), cancellationToken);
            await input.Content.CopyToAsync(fs, cancellationToken);
        }

        var existing = await db.Books.FirstOrDefaultAsync(b => b.BookId == input.BookId, cancellationToken);
        int id;
        if (existing is not null)
        {
            existing.Title = input.Title;
            existing.Author = input.Author;
            existing.Language = input.Language;
            existing.Edition = input.Edition;
            existing.FilePath = destPath;
            existing.Status = BookStatus.Pending;
            existing.TotalChunks = 0;
            existing.IndexedAt = null;
            await db.SaveChangesAsync(cancellationToken);
            id = existing.Id;
        }
        else
        {
            var entity = new BookEntity
            {
                BookId = input.BookId,
                Title = input.Title,
                Author = input.Author,
                Language = input.Language,
                Edition = input.Edition,
                FilePath = destPath,
                Status = BookStatus.Pending
            };
            db.Books.Add(entity);
            await db.SaveChangesAsync(cancellationToken);
            id = entity.Id;
        }

        return new BookUploadResult(true, id, UploadError.None, $"Book '{input.Title}' uploaded.");
    }

    public async Task<TriggerIndexResult> TriggerIndexAsync(int id, bool force, CancellationToken cancellationToken = default)
    {
        var book = await bookRepo.GetByIdAsync(id, cancellationToken);
        if (book is null)
        {
            return new TriggerIndexResult(TriggerIndexOutcome.NotFound, "Book not found.");
        }

        if (!BookIdRules.IsValid(book.BookId))
        {
            return new TriggerIndexResult(TriggerIndexOutcome.InvalidBookId, $"Book '{book.BookId}' has an invalid identifier.");
        }

        var mdBasePath = configuration["Books:MdPath"] ?? "/books/mdfiles";
        var cachedMarkdownPath = BookIdRules.ResolveWithin(mdBasePath, book.BookId, ".md");
        var hasCachedMarkdown = File.Exists(cachedMarkdownPath);

        // Indexing needs either cached markdown (skip Marker) or the source PDF (to OCR). After a DB
        // wipe the PDFs may be gone while the markdown cache survives — that's fine.
        if (!hasCachedMarkdown && !File.Exists(book.FilePath))
        {
            return new TriggerIndexResult(TriggerIndexOutcome.NotFound, "Neither cached markdown nor source PDF found.");
        }

        // Atomically claim the book before clearing or enqueuing, so two concurrent triggers can't
        // both proceed (the earlier check-then-act had a TOCTOU race — audit P1-7).
        if (!await bookRepo.TryMarkInProgressAsync(id, cancellationToken))
        {
            return new TriggerIndexResult(TriggerIndexOutcome.AlreadyInProgress, $"Book '{book.BookId}' is already being indexed.");
        }

        if (force)
        {
            // Scope the clear to THIS book only — never drop the whole collection or truncate the
            // shared BM25 tables (audit P0-1).
            await new BookReindexCleaner(vectorStore).ClearBookAsync(book.BookId, force: true, cancellationToken);
            logger.LogInformation("Force re-index: cleared Qdrant points for book {BookId} (shared BM25 vocab left intact)", book.BookId);
        }

        // Always clear the checkpoint so a previous complete/partial run doesn't block re-indexing.
        await checkpointRepo.DeleteAsync(book.BookId, cancellationToken);

        // Hand the long-running work to the host-managed ingestion worker (audit P1-8).
        await queue.EnqueueAsync(new IngestionJob(
            IngestionJobKind.Index, id, book.BookId, book.Title, book.Author, book.Language, book.Edition, book.FilePath), cancellationToken);

        return new TriggerIndexResult(TriggerIndexOutcome.Started, $"Indexing started for '{book.BookId}'.");
    }
}
