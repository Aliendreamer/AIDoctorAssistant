using System.Diagnostics.Metrics;
using System.Text.RegularExpressions;
using MedAssist.AI.Dictionary;
using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;
using Microsoft.Extensions.Logging;

namespace MedAssist.AI.Ingestion;

public sealed class BookIndexer
{
    private static readonly Meter _meter = new("MedAssist.AI");
    private static readonly Counter<long> _chunksIndexed = _meter.CreateCounter<long>(
        "indexer_chunks_total", description: "Total chunks indexed across all books");

    private readonly IVectorStore _vectorStore;
    private readonly IEmbedder _embedder;
    private readonly ISparseVectorizer _sparseVectorizer;
    private readonly MarkdownChunker _chunker;
    private readonly ChunkEnricher _enricher;
    private readonly IBookRepository _bookRepo;
    private readonly ICheckpointRepository _checkpointRepo;
    private readonly VocabularyBuilder _vocabBuilder;
    private readonly Bm25VocabCache _vocabCache;
    private readonly ILogger<BookIndexer> _logger;
    private const int _checkpointInterval = 50;

    public BookIndexer(
        IVectorStore vectorStore,
        IEmbedder embedder,
        ISparseVectorizer sparseVectorizer,
        MarkdownChunker chunker,
        ChunkEnricher enricher,
        IBookRepository bookRepo,
        ICheckpointRepository checkpointRepo,
        VocabularyBuilder vocabBuilder,
        Bm25VocabCache vocabCache,
        ILogger<BookIndexer> logger)
    {
        _vectorStore = vectorStore;
        _embedder = embedder;
        _sparseVectorizer = sparseVectorizer;
        _chunker = chunker;
        _enricher = enricher;
        _bookRepo = bookRepo;
        _checkpointRepo = checkpointRepo;
        _vocabBuilder = vocabBuilder;
        _vocabCache = vocabCache;
        _logger = logger;
    }

    public async Task IndexAsync(
        string markdownText,
        string bookId,
        string title,
        string author,
        string language,
        string edition = "",
        CancellationToken cancellationToken = default)
    {
        var allChunks = _chunker.Chunk(markdownText);

        var checkpoint = await _checkpointRepo.GetByBookIdAsync(bookId, cancellationToken);
        var resumeFromIndex = (checkpoint?.LastChunkIndex ?? -1) + 1;

        _logger.LogInformation("Indexing book {BookId}: {Total} chunks, resuming from {Start}",
            bookId, allChunks.Count, resumeFromIndex);

        await _bookRepo.UpsertAsync(new BookInfo
        {
            BookId = bookId,
            Title = title,
            Author = author,
            Language = language,
            Edition = edition,
            TotalChunks = allChunks.Count,
            Status = BookStatus.InProgress
        }, cancellationToken);

        // Pass A — build the book's BM25 vocabulary over ALL chunks and refresh the snapshot BEFORE
        // any sparse vector is produced (audit P1-5). Without this, the first book on a fresh corpus
        // would be stored with empty sparse vectors. Runs over every chunk on each attempt; the
        // contribution is applied as a delta, so re-running on resume is an idempotent no-op.
        foreach (var (_, text, _) in allChunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _vocabBuilder.AddChunk(text);
        }

        await _vocabBuilder.FlushAsync(bookId, cancellationToken);
        // The vocabulary changed — drop the cached snapshot so Pass B's sparse vectors (and later
        // queries) see the new terms (P1-9).
        _vocabCache.Invalidate();

        var indexed = checkpoint?.IndexedChunks ?? 0;

        // Pass B — embed (dense) + vectorize (sparse, now non-empty) + upsert, resumable from the
        // checkpoint. Points are accumulated and upserted in a batch at each checkpoint boundary
        // (one Qdrant round-trip per batch instead of per chunk); deterministic ids keep re-upsert
        // on resume idempotent.
        var batch = new List<ChunkVector>(_checkpointInterval);
        for (var i = resumeFromIndex; i < allChunks.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (headingPath, text, contentType) = allChunks[i];
            var parts = headingPath.Split(" > ", 2);

            var icdCodes = await _enricher.GetIcdCodesAsync(text, cancellationToken);
            var chunk = new MedicalChunk
            {
                BookId = bookId,
                BookTitle = title,
                Author = author,
                Language = language,
                ChapterTitle = parts.Length > 0 ? parts[0] : string.Empty,
                SectionTitle = parts.Length > 1 ? parts[1] : string.Empty,
                PageStart = 0,
                PageEnd = 0,
                ChunkIndex = i,
                ContentType = contentType,
                Text = text,
                IcdCodes = icdCodes
            };

            var denseVector = await _embedder.EmbedPassageAsync(text, cancellationToken);
            var sparseVector = await _sparseVectorizer.VectorizePassageAsync(text, cancellationToken);
            batch.Add(new ChunkVector(chunk, denseVector, sparseVector));
            _chunksIndexed.Add(1);
            indexed++;

            if (indexed % _checkpointInterval == 0)
            {
                // Flush the batch to Qdrant before checkpointing so the checkpoint never claims more
                // progress than has actually been persisted.
                await _vectorStore.UpsertBatchAsync(batch, cancellationToken);
                batch.Clear();
                await _checkpointRepo.UpsertAsync(new IngestionCheckpoint(
                    bookId, allChunks.Count, indexed, i, BookStatus.InProgress, DateTimeOffset.UtcNow), cancellationToken);
                _logger.LogInformation("Checkpoint saved: {Indexed}/{Total} chunks", indexed, allChunks.Count);
            }
        }

        // Flush the final partial batch (no-op if empty) before marking the book Indexed.
        await _vectorStore.UpsertBatchAsync(batch, cancellationToken);

        await _checkpointRepo.UpsertAsync(new IngestionCheckpoint(
            bookId, allChunks.Count, allChunks.Count, allChunks.Count - 1, BookStatus.Indexed, DateTimeOffset.UtcNow), cancellationToken);

        await IndexSectionSummariesAsync(allChunks, bookId, title, author, language, cancellationToken);

        var outline = ExtractOutline(markdownText);
        await _bookRepo.UpdateOutlineAsync(bookId, outline, cancellationToken);

        await _bookRepo.UpsertAsync(new BookInfo
        {
            BookId = bookId,
            Title = title,
            Author = author,
            Language = language,
            Edition = edition,
            TotalChunks = allChunks.Count,
            Status = BookStatus.Indexed,
            IndexedAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        _logger.LogInformation("Indexing complete for {BookId}: {Total} chunks", bookId, allChunks.Count);
    }

    private static readonly Regex _headingRegex = new(@"^## (.+)$", RegexOptions.Multiline | RegexOptions.Compiled);

    private static string ExtractOutline(string markdown)
    {
        var headings = new List<string>();
        foreach (Match m in _headingRegex.Matches(markdown))
        {
            var text = m.Groups[1].Value.Trim();
            if (text.Length < 5)
            {
                continue;
            }

            if (text.EndsWith(':') || text.EndsWith('.'))
            {
                continue;
            }

            if (text.Contains('=') || text.Contains('+'))
            {
                continue;
            }

            headings.Add(text);
            if (headings.Count >= 100)
            {
                break;
            }
        }
        return string.Join('\n', headings);
    }

    private async Task IndexSectionSummariesAsync(
        IReadOnlyList<(string HeadingPath, string Text, ContentType ContentType)> allChunks,
        string bookId,
        string title,
        string author,
        string language,
        CancellationToken cancellationToken)
    {
        var groups = allChunks
            .Select((c, i) => (c.HeadingPath, c.Text, Index: i))
            .GroupBy(c => c.HeadingPath)
            .Where(g => !string.IsNullOrWhiteSpace(g.Key));

        var summaryIndex = allChunks.Count;
        var batch = new List<ChunkVector>();
        foreach (var group in groups)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var headingPath = group.Key;
            var firstText = group.First().Text;
            var preview = firstText.Length > 800 ? firstText[..800] : firstText;
            var summaryText = $"{headingPath}\n\n{preview}";

            var parts = headingPath.Split(" > ", 2);
            var chunk = new MedicalChunk
            {
                BookId = bookId,
                BookTitle = title,
                Author = author,
                Language = language,
                ChapterTitle = parts.Length > 0 ? parts[0] : string.Empty,
                SectionTitle = parts.Length > 1 ? parts[1] : string.Empty,
                ChunkIndex = summaryIndex++,
                ContentType = ContentType.Text,
                Text = summaryText,
                IcdCodes = [],
                IsSummary = true
            };

            var denseVector = await _embedder.EmbedPassageAsync(summaryText, cancellationToken);
            var sparseVector = await _sparseVectorizer.VectorizePassageAsync(summaryText, cancellationToken);
            batch.Add(new ChunkVector(chunk, denseVector, sparseVector));

            if (batch.Count >= _checkpointInterval)
            {
                await _vectorStore.UpsertBatchAsync(batch, cancellationToken);
                batch.Clear();
            }
        }

        await _vectorStore.UpsertBatchAsync(batch, cancellationToken);
        _logger.LogInformation("Section summaries indexed for {BookId}", bookId);
    }
}
