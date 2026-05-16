using MedAssist.Indexer.Repositories;
using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;
using Microsoft.Extensions.Logging;

namespace MedAssist.Indexer.Ingestion;

public sealed class BookIndexer
{
    private readonly IVectorStore _vectorStore;
    private readonly IEmbedder _embedder;
    private readonly ISparseVectorizer _sparseVectorizer;
    private readonly MarkdownChunker _chunker;
    private readonly ChunkEnricher _enricher;
    private readonly BookRepository _bookRepo;
    private readonly CheckpointRepository _checkpointRepo;
    private readonly VocabularyBuilder _vocabBuilder;
    private readonly BM25VocabRepository _vocabRepo;
    private readonly ILogger<BookIndexer> _logger;
    private const int _checkpointInterval = 50;

    public BookIndexer(
        IVectorStore vectorStore,
        IEmbedder embedder,
        ISparseVectorizer sparseVectorizer,
        MarkdownChunker chunker,
        ChunkEnricher enricher,
        BookRepository bookRepo,
        CheckpointRepository checkpointRepo,
        VocabularyBuilder vocabBuilder,
        BM25VocabRepository vocabRepo,
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
        _vocabRepo = vocabRepo;
        _logger = logger;
    }

    public async Task IndexAsync(
        string markdownPath,
        string bookId,
        string title,
        string author,
        string language,
        string edition = "",
        CancellationToken cancellationToken = default)
    {
        var markdown = await File.ReadAllTextAsync(markdownPath, cancellationToken);
        var allChunks = _chunker.Chunk(markdown);

        var checkpoint = await _checkpointRepo.GetByBookIdAsync(bookId, cancellationToken);
        var resumeFromIndex = checkpoint?.Status == "complete"
            ? throw new InvalidOperationException($"Book '{bookId}' is already fully indexed.")
            : (checkpoint?.LastChunkIndex ?? -1) + 1;

        _logger.LogInformation("Indexing book {BookId}: {Total} chunks, resuming from {Start}",
            bookId, allChunks.Count, resumeFromIndex);

        await _bookRepo.UpsertAsync(new BookInfo
        {
            Id = bookId,
            Title = title,
            Author = author,
            Language = language,
            Edition = edition,
            TotalChunks = allChunks.Count,
            Status = BookStatus.InProgress
        }, cancellationToken);

        var indexed = checkpoint?.IndexedChunks ?? 0;

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
            await _vectorStore.UpsertAsync(chunk, denseVector, sparseVector, cancellationToken);
            _vocabBuilder.AddChunk(text);
            indexed++;

            if (indexed % _checkpointInterval == 0)
            {
                await SaveCheckpointAsync(bookId, allChunks.Count, indexed, i, "in_progress", cancellationToken);
                _logger.LogInformation("Checkpoint saved: {Indexed}/{Total} chunks", indexed, allChunks.Count);
            }
        }

        await SaveCheckpointAsync(bookId, allChunks.Count, allChunks.Count, allChunks.Count - 1, "complete", cancellationToken);

        var existingTotal = await _vocabRepo.GetTotalDocumentsAsync(cancellationToken);
        await _vocabBuilder.FlushAsync(existingTotal, cancellationToken);
        _logger.LogInformation("Vocabulary updated for {BookId}", bookId);

        await _bookRepo.UpsertAsync(new BookInfo
        {
            Id = bookId,
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

    private async Task SaveCheckpointAsync(
        string bookId,
        int total,
        int indexed,
        int lastIndex,
        string status,
        CancellationToken cancellationToken)
    {
        await _checkpointRepo.UpsertAsync(new IngestionCheckpoint(
            bookId, total, indexed, lastIndex, status, DateTimeOffset.UtcNow), cancellationToken);
    }
}
