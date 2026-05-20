namespace MedAssist.Data.Entities;

public sealed class ChatMessageEntity
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string QueryType { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
