using MedAssist.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedAssist.Data.Configuration;

public sealed class UserEntityConfiguration : IEntityTypeConfiguration<UserEntity>
{
    public void Configure(EntityTypeBuilder<UserEntity> builder)
    {
        builder.ToTable("users");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.Username).HasColumnName("username").HasMaxLength(100).IsRequired();
        builder.Property(e => e.PasswordHash).HasColumnName("password_hash").IsRequired();
        builder.Property(e => e.Role).HasColumnName("role").HasMaxLength(50).IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("now()");
        builder.HasIndex(e => e.Username).IsUnique();
    }
}
