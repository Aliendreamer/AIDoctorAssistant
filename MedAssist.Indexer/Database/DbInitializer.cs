using Microsoft.Data.Sqlite;

namespace MedAssist.Indexer.Database;

public sealed class DbInitializer
{
    private readonly string _connectionString;

    public string DatabasePath { get; }

    public DbInitializer(string databasePath)
    {
        DatabasePath = databasePath;
        _connectionString = $"Data Source={databasePath};Mode=ReadWriteCreate";
    }

    public void Initialize()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // Enable WAL mode for concurrent reads
        using (var walCmd = connection.CreateCommand())
        {
            walCmd.CommandText = "PRAGMA journal_mode=WAL;";
            walCmd.ExecuteNonQuery();
        }

        using var cmd = connection.CreateCommand();
        cmd.CommandText = _schema;
        cmd.ExecuteNonQuery();
    }

    public SqliteConnection CreateConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private const string _schema = """
        CREATE TABLE IF NOT EXISTS books (
            id          TEXT NOT NULL PRIMARY KEY,
            title       TEXT NOT NULL,
            author      TEXT NOT NULL,
            language    TEXT NOT NULL,
            edition     TEXT NOT NULL DEFAULT '',
            total_chunks INTEGER NOT NULL DEFAULT 0,
            status      TEXT NOT NULL DEFAULT 'pending',
            indexed_at  TEXT
        );

        CREATE TABLE IF NOT EXISTS illnesses (
            id          TEXT NOT NULL PRIMARY KEY DEFAULT (lower(hex(randomblob(16)))),
            icd_code    TEXT NOT NULL UNIQUE,
            name_en     TEXT NOT NULL,
            name_bg     TEXT NOT NULL,
            created_at  TEXT NOT NULL DEFAULT (datetime('now'))
        );

        CREATE TABLE IF NOT EXISTS illness_aliases (
            id          TEXT NOT NULL PRIMARY KEY DEFAULT (lower(hex(randomblob(16)))),
            illness_id  TEXT NOT NULL REFERENCES illnesses(id) ON DELETE CASCADE,
            alias       TEXT NOT NULL,
            language    TEXT NOT NULL
        );

        CREATE INDEX IF NOT EXISTS idx_illness_aliases_illness_id ON illness_aliases(illness_id);

        CREATE TABLE IF NOT EXISTS bm25_vocab (
            id                   INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
            term                 TEXT NOT NULL UNIQUE,
            document_frequency   INTEGER NOT NULL DEFAULT 0,
            total_documents      INTEGER NOT NULL DEFAULT 0,
            updated_at           TEXT NOT NULL DEFAULT (datetime('now'))
        );

        CREATE INDEX IF NOT EXISTS idx_bm25_vocab_term ON bm25_vocab(term);

        CREATE TABLE IF NOT EXISTS ingestion_checkpoints (
            book_id         TEXT NOT NULL PRIMARY KEY,
            total_chunks    INTEGER NOT NULL DEFAULT 0,
            indexed_chunks  INTEGER NOT NULL DEFAULT 0,
            last_chunk_index INTEGER NOT NULL DEFAULT -1,
            status          TEXT NOT NULL DEFAULT 'in_progress',
            updated_at      TEXT NOT NULL DEFAULT (datetime('now'))
        );
        """;
}
