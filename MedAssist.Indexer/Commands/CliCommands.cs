using MedAssist.AI.Embedding;
using MedAssist.AI.Extensions;
using MedAssist.AI.VectorStore;
using MedAssist.Data;
using MedAssist.Indexer.Ingestion;
using MedAssist.Indexer.Repositories;
using MedAssist.Shared.Constants;
using MedAssist.Shared.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Qdrant.Client;

namespace MedAssist.Indexer.Commands;

internal static class CliCommands
{
    internal static async Task RunAsync(string[] args)
    {
        var config = new ConfigurationBuilder()
            .AddSharedConfiguration()
            .AddEnvironmentVariables()
            .Build();

        var connectionString = config["Database:ConnectionString"]
            ?? throw new InvalidOperationException("Database:ConnectionString is not configured");
        var qdrantEndpoint = config["VectorStore:Qdrant:Endpoint"] ?? "http://localhost:6333";
        var modelsPath = config["Models:Path"] ?? "models";

        var dbOptions = new DbContextOptionsBuilder<MedAssistDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new MedAssistDbContext(dbOptions);
        await db.Database.MigrateAsync();

        switch (args[0])
        {
            case "index":
                await IndexAsync(args, db, qdrantEndpoint, modelsPath);
                break;

            case "dictionary":
                if (args.Length > 1 && args[1] == "add")
                {
                    await DictionaryAddAsync(args, db);
                }
                else
                {
                    Console.Error.WriteLine("Usage: dictionary add --icd <code> --en <name> --bg <name>");
                }

                break;

            case "rebuild-vocab":
                await RebuildVocabAsync(db, qdrantEndpoint);
                break;

            default:
                Console.Error.WriteLine($"Unknown command: {args[0]}");
                Console.Error.WriteLine("Available commands: index, dictionary, rebuild-vocab");
                break;
        }
    }

    private static async Task IndexAsync(string[] args, MedAssistDbContext db, string qdrantEndpoint, string modelsPath)
    {
        var parsed = ParseArgs(args[1..]);
        if (!parsed.TryGetValue("--book", out var bookFile) ||
            !parsed.TryGetValue("--book-id", out var bookId) ||
            !parsed.TryGetValue("--title", out var title) ||
            !parsed.TryGetValue("--author", out var author) ||
            !parsed.TryGetValue("--language", out var language))
        {
            Console.Error.WriteLine("Usage: index --book <file> --book-id <id> --title <title> --author <author> --language <bg|en> [--recreate-collection]");
            return;
        }

        parsed.TryGetValue("--edition", out var edition);
        var recreateCollection = parsed.ContainsKey("--recreate-collection");

        var modelDir = Path.Combine(modelsPath, "multilingual-e5-large");
        using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        using var httpClient = new HttpClient();
        var modelInit = new ModelInitializer(httpClient, loggerFactory.CreateLogger<ModelInitializer>());
        await modelInit.EnsureModelReadyAsync(modelDir);

        IEmbedder embedder = new MultilingualE5Embedder(modelDir);
        var qdrantUri = new Uri(qdrantEndpoint);
        var qdrantClient = new QdrantClient(qdrantUri.Host, qdrantUri.Port);
        var vectorStore = new QdrantVectorStore(qdrantClient);

        if (recreateCollection)
        {
            await vectorStore.DeleteCollectionAsync();
            Console.WriteLine("Collection deleted — will be recreated with named-vector schema.");
        }

        var vocabRepo = new BM25VocabRepository(db);
        ISparseVectorizer sparseVectorizer = new SparseVectorizer(vocabRepo);

        var bookRepo = new BookRepository(db);
        var checkpointRepo = new CheckpointRepository(db);
        var illnessRepo = new IllnessDictionaryRepository(db);
        var chunker = new MarkdownChunker();
        var enricher = new ChunkEnricher(illnessRepo);
        var vocabBuilder = new VocabularyBuilder(vocabRepo);
        var logger = loggerFactory.CreateLogger<BookIndexer>();
        var indexer = new BookIndexer(
            vectorStore, embedder, sparseVectorizer,
            chunker, enricher, bookRepo, checkpointRepo,
            vocabBuilder, vocabRepo, logger);

        await indexer.IndexAsync(bookFile, bookId, title, author, language, edition ?? string.Empty);
        Console.WriteLine($"Done indexing: {bookId}");
    }

    private static async Task RebuildVocabAsync(MedAssistDbContext db, string qdrantEndpoint)
    {
        var qdrantUri = new Uri(qdrantEndpoint);
        var qdrantClient = new QdrantClient(qdrantUri.Host, qdrantUri.Port);

        var vocabRepo = new BM25VocabRepository(db);
        await vocabRepo.ClearAsync();
        Console.WriteLine("Cleared existing vocabulary.");

        var builder = new VocabularyBuilder(vocabRepo);
        ulong? offset = null;
        var totalScrolled = 0;

        do
        {
            var page = await qdrantClient.ScrollAsync(
                VectorStoreConstants.CollectionName,
                limit: 1000,
                offset: offset.HasValue ? new Qdrant.Client.Grpc.PointId { Num = offset.Value } : null,
                payloadSelector: new Qdrant.Client.Grpc.WithPayloadSelector { Enable = true });

            foreach (var point in page.Result)
            {
                if (point.Payload.TryGetValue(VectorStoreConstants.Payload.Text, out var textVal))
                {
                    builder.AddChunk(textVal.StringValue);
                }
            }

            totalScrolled += page.Result.Count;
            offset = page.NextPageOffset?.Num;
            Console.WriteLine($"Scrolled {totalScrolled} chunks...");
        }
        while (offset.HasValue);

        await builder.FlushAsync(0, CancellationToken.None);
        Console.WriteLine($"Vocabulary rebuilt from {totalScrolled} chunks.");
    }

    private static async Task DictionaryAddAsync(string[] args, MedAssistDbContext db)
    {
        var parsed = ParseArgs(args[2..]);
        if (!parsed.TryGetValue("--icd", out var icd) ||
            !parsed.TryGetValue("--en", out var nameEn) ||
            !parsed.TryGetValue("--bg", out var nameBg))
        {
            Console.Error.WriteLine("Usage: dictionary add --icd <code> --en <name> --bg <name>");
            return;
        }

        var repo = new IllnessDictionaryRepository(db);
        await repo.AddAsync(icd, nameEn, nameBg);
        Console.WriteLine($"Added: {icd} | {nameEn} | {nameBg}");
    }

    private static Dictionary<string, string> ParseArgs(string[] args)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--"))
            {
                continue;
            }

            if (i + 1 >= args.Length || args[i + 1].StartsWith("--"))
            {
                result[args[i]] = "true";
            }
            else
            {
                result[args[i]] = args[i + 1];
                i++;
            }
        }

        return result;
    }
}
