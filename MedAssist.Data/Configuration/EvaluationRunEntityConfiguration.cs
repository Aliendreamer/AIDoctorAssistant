using MedAssist.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedAssist.Data.Configuration;

public sealed class EvaluationRunEntityConfiguration : IEntityTypeConfiguration<EvaluationRunEntity>
{
    public void Configure(EntityTypeBuilder<EvaluationRunEntity> builder)
    {
        builder.ToTable("evaluation_runs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(e => e.Note).HasColumnName("note").IsRequired();
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(e => e.Phase).HasColumnName("phase").IsRequired();
        builder.Property(e => e.Done).HasColumnName("done");
        builder.Property(e => e.Total).HasColumnName("total");
        builder.Property(e => e.StartedBy).HasColumnName("started_by").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.CompletedAt).HasColumnName("completed_at");
        builder.Property(e => e.IsBaseline).HasColumnName("is_baseline");
        builder.Property(e => e.Snapshot).HasColumnName("snapshot").IsRequired();
        builder.Property(e => e.Summary).HasColumnName("summary");
        builder.Property(e => e.Error).HasColumnName("error");

        builder.HasIndex(e => e.CreatedAt).HasDatabaseName("ix_evaluation_runs_created_at");
    }
}
