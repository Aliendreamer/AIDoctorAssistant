namespace MedAssist.Data.Entities;

public sealed class IllnessAliasEntity
{
    public Guid Id { get; set; }
    public Guid IllnessId { get; set; }
    public string Alias { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public IllnessEntity Illness { get; set; } = null!;
}
