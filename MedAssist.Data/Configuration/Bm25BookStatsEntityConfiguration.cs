using MedAssist.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedAssist.Data.Configuration;

public sealed class Bm25BookStatsEntityConfiguration : IEntityTypeConfiguration<Bm25BookStatsEntity>
{
    public void Configure(EntityTypeBuilder<Bm25BookStatsEntity> builder)
    {
        builder.ToTable("bm25_book_stats");
        builder.HasKey(e => e.BookId);
        builder.Property(e => e.BookId).HasColumnName("book_id").ValueGeneratedNever();
        builder.Property(e => e.ChunkCount).HasColumnName("chunk_count").HasDefaultValue(0);
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
    }
}
