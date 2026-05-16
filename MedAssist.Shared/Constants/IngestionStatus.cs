namespace MedAssist.Shared.Constants;

// NOTE: Indexed must equal BookStatus.Indexed.ToString().ToLowerInvariant().
// If BookStatus enum member names change, update the corresponding constant here.
public static class IngestionStatus
{
    public const string Pending = "pending";
    public const string InProgress = "in_progress";
    public const string Indexed = "indexed";
    public const string Complete = "complete";
}
