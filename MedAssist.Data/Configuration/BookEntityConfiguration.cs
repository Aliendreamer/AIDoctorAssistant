using MedAssist.Data.Entities;
using MedAssist.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedAssist.Data.Configuration;

public sealed class BookEntityConfiguration : IEntityTypeConfiguration<BookEntity>
{
    public void Configure(EntityTypeBuilder<BookEntity> builder)
    {
        builder.ToTable("books");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").UseIdentityColumn();
        builder.Property(e => e.BookId).HasColumnName("book_id").IsRequired();
        builder.HasIndex(e => e.BookId).IsUnique();
        builder.Property(e => e.Title).HasColumnName("title").IsRequired();
        builder.Property(e => e.Author).HasColumnName("author").IsRequired();
        builder.Property(e => e.Language).HasColumnName("language").IsRequired();
        builder.Property(e => e.Edition).HasColumnName("edition").HasDefaultValue(string.Empty);
        builder.Property(e => e.FilePath).HasColumnName("file_path").HasDefaultValue(string.Empty);
        builder.Property(e => e.TotalChunks).HasColumnName("total_chunks").HasDefaultValue(0);
        builder.Property(e => e.Status).HasColumnName("status").HasDefaultValue(BookStatus.Pending);
        builder.Property(e => e.IndexedAt).HasColumnName("indexed_at");
    }
}
