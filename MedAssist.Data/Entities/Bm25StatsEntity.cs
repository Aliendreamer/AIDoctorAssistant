namespace MedAssist.Data.Entities;

public sealed class Bm25StatsEntity
{
    public int Id { get; set; }
    public int TotalDocuments { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
