namespace MedAssist.Data.Entities;

public sealed class IllnessEntity
{
    public Guid Id { get; set; }
    public string IcdCode { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameBg { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public ICollection<IllnessAliasEntity> Aliases { get; set; } = [];
}
