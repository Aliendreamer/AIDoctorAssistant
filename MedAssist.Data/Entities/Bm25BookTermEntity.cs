namespace MedAssist.Data.Entities;

/// <summary>
/// One book's document-frequency contribution for a single term: the number of that book's chunks
/// containing <see cref="Term"/>. The global <c>bm25_vocab</c> row for a term is the sum of every
/// book's contribution, so re-indexing subtracts a book's old rows and adds its new ones — keeping
/// the global DF idempotent across re-index/resume (audit P2-1/P2-2).
/// </summary>
public sealed class Bm25BookTermEntity
{
    public string BookId { get; set; } = string.Empty;
    public string Term { get; set; } = string.Empty;
    public int DocumentFrequency { get; set; }
}
