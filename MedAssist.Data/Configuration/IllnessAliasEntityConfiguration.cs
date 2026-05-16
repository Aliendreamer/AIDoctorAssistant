using MedAssist.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedAssist.Data.Configuration;

public sealed class IllnessAliasEntityConfiguration : IEntityTypeConfiguration<IllnessAliasEntity>
{
    public void Configure(EntityTypeBuilder<IllnessAliasEntity> builder)
    {
        builder.ToTable("illness_aliases");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(e => e.IllnessId).HasColumnName("illness_id");
        builder.Property(e => e.Alias).HasColumnName("alias").IsRequired();
        builder.Property(e => e.Language).HasColumnName("language").IsRequired();
        builder.HasIndex(e => e.IllnessId);
    }
}
