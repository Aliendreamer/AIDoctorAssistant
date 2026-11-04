using MedAssist.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MedAssist.Data.Configuration;

public sealed class EvaluationResultEntityConfiguration : IEntityTypeConfiguration<EvaluationResultEntity>
{
    public void Configure(EntityTypeBuilder<EvaluationResultEntity> builder)
    {
        builder.ToTable("evaluation_results");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id").UseIdentityColumn();
        builder.Property(e => e.RunId).HasColumnName("run_id");
        builder.Property(e => e.CaseId).HasColumnName("case_id").IsRequired();
        builder.Property(e => e.Ordinal).HasColumnName("ordinal");
        builder.Property(e => e.Details).HasColumnName("details").IsRequired();

        builder.HasOne<EvaluationRunEntity>().WithMany().HasForeignKey(e => e.RunId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(e => new { e.RunId, e.CaseId }).IsUnique().HasDatabaseName("ux_evaluation_results_run_case");
    }
}
