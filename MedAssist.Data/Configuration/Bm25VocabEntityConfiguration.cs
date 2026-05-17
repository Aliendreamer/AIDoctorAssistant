using MedAssist.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedAssist.Data.Configuration;

public sealed class Bm25VocabEntityConfiguration : IEntityTypeConfiguration<Bm25VocabEntity>
{
    public void Configure(EntityTypeBuilder<Bm25VocabEntity> builder)
    {
        builder.ToTable("bm25_vocab");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        builder.Property(e => e.Term).HasColumnName("term").IsRequired();
        builder.HasIndex(e => e.Term).IsUnique();
        builder.Property(e => e.DocumentFrequency).HasColumnName("document_frequency").HasDefaultValue(0);
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
    }
}
