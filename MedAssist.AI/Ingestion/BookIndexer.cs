using MedAssist.Data.Repositories;
using MedAssist.Shared.Constants;
using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;
using Microsoft.Extensions.Logging;

namespace MedAssist.AI.Ingestion;

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
        if (checkpoint?.Status == IngestionStatus.Complete)
        {
            throw new InvalidOperationException($"Book '{bookId}' is already fully indexed.");
        }

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
                await _checkpointRepo.UpsertAsync(new IngestionCheckpoint(
                    bookId, allChunks.Count, indexed, i, IngestionStatus.InProgress, DateTimeOffset.UtcNow), cancellationToken);
                _logger.LogInformation("Checkpoint saved: {Indexed}/{Total} chunks", indexed, allChunks.Count);
            }
        }

        await _checkpointRepo.UpsertAsync(new IngestionCheckpoint(
            bookId, allChunks.Count, allChunks.Count, allChunks.Count - 1, IngestionStatus.Complete, DateTimeOffset.UtcNow), cancellationToken);

        await _vocabBuilder.FlushAsync(cancellationToken);

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
}
