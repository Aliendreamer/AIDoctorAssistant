using MedAssist.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedAssist.Data.Configuration;

public sealed class Bm25BookTermEntityConfiguration : IEntityTypeConfiguration<Bm25BookTermEntity>
{
    public void Configure(EntityTypeBuilder<Bm25BookTermEntity> builder)
    {
        builder.ToTable("bm25_book_terms");
        builder.HasKey(e => new { e.BookId, e.Term });
        builder.Property(e => e.BookId).HasColumnName("book_id").IsRequired();
        builder.Property(e => e.Term).HasColumnName("term").IsRequired();
        builder.Property(e => e.DocumentFrequency).HasColumnName("document_frequency").HasDefaultValue(0);
        // The hot path deletes/loads all of a book's rows at once — index the book id.
        builder.HasIndex(e => e.BookId);
    }
}
