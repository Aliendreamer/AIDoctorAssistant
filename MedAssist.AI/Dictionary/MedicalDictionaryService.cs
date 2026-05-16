using MedAssist.Shared.Interfaces;
using MedAssist.Shared.Models;
using Microsoft.Data.Sqlite;

namespace MedAssist.AI.Dictionary;

public sealed class MedicalDictionaryService : IMedicalDictionary
{
    private readonly string _connectionString;

    public MedicalDictionaryService(string databasePath)
    {
        _connectionString = $"Data Source={databasePath};Mode=ReadOnly";
    }

    public async Task<IReadOnlyList<string>> ExpandQueryAsync(string query, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT i.name_en, i.name_bg, a.alias
            FROM illnesses i
            LEFT JOIN illness_aliases a ON a.illness_id = i.id
            WHERE lower(i.name_en) = lower($query)
               OR lower(i.name_bg) = lower($query)
               OR lower(a.alias) = lower($query);
            """;
        cmd.Parameters.AddWithValue("$query", query);

        var terms = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { query };
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!reader.IsDBNull(0))
            {
                terms.Add(reader.GetString(0));
            }

            if (!reader.IsDBNull(1))
            {
                terms.Add(reader.GetString(1));
            }

            if (!reader.IsDBNull(2))
            {
                terms.Add(reader.GetString(2));
            }
        }

        return [.. terms];
    }

    public async Task<IllnessEntry?> GetByIcdAsync(string icdCode, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT i.id, i.icd_code, i.name_en, i.name_bg,
                   group_concat(a.alias, '|') AS aliases
            FROM illnesses i
            LEFT JOIN illness_aliases a ON a.illness_id = i.id
            WHERE upper(i.icd_code) = upper($icd)
            GROUP BY i.id;
            """;
        cmd.Parameters.AddWithValue("$icd", icdCode);

        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return ReadEntry(reader);
    }

    public async Task<IReadOnlyList<IllnessEntry>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT i.id, i.icd_code, i.name_en, i.name_bg,
                   group_concat(a.alias, '|') AS aliases
            FROM illnesses i
            LEFT JOIN illness_aliases a ON a.illness_id = i.id
            WHERE lower(i.name_en) LIKE '%' || lower($q) || '%'
               OR lower(i.name_bg) LIKE '%' || lower($q) || '%'
               OR upper(i.icd_code) LIKE upper($q) || '%'
            GROUP BY i.id
            ORDER BY i.name_en
            LIMIT 50;
            """;
        cmd.Parameters.AddWithValue("$q", query);

        var results = new List<IllnessEntry>();
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(ReadEntry(reader));
        }

        return results;
    }

    private static IllnessEntry ReadEntry(SqliteDataReader reader)
    {
        string? aliasesRaw = reader.IsDBNull(4) ? null : reader.GetString(4);
        string[] aliases = aliasesRaw?.Split('|', StringSplitOptions.RemoveEmptyEntries) ?? [];
        return new IllnessEntry
        {
            Id = Guid.Parse(reader.GetString(0)),
            IcdCode = reader.GetString(1),
            NameEn = reader.GetString(2),
            NameBg = reader.GetString(3),
            Aliases = aliases
        };
    }
}
