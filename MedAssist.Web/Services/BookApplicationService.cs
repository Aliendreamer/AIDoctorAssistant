using System.IO.Compression;
using MedAssist.AI.Ingestion;
using MedAssist.Shared.Constants;
using MedAssist.Data;
using MedAssist.Data.Entities;
using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;
using MedAssist.Shared.Validation;
using MedAssist.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace MedAssist.Web.Services;

public enum UploadError { None, InvalidBookId, NotPdf, InvalidZip }

/// <param name="PreviouslyIndexed">The book was already indexed before this upload, so it needs a <c>force=true</c> re-index.</param>
public sealed record BookUploadResult(bool Success, int Id, UploadError Error, string Message, bool PreviouslyIndexed = false);

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

        // The file signature decides the path: a PDF (OCR'd later by MinerU) or a zip of Word parts (change
        // ingest-docx-and-page-citations). Read the header from the (possibly non-seekable) upload stream, then
        // stream header + remainder onward (audit P2-10).
        var header = new byte[5];
        var read = await input.Content.ReadAtLeastAsync(header, 5, throwOnEndOfStream: false, cancellationToken);

        string sourcePath;
        string stored;
        if (read == 5 && header.AsSpan().SequenceEqual("%PDF-"u8))
        {
            sourcePath = await StorePdfAsync(input, header, cancellationToken);
            stored = "PDF";
        }
        else if (read >= 4 && header.AsSpan(0, 4).SequenceEqual(_zipSignature))
        {
            var parts = await StoreWordPartsAsync(input, header.AsMemory(0, read), cancellationToken);
            if (parts.Error is not null)
            {
                return new BookUploadResult(false, 0, UploadError.InvalidZip, parts.Error);
            }

            sourcePath = parts.Directory;
            stored = $"{parts.Added} Word parts ({parts.Total} stored for this book)";
        }
        else
        {
            return new BookUploadResult(false, 0, UploadError.NotPdf,
                "Uploaded file is neither a PDF (missing %PDF- header) nor a zip of Word parts.");
        }

        var existing = await db.Books.FirstOrDefaultAsync(b => b.BookId == input.BookId, cancellationToken);
        var previouslyIndexed = existing?.Status == BookStatus.Indexed;
        int id;
        if (existing is not null)
        {
            existing.Title = input.Title;
            existing.Author = input.Author;
            existing.Language = input.Language;
            existing.Edition = input.Edition;
            existing.FilePath = sourcePath;
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
                FilePath = sourcePath,
                Status = BookStatus.Pending
            };
            db.Books.Add(entity);
            await db.SaveChangesAsync(cancellationToken);
            id = entity.Id;
        }

        var message = $"Book '{input.Title}' uploaded: {stored}.";
        if (previouslyIndexed)
        {
            message += " It was already indexed — re-index it with force so the search uses the new content.";
        }

        return new BookUploadResult(true, id, UploadError.None, message, previouslyIndexed);
    }

    private static readonly byte[] _zipSignature = [0x50, 0x4B, 0x03, 0x04];

    private async Task<string> StorePdfAsync(UploadBookInput input, byte[] header, CancellationToken cancellationToken)
    {
        var pdfPath = configuration["Books:PdfPath"]
            ?? throw new InvalidOperationException("Books:PdfPath is not configured.");
        Directory.CreateDirectory(pdfPath);
        var destPath = BookIdRules.ResolveWithin(pdfPath, input.BookId, ".pdf");

        await using var fs = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await fs.WriteAsync(header, cancellationToken);
        await input.Content.CopyToAsync(fs, cancellationToken);
        return destPath;
    }

    private sealed record StoredParts(string Directory, int Added, int Total, string? Error);

    /// <summary>
    /// Extracts a zip of Word parts into the book's parts directory. Every entry is checked before anything reaches
    /// that directory: only <c>.docx</c> files, flattened to safe names, with entry count and sizes bounded (sizes
    /// counted while copying, never trusted from the zip headers). Extraction goes to a staging directory that is
    /// moved into place only when every entry passes, so a rejected zip leaves the stored parts untouched.
    /// </summary>
    private async Task<StoredParts> StoreWordPartsAsync(UploadBookInput input, ReadOnlyMemory<byte> header, CancellationToken cancellationToken)
    {
        var docxBase = configuration["Books:DocxPath"] ?? DocxUploadLimits.DefaultDocxPath;
        var partsDir = BookIdRules.ResolveWithin(docxBase, input.BookId, string.Empty);
        var maxEntries = configuration.GetValue("Books:DocxMaxEntries", DocxUploadLimits.MaxEntries);
        var maxEntryBytes = configuration.GetValue("Books:DocxMaxEntryBytes", DocxUploadLimits.MaxEntryBytes);
        var maxTotalBytes = configuration.GetValue("Books:DocxMaxTotalBytes", DocxUploadLimits.MaxTotalBytes);

        var staging = Path.Combine(Path.GetFullPath(docxBase), $".staging-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);
        try
        {
            // ZipArchive needs a seekable stream; the upload is already bounded by the request size limit.
            var zipPath = Path.Combine(staging, "upload.zip");
            await using (var zipFile = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                await zipFile.WriteAsync(header, cancellationToken);
                await input.Content.CopyToAsync(zipFile, cancellationToken);
            }

            var extracted = Path.Combine(staging, "parts");
            Directory.CreateDirectory(extracted);
            var names = new List<string>();
            using (var zip = ZipFile.OpenRead(zipPath))
            {
                var files = zip.Entries.Where(e => !e.FullName.EndsWith('/')).ToList();
                if (files.Count == 0 || files.Count > maxEntries)
                {
                    return Rejected($"The zip must contain between 1 and {maxEntries} Word parts; it has {files.Count}.");
                }

                long total = 0;
                foreach (var entry in files)
                {
                    if (WordPartName(entry.FullName) is not { } name)
                    {
                        return Rejected($"Zip entry '{entry.FullName}' is not an allowed Word part (.docx with a plain file name).");
                    }

                    await using var source = entry.Open();
                    await using var target = new FileStream(Path.Combine(extracted, name), FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                    var copied = await CopyBoundedAsync(source, target, maxEntryBytes, cancellationToken);
                    total += copied ?? 0;
                    if (copied is null || total > maxTotalBytes)
                    {
                        return Rejected($"Zip entry '{name}' exceeds the size limit ({maxEntryBytes} bytes per part, {maxTotalBytes} in total).");
                    }

                    names.Add(name);
                }
            }

            Directory.CreateDirectory(partsDir);
            foreach (var name in names)
            {
                File.Move(Path.Combine(extracted, name), Path.Combine(partsDir, name), overwrite: true);
            }

            var totalParts = Directory.GetFiles(partsDir, "*.docx").Length;
            logger.LogInformation("Stored {Added} Word parts for {BookId} ({Total} in total)", names.Count, input.BookId, totalParts);
            return new StoredParts(partsDir, names.Count, totalParts, null);
        }
        finally
        {
            Directory.Delete(staging, recursive: true);
        }

        StoredParts Rejected(string error)
        {
            logger.LogWarning("Rejected Word-part zip for {BookId}: {Error}", input.BookId, error);
            return new StoredParts(partsDir, 0, 0, error);
        }
    }

    /// <summary>The entry's file name if it is an allowed Word part: no traversal, no control characters, ends in .docx.</summary>
    private static string? WordPartName(string fullName)
    {
        var segments = fullName.Split('/', '\\');
        if (segments.Any(s => s == "..") || fullName.StartsWith('/') || fullName.StartsWith('\\'))
        {
            return null;
        }

        var name = segments[^1].Trim();
        return name.Length > ".docx".Length
            && name.EndsWith(".docx", StringComparison.OrdinalIgnoreCase)
            && !name.StartsWith('.')
            && !name.Any(char.IsControl)
            && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
                ? name
                : null;
    }

    /// <summary>Copies at most <paramref name="maxBytes"/>; returns the byte count, or null when the source is larger.</summary>
    private static async Task<long?> CopyBoundedAsync(Stream source, Stream target, long maxBytes, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long copied = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            copied += read;
            if (copied > maxBytes)
            {
                return null;
            }

            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        return copied;
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

        // Indexing needs cached markdown (skip OCR), the source PDF (to OCR), or a Word book's parts directory
        // (converted without OCR). After a DB wipe the PDFs may be gone while the markdown cache survives — that's fine.
        if (!hasCachedMarkdown && !File.Exists(book.FilePath) && !Directory.Exists(book.FilePath))
        {
            return new TriggerIndexResult(TriggerIndexOutcome.NotFound, "Neither cached markdown, a source PDF nor Word parts found.");
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
