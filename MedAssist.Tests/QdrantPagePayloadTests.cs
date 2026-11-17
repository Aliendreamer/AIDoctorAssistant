using MedAssist.AI.VectorStore;
using MedAssist.Shared.Constants;
using MedAssist.Shared.Models;

namespace MedAssist.Tests;

public sealed class QdrantPagePayloadTests
{
    private static MedicalChunk Chunk(int pdfStart, int pdfEnd, string? printedStart, string? printedEnd) => new()
    {
        BookId = "nelson-20-bg",
        BookTitle = "Nelson",
        Language = "bg",
        ChunkIndex = 3,
        PageStart = pdfStart,
        PageEnd = pdfEnd,
        PrintedPageStart = printedStart,
        PrintedPageEnd = printedEnd,
        Text = "текст",
    };

    [Fact]
    public void PrintedPages_RoundTripThroughPayload()
    {
        var point = QdrantVectorStore.BuildPoint(Chunk(2635, 2636, "1804", "1804.e1"), [0.1f], SparseVector.Empty);

        var chunk = QdrantVectorStore.MapPayload(point.Payload);

        Assert.Equal(2635, chunk.PageStart);
        Assert.Equal(2636, chunk.PageEnd);
        Assert.Equal("1804", chunk.PrintedPageStart);
        Assert.Equal("1804.e1", chunk.PrintedPageEnd);
    }

    [Fact]
    public void UnknownPrintedPages_AreNotWritten()
    {
        var point = QdrantVectorStore.BuildPoint(Chunk(530, 530, null, null), [0.1f], SparseVector.Empty);

        Assert.False(point.Payload.ContainsKey(VectorStoreConstants.Payload.PrintedPageStart));
        Assert.Null(QdrantVectorStore.MapPayload(point.Payload).PrintedPageStart);
    }

    [Fact]
    public void LegacyPayload_WithoutPageFields_ReadsAsUnknown()
    {
        var point = QdrantVectorStore.BuildPoint(Chunk(0, 0, null, null), [0.1f], SparseVector.Empty);
        point.Payload.Remove(VectorStoreConstants.Payload.PageStart);
        point.Payload.Remove(VectorStoreConstants.Payload.PageEnd);

        var chunk = QdrantVectorStore.MapPayload(point.Payload);

        Assert.Equal(0, chunk.PageStart);
        Assert.Equal(0, chunk.PageEnd);
        Assert.Null(chunk.PrintedPageStart);
        Assert.Equal(string.Empty, PageLabel.Format(chunk.PageStart, chunk.PageEnd, chunk.PrintedPageStart, chunk.PrintedPageEnd));
    }
}
