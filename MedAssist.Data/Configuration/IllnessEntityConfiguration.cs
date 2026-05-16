using MedAssist.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedAssist.Data.Configuration;

public sealed class IllnessEntityConfiguration : IEntityTypeConfiguration<IllnessEntity>
{
    public void Configure(EntityTypeBuilder<IllnessEntity> builder)
    {
        builder.ToTable("illnesses");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
        builder.Property(e => e.IcdCode).HasColumnName("icd_code").IsRequired();
        builder.HasIndex(e => e.IcdCode).IsUnique();
        builder.Property(e => e.NameEn).HasColumnName("name_en").IsRequired();
        builder.Property(e => e.NameBg).HasColumnName("name_bg").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.HasMany(e => e.Aliases)
               .WithOne(a => a.Illness)
               .HasForeignKey(a => a.IllnessId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
