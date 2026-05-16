using MedAssist.Data.Entities;
using MedAssist.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedAssist.Data.Configuration;

public sealed class IngestionCheckpointEntityConfiguration : IEntityTypeConfiguration<IngestionCheckpointEntity>
{
    public void Configure(EntityTypeBuilder<IngestionCheckpointEntity> builder)
    {
        builder.ToTable("ingestion_checkpoints");
        builder.HasKey(e => e.BookId);
        builder.Property(e => e.BookId).HasColumnName("book_id");
        builder.Property(e => e.TotalChunks).HasColumnName("total_chunks").HasDefaultValue(0);
        builder.Property(e => e.IndexedChunks).HasColumnName("indexed_chunks").HasDefaultValue(0);
        builder.Property(e => e.LastChunkIndex).HasColumnName("last_chunk_index").HasDefaultValue(-1);
        builder.Property(e => e.Status).HasColumnName("status").HasDefaultValue(IngestionStatus.InProgress);
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("now()");
    }
}
