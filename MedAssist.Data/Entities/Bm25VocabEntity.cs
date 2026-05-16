namespace MedAssist.Data.Entities;

public sealed class Bm25VocabEntity
{
    public long Id { get; set; }
    public string Term { get; set; } = string.Empty;
    public int DocumentFrequency { get; set; }
    public int TotalDocuments { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
