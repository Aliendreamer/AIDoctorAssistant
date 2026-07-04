namespace MedAssist.Shared.Constants;

public static class TokenizationConstants
{
    /// <summary>
    /// Word-token pattern shared by the BM25 vocabulary builder (indexing) and the sparse vectorizer
    /// (querying) so both tokenize identically. Duplicated literals could drift and silently break
    /// index/query symmetry (audit P3-9).
    /// </summary>
    public const string WordPattern = @"\p{L}+";
}
