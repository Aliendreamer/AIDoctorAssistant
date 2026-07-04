using MedAssist.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedAssist.Data.Configuration;

public sealed class ExtractionStatusEntityConfiguration : IEntityTypeConfiguration<ExtractionStatusEntity>
{
    public void Configure(EntityTypeBuilder<ExtractionStatusEntity> builder)
    {
        builder.ToTable("extraction_status");
        builder.HasKey(e => e.BookDbId);
        builder.Property(e => e.BookDbId).HasColumnName("book_db_id").ValueGeneratedNever();
        builder.Property(e => e.BookSlug).HasColumnName("book_slug").IsRequired();
        // Stored as text (not a Postgres enum) — a small, rarely-read status column; string keeps it
        // human-readable and dodges another enum-type migration.
        builder.Property(e => e.State).HasColumnName("state").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(e => e.StartedAt).HasColumnName("started_at");
        builder.Property(e => e.CompletedAt).HasColumnName("completed_at");
        builder.Property(e => e.Error).HasColumnName("error");
    }
}
