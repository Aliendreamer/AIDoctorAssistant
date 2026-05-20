using MedAssist.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedAssist.Data.Configuration;

public sealed class ChatMessageEntityConfiguration : IEntityTypeConfiguration<ChatMessageEntity>
{
    public void Configure(EntityTypeBuilder<ChatMessageEntity> builder)
    {
        builder.ToTable("chat_messages", t =>
        {
            t.HasCheckConstraint("ck_chat_messages_query_type",
                "query_type IN ('disease', 'symptoms', 'treatment')");
            t.HasCheckConstraint("ck_chat_messages_role",
                "role IN ('user', 'assistant')");
        });
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").UseIdentityColumn();
        builder.Property(e => e.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(e => e.QueryType).HasColumnName("query_type").IsRequired();
        builder.Property(e => e.Role).HasColumnName("role").IsRequired();
        builder.Property(e => e.Content).HasColumnName("content").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");

        builder.HasIndex(e => new { e.UserId, e.QueryType, e.CreatedAt })
            .HasDatabaseName("ix_chat_messages_user_querytype_created");
    }
}
