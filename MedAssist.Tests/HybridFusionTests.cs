using MedAssist.AI.VectorStore;
using Qdrant.Client.Grpc;
using Xunit;

namespace MedAssist.Tests;

/// <summary>
/// RRF scores come from ranks alone, so two points at the same rank in different lists tie exactly.
/// Qdrant returns tied points in a varying order, which used to change which chunks survived the
/// top-K cut between identical searches.
/// </summary>
public class HybridFusionTests
{
    private const string _first = "00000000-0000-0000-0000-000000000001";
    private const string _second = "00000000-0000-0000-0000-000000000002";
    private const string _third = "00000000-0000-0000-0000-000000000003";
    private const string _fourth = "00000000-0000-0000-0000-000000000004";

    [Fact]
    public void TiesAtTheCut_KeepTheSamePoints_WhateverOrderQdrantReturnsThem()
    {
        ScoredPoint[] oneOrder = [Point(_fourth, 0.5f), Point(_first, 0.25f), Point(_third, 0.25f), Point(_second, 0.25f)];
        ScoredPoint[] otherOrder = [Point(_second, 0.25f), Point(_third, 0.25f), Point(_fourth, 0.5f), Point(_first, 0.25f)];

        var one = QdrantVectorStore.TakeTopFused(oneOrder, 2).Select(p => p.Id.Uuid);
        var other = QdrantVectorStore.TakeTopFused(otherOrder, 2).Select(p => p.Id.Uuid);

        Assert.Equal([_fourth, _first], one);
        Assert.Equal(one, other);
    }

    [Fact]
    public void HigherScore_BeatsALowerPointId()
    {
        var top = QdrantVectorStore.TakeTopFused([Point(_first, 0.2f), Point(_fourth, 0.3f)], 1);

        Assert.Equal(_fourth, Assert.Single(top).Id.Uuid);
    }

    [Fact]
    public void FewerPointsThanTopK_ReturnsThemAll()
    {
        Assert.Equal(2, QdrantVectorStore.TakeTopFused([Point(_first, 0.2f), Point(_second, 0.1f)], 5).Count);
    }

    private static ScoredPoint Point(string id, float score) => new() { Id = new PointId { Uuid = id }, Score = score };
}
