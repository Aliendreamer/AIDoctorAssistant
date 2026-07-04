using MedAssist.AI.Reranker;

namespace MedAssist.Tests;

// Guards P2-5/P1-11: the combined [CLS] query [SEP] passage [SEP] sequence must never exceed the
// model's 512-token position-embedding limit, or ONNX Run throws and the whole query fails. The old
// budget (Math.Max(3, 512 - queryLen + 1)) could produce 514 tokens for a near-max-length query.
public sealed class CrossEncoderRerankerTests
{
    [Theory]
    [InlineData(512, 100)]
    [InlineData(500, 300)]
    [InlineData(10, 20)]
    [InlineData(512, 0)]
    [InlineData(400, 400)]
    public void CombineInputIds_NeverExceedsMaxTokens(int queryLen, int passageLen)
    {
        var query = Enumerable.Range(0, queryLen).ToArray();
        var passage = Enumerable.Range(1000, passageLen).ToArray();

        var combined = CrossEncoderReranker.CombineInputIds(query, passage, 512);

        Assert.True(combined.Length <= 512, $"combined length {combined.Length} exceeded 512");
    }

    [Fact]
    public void CombineInputIds_SkipsPassageClsToken()
    {
        var query = new[] { 101, 5, 102 };      // [CLS] q [SEP]
        var passage = new[] { 101, 7, 8, 102 }; // [CLS] p p [SEP]

        var combined = CrossEncoderReranker.CombineInputIds(query, passage, 512);

        Assert.Equal(new long[] { 101, 5, 102, 7, 8, 102 }, combined);
    }
}
