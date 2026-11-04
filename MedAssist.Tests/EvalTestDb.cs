using MedAssist.Data;
using MedAssist.Data.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MedAssist.Tests;

/// <summary>
/// Real SQLite database for the evaluation tables, so the repository's SQL is actually translated.
/// SQLite cannot ORDER BY a DateTimeOffset, so timestamps are stored as sortable integers here only.
/// </summary>
internal sealed class EvalTestDb : IDisposable
{
    private readonly SqliteConnection _connection = new("Filename=:memory:");

    public EvalTestDb()
    {
        _connection.Open();
        using var db = CreateContext();
        db.Database.ExecuteSqlRaw(
            """
            CREATE TABLE evaluation_runs (id TEXT NOT NULL PRIMARY KEY, note TEXT NOT NULL, status TEXT NOT NULL,
                phase TEXT NOT NULL, done INTEGER NOT NULL, total INTEGER NOT NULL, started_by TEXT NOT NULL,
                created_at INTEGER NOT NULL, completed_at INTEGER NULL, is_baseline INTEGER NOT NULL,
                snapshot TEXT NOT NULL, summary TEXT NULL, error TEXT NULL);
            CREATE TABLE evaluation_results (id INTEGER PRIMARY KEY AUTOINCREMENT, run_id TEXT NOT NULL
                REFERENCES evaluation_runs(id) ON DELETE CASCADE, case_id TEXT NOT NULL, ordinal INTEGER NOT NULL,
                details TEXT NOT NULL, UNIQUE (run_id, case_id));
            CREATE TABLE chat_messages (id INTEGER PRIMARY KEY AUTOINCREMENT, user_id TEXT NOT NULL,
                query_type TEXT NOT NULL, role TEXT NOT NULL, content TEXT NOT NULL, created_at INTEGER NOT NULL);
            CREATE TABLE books (id INTEGER PRIMARY KEY, book_id TEXT, title TEXT, author TEXT, language TEXT, edition TEXT,
                file_path TEXT, total_chunks INTEGER, status TEXT, indexed_at INTEGER, outline TEXT);
            """);
    }

    public MedAssistDbContext CreateContext() => new(
        new DbContextOptionsBuilder<MedAssistDbContext>()
            .UseSqlite(_connection)
            .ReplaceService<IModelCustomizer, SortableTimestampCustomizer>()
            .Options);

    public void Dispose() => _connection.Dispose();

    private sealed class SortableTimestampCustomizer(ModelCustomizerDependencies dependencies) : ModelCustomizer(dependencies)
    {
        public override void Customize(ModelBuilder modelBuilder, DbContext context)
        {
            base.Customize(modelBuilder, context);
            var converter = new DateTimeOffsetToBinaryConverter();
            modelBuilder.Entity<EvaluationRunEntity>().Property(e => e.CreatedAt).HasConversion(converter);
            modelBuilder.Entity<EvaluationRunEntity>().Property(e => e.CompletedAt).HasConversion(converter);
            modelBuilder.Entity<ChatMessageEntity>().Property(e => e.CreatedAt).HasConversion(converter);
        }
    }
}
