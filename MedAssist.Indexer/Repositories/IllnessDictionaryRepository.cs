using MedAssist.Indexer.Database;
using MedAssist.Shared.Models;
using Microsoft.Data.Sqlite;

namespace MedAssist.Indexer.Repositories;

public sealed class IllnessDictionaryRepository
{
    private readonly DbInitializer _db;

    public IllnessDictionaryRepository(DbInitializer db) => _db = db;

    public async Task AddAsync(string icdCode, string nameEn, string nameBg, CancellationToken cancellationToken = default)
    {
        await using var connection = _db.CreateConnection();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO illnesses (icd_code, name_en, name_bg)
            VALUES ($icdCode, $nameEn, $nameBg)
            ON CONFLICT(icd_code) DO UPDATE SET
                name_en = excluded.name_en,
                name_bg = excluded.name_bg;
            """;
        cmd.Parameters.AddWithValue("$icdCode", icdCode);
        cmd.Parameters.AddWithValue("$nameEn", nameEn);
        cmd.Parameters.AddWithValue("$nameBg", nameBg);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IllnessEntry>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _db.CreateConnection();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT id, icd_code, name_en, name_bg FROM illnesses ORDER BY name_en;";

        var results = new List<IllnessEntry>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = Guid.Parse(reader.GetString(0));
            var aliases = await GetAliasesAsync(connection, id.ToString(), cancellationToken);
            results.Add(new IllnessEntry
            {
                Id = id,
                IcdCode = reader.GetString(1),
                NameEn = reader.GetString(2),
                NameBg = reader.GetString(3),
                Aliases = aliases
            });
        }

        return results;
    }

    public async Task<IllnessEntry?> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        await using var connection = _db.CreateConnection();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT i.id, i.icd_code, i.name_en, i.name_bg
            FROM illnesses i
            WHERE lower(i.name_en) = lower($name)
               OR lower(i.name_bg) = lower($name)
               OR EXISTS (
                   SELECT 1 FROM illness_aliases a
                   WHERE a.illness_id = i.id AND lower(a.alias) = lower($name)
               )
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("$name", name);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var id = Guid.Parse(reader.GetString(0));
        var icdCode = reader.GetString(1);
        var nameEn = reader.GetString(2);
        var nameBg = reader.GetString(3);
        var aliases = await GetAliasesAsync(connection, id.ToString(), cancellationToken);

        return new IllnessEntry
        {
            Id = id,
            IcdCode = icdCode,
            NameEn = nameEn,
            NameBg = nameBg,
            Aliases = aliases
        };
    }

    public async Task<IReadOnlyList<string>> ExpandQueryAsync(string query, CancellationToken cancellationToken = default)
    {
        var entry = await FindByNameAsync(query, cancellationToken);
        if (entry is null)
        {
            return [query];
        }

        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { query };
        terms.Add(entry.NameEn);
        terms.Add(entry.NameBg);
        foreach (var alias in entry.Aliases)
        {
            terms.Add(alias);
        }

        return [.. terms];
    }

    private static async Task<IReadOnlyList<string>> GetAliasesAsync(
        SqliteConnection connection,
        string illnessId,
        CancellationToken cancellationToken)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT alias FROM illness_aliases WHERE illness_id = $illnessId;";
        cmd.Parameters.AddWithValue("$illnessId", illnessId);

        var aliases = new List<string>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            aliases.Add(reader.GetString(0));
        }

        return aliases;
    }
}
