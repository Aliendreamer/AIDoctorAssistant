using MedAssist.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MedAssist.Data;

public sealed class MedAssistDbContext : DbContext
{
    public MedAssistDbContext(DbContextOptions<MedAssistDbContext> options) : base(options) { }

    public DbSet<BookEntity> Books => Set<BookEntity>();
    public DbSet<IllnessEntity> Illnesses => Set<IllnessEntity>();
    public DbSet<IllnessAliasEntity> IllnessAliases => Set<IllnessAliasEntity>();
    public DbSet<Bm25VocabEntity> Bm25Vocab => Set<Bm25VocabEntity>();
    public DbSet<Bm25StatsEntity> Bm25Stats => Set<Bm25StatsEntity>();
    public DbSet<IngestionCheckpointEntity> IngestionCheckpoints => Set<IngestionCheckpointEntity>();
    public DbSet<UserEntity> Users => Set<UserEntity>();
    public DbSet<ChatMessageEntity> ChatMessages => Set<ChatMessageEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.UseIdentityAlwaysColumns();
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MedAssistDbContext).Assembly);
    }
}
