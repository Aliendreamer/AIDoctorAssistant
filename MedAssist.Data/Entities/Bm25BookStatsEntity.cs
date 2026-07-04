namespace MedAssist.Data.Entities;

/// <summary>
/// One book's chunk count as contributed to the global BM25 corpus size (<c>bm25_stats</c>). Stored
/// per book so a re-index applies only the delta (new count − old count) to the global total,
/// leaving other books' contribution intact (audit P2-1/P2-2).
/// </summary>
public sealed class Bm25BookStatsEntity
{
    public string BookId { get; set; } = string.Empty;
    public int ChunkCount { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
