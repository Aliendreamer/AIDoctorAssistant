using MedAssist.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedAssist.Data.Configuration;

public sealed class Bm25StatsEntityConfiguration : IEntityTypeConfiguration<Bm25StatsEntity>
{
    public void Configure(EntityTypeBuilder<Bm25StatsEntity> builder)
    {
        builder.ToTable("bm25_stats");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        builder.Property(e => e.TotalDocuments).HasColumnName("total_documents").HasDefaultValue(0);
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
    }
}
