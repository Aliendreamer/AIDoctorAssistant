using MedAssist.Shared.Constants;
using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using DomainSparseVector = MedAssist.Shared.Models.SparseVector;

namespace MedAssist.AI.VectorStore;

public sealed class QdrantVectorStore : IVectorStore
{
    private readonly QdrantClient _client;
    private const ulong _vectorSize = 1024;

    public QdrantVectorStore(QdrantClient client) => _client = client;

    public async Task UpsertAsync(
        MedicalChunk chunk,
        float[] denseVector,
        DomainSparseVector sparseVector,
        CancellationToken cancellationToken = default)
    {
        await EnsureCollectionExistsAsync(cancellationToken);

        var namedVectors = new NamedVectors();

        var denseVec = new Vector { Dense = new DenseVector() };
        denseVec.Dense.Data.AddRange(denseVector);
        namedVectors.Vectors.Add(VectorStoreConstants.Vectors.Dense, denseVec);

        if (!sparseVector.IsEmpty)
        {
            var sorted = sparseVector.Entries.OrderBy(e => e.Key).ToList();
            var sparseVec = new Vector { Sparse = new Qdrant.Client.Grpc.SparseVector() };
            sparseVec.Sparse.Values.AddRange(sorted.Select(e => e.Value));
            sparseVec.Sparse.Indices.AddRange(sorted.Select(e => e.Key));
            namedVectors.Vectors.Add(VectorStoreConstants.Vectors.Sparse, sparseVec);
        }

        var point = new PointStruct
        {
            Id = new PointId { Uuid = Guid.NewGuid().ToString() },
            Vectors = new Vectors { Vectors_ = namedVectors },
            Payload =
            {
                [VectorStoreConstants.Payload.BookId] = chunk.BookId,
                [VectorStoreConstants.Payload.BookTitle] = chunk.BookTitle,
                [VectorStoreConstants.Payload.Author] = chunk.Author,
                [VectorStoreConstants.Payload.Language] = chunk.Language,
                [VectorStoreConstants.Payload.ChapterTitle] = chunk.ChapterTitle,
                [VectorStoreConstants.Payload.SectionTitle] = chunk.SectionTitle,
                [VectorStoreConstants.Payload.PageStart] = chunk.PageStart,
                [VectorStoreConstants.Payload.PageEnd] = chunk.PageEnd,
                [VectorStoreConstants.Payload.ChunkIndex] = chunk.ChunkIndex,
                [VectorStoreConstants.Payload.ContentType] = chunk.ContentType.ToString().ToLowerInvariant(),
                [VectorStoreConstants.Payload.Text] = chunk.Text,
                [VectorStoreConstants.Payload.IcdCodes] = string.Join(",", chunk.IcdCodes),
            }
        };

        await _client.UpsertAsync(VectorStoreConstants.CollectionName, [point], cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<MedicalChunk>> SearchAsync(
        float[] denseQueryVector,
        DomainSparseVector? sparseQueryVector,
        LanguageFilter language,
        IReadOnlyList<string>? bookIds,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        var filter = BuildFilter(language, bookIds);

        if (sparseQueryVector is { IsEmpty: false })
        {
            return await HybridSearchAsync(denseQueryVector, sparseQueryVector, filter, topK, cancellationToken);
        }

        // Dense-only fallback
        var results = await _client.SearchAsync(
            VectorStoreConstants.CollectionName,
            denseQueryVector,
            filter: filter,
            limit: (ulong)topK,
            vectorName: VectorStoreConstants.Vectors.Dense,
            payloadSelector: new WithPayloadSelector { Enable = true },
            cancellationToken: cancellationToken);

        return results.Select(MapToChunk).ToList();
    }

    private async Task<IReadOnlyList<MedicalChunk>> HybridSearchAsync(
        float[] denseQueryVector,
        DomainSparseVector sparseQueryVector,
        Filter? filter,
        int topK,
        CancellationToken cancellationToken)
    {
        var sortedSparse = sparseQueryVector.Entries.OrderBy(e => e.Key).ToList();

        var denseInput = new VectorInput { Dense = new DenseVector() };
        denseInput.Dense.Data.AddRange(denseQueryVector);

        var sparseInput = new VectorInput { Sparse = new Qdrant.Client.Grpc.SparseVector() };
        sparseInput.Sparse.Values.AddRange(sortedSparse.Select(e => e.Value));
        sparseInput.Sparse.Indices.AddRange(sortedSparse.Select(e => e.Key));

        var prefetchLimit = (ulong)(topK * 3);

        var prefetch = new List<PrefetchQuery>
        {
            new()
            {
                Query = new Query { Nearest = denseInput },
                Using = VectorStoreConstants.Vectors.Dense,
                Limit = prefetchLimit,
                Filter = filter
            },
            new()
            {
                Query = new Query { Nearest = sparseInput },
                Using = VectorStoreConstants.Vectors.Sparse,
                Limit = prefetchLimit,
                Filter = filter
            }
        };

        var results = await _client.QueryAsync(
            VectorStoreConstants.CollectionName,
            query: new Query { Fusion = Fusion.Rrf },
            prefetch: prefetch,
            filter: filter,
            limit: (ulong)topK,
            payloadSelector: new WithPayloadSelector { Enable = true },
            cancellationToken: cancellationToken);

        return results.Select(MapToChunk).ToList();
    }

    public async Task DeleteCollectionAsync(CancellationToken cancellationToken = default)
    {
        if (await _client.CollectionExistsAsync(VectorStoreConstants.CollectionName, cancellationToken))
        {
            await _client.DeleteCollectionAsync(VectorStoreConstants.CollectionName, cancellationToken: cancellationToken);
        }
    }

    private static Filter? BuildFilter(LanguageFilter language, IReadOnlyList<string>? bookIds)
    {
        var conditions = new List<Condition>();

        if (language != LanguageFilter.Both)
        {
            var langCode = language == LanguageFilter.English ? LanguageCodes.English : LanguageCodes.Bulgarian;
            conditions.Add(new Condition
            {
                Field = new FieldCondition
                {
                    Key = VectorStoreConstants.Payload.Language,
                    Match = new Match { Keyword = langCode }
                }
            });
        }

        if (bookIds is { Count: > 0 })
        {
            conditions.Add(new Condition
            {
                Filter = new Filter
                {
                    Should =
                    {
                        bookIds.Select(id => new Condition
                        {
                            Field = new FieldCondition
                            {
                                Key = VectorStoreConstants.Payload.BookId,
                                Match = new Match { Keyword = id }
                            }
                        })
                    }
                }
            });
        }

        if (conditions.Count == 0)
        {
            return null;
        }

        var filter = new Filter();
        filter.Must.AddRange(conditions);
        return filter;
    }

    private static MedicalChunk MapToChunk(ScoredPoint point) => new()
    {
        BookId = point.Payload[VectorStoreConstants.Payload.BookId].StringValue,
        BookTitle = point.Payload[VectorStoreConstants.Payload.BookTitle].StringValue,
        Author = point.Payload[VectorStoreConstants.Payload.Author].StringValue,
        Language = point.Payload[VectorStoreConstants.Payload.Language].StringValue,
        ChapterTitle = point.Payload[VectorStoreConstants.Payload.ChapterTitle].StringValue,
        SectionTitle = point.Payload[VectorStoreConstants.Payload.SectionTitle].StringValue,
        PageStart = (int)point.Payload[VectorStoreConstants.Payload.PageStart].IntegerValue,
        PageEnd = (int)point.Payload[VectorStoreConstants.Payload.PageEnd].IntegerValue,
        ChunkIndex = (int)point.Payload[VectorStoreConstants.Payload.ChunkIndex].IntegerValue,
        ContentType = Enum.Parse<ContentType>(point.Payload[VectorStoreConstants.Payload.ContentType].StringValue, ignoreCase: true),
        Text = point.Payload[VectorStoreConstants.Payload.Text].StringValue,
        IcdCodes = point.Payload[VectorStoreConstants.Payload.IcdCodes].StringValue
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
    };

    private async Task EnsureCollectionExistsAsync(CancellationToken cancellationToken)
    {
        if (await _client.CollectionExistsAsync(VectorStoreConstants.CollectionName, cancellationToken))
        {
            return;
        }

        var vectorsMap = new VectorParamsMap();
        vectorsMap.Map.Add(VectorStoreConstants.Vectors.Dense, new VectorParams
        {
            Size = _vectorSize,
            Distance = Distance.Cosine
        });

        var sparseConfig = new SparseVectorConfig();
        sparseConfig.Map.Add(VectorStoreConstants.Vectors.Sparse, new SparseVectorParams
        {
            Index = new SparseIndexConfig()
        });

        await _client.CreateCollectionAsync(
            VectorStoreConstants.CollectionName,
            vectorsMap,
            sparseVectorsConfig: sparseConfig,
            cancellationToken: cancellationToken);
    }
}
