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

        // Deterministic id from the chunk's identity so re-indexing overwrites instead of
        // appending duplicate points (audit P1-6). Summaries get a distinct key namespace.
        var pointKey = $"{(chunk.IsSummary ? "summary:" : string.Empty)}{chunk.BookId}:{chunk.ChunkIndex}";
        var point = new PointStruct
        {
            Id = new PointId { Uuid = DeterministicGuid.Create(pointKey).ToString() },
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
                [VectorStoreConstants.Payload.IsSummary] = chunk.IsSummary,
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

    public async Task<IReadOnlyList<MedicalChunk>> ScrollSectionAsync(
        string chapterTitle,
        string sectionTitle,
        string bookId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        var filter = new Filter();
        filter.Must.Add(new Condition
        {
            Field = new FieldCondition
            {
                Key = VectorStoreConstants.Payload.BookId,
                Match = new Match { Keyword = bookId }
            }
        });
        filter.Must.Add(new Condition
        {
            Field = new FieldCondition
            {
                Key = VectorStoreConstants.Payload.ChapterTitle,
                Match = new Match { Keyword = chapterTitle }
            }
        });
        filter.Must.Add(new Condition
        {
            Field = new FieldCondition
            {
                Key = VectorStoreConstants.Payload.SectionTitle,
                Match = new Match { Keyword = sectionTitle }
            }
        });
        // Exclude summary chunks — regular chunks may not have this field (old data),
        // but MustNot only blocks points where the condition is true.
        filter.MustNot.Add(new Condition
        {
            Field = new FieldCondition
            {
                Key = VectorStoreConstants.Payload.IsSummary,
                Match = new Match { Boolean = true }
            }
        });

        var scrollResult = await _client.ScrollAsync(
            VectorStoreConstants.CollectionName,
            filter,
            (uint)limit,
            null!,
            new WithPayloadSelector { Enable = true },
            new WithVectorsSelector { Enable = false },
            null!,
            null!,
            null!,
            cancellationToken);

        return scrollResult.Result.Select(MapRetrievedToChunk).ToList();
    }

    public async Task DeleteCollectionAsync(CancellationToken cancellationToken = default)
    {
        if (await _client.CollectionExistsAsync(VectorStoreConstants.CollectionName, cancellationToken))
        {
            await _client.DeleteCollectionAsync(VectorStoreConstants.CollectionName, cancellationToken: cancellationToken);
        }
    }

    public async Task DeleteByBookAsync(string bookId, CancellationToken cancellationToken = default)
    {
        if (!await _client.CollectionExistsAsync(VectorStoreConstants.CollectionName, cancellationToken))
        {
            return;
        }

        var filter = new Filter();
        filter.Must.Add(new Condition
        {
            Field = new FieldCondition
            {
                Key = VectorStoreConstants.Payload.BookId,
                Match = new Match { Keyword = bookId }
            }
        });

        await _client.DeleteAsync(VectorStoreConstants.CollectionName, filter, cancellationToken: cancellationToken);
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

    private static MedicalChunk MapToChunk(ScoredPoint point) => MapPayload(point.Payload);

    private static MedicalChunk MapRetrievedToChunk(RetrievedPoint point) => MapPayload(point.Payload);

    private static MedicalChunk MapPayload(Google.Protobuf.Collections.MapField<string, Value> payload) => new()
    {
        BookId = payload[VectorStoreConstants.Payload.BookId].StringValue,
        BookTitle = payload[VectorStoreConstants.Payload.BookTitle].StringValue,
        Author = payload[VectorStoreConstants.Payload.Author].StringValue,
        Language = payload[VectorStoreConstants.Payload.Language].StringValue,
        ChapterTitle = payload[VectorStoreConstants.Payload.ChapterTitle].StringValue,
        SectionTitle = payload[VectorStoreConstants.Payload.SectionTitle].StringValue,
        PageStart = (int)payload[VectorStoreConstants.Payload.PageStart].IntegerValue,
        PageEnd = (int)payload[VectorStoreConstants.Payload.PageEnd].IntegerValue,
        ChunkIndex = (int)payload[VectorStoreConstants.Payload.ChunkIndex].IntegerValue,
        ContentType = Enum.Parse<ContentType>(payload[VectorStoreConstants.Payload.ContentType].StringValue, ignoreCase: true),
        Text = payload[VectorStoreConstants.Payload.Text].StringValue,
        IcdCodes = payload[VectorStoreConstants.Payload.IcdCodes].StringValue
            .Split(',', StringSplitOptions.RemoveEmptyEntries),
        IsSummary = payload.TryGetValue(VectorStoreConstants.Payload.IsSummary, out var isSummaryVal)
            && isSummaryVal.BoolValue,
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
