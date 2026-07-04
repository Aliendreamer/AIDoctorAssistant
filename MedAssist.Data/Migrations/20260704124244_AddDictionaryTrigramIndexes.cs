using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedAssist.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDictionaryTrigramIndexes : Migration
    {
        // Hand-written index migration (audit P2-18). These are expression / GIN-trigram indexes on
        // lower(name) that EF's fluent model can't express cleanly, so they live as raw SQL and are
        // deliberately kept OUT of the model — the model snapshot stays unchanged and EF won't try to
        // manage or drop them. Postgres-only (pg_trgm); never exercised by the SQLite test provider.
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pg_trgm;");

            // Equality on the lowered name/alias — MedicalDictionaryService.ExpandQueryAsync issues
            // `lower(name_en) IN (...)` (and name_bg / alias). B-tree expression indexes serve these.
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_illnesses_name_en_lower ON illnesses (lower(name_en));");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_illnesses_name_bg_lower ON illnesses (lower(name_bg));");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_illness_aliases_alias_lower ON illness_aliases (lower(alias));");

            // Substring search — MedicalDictionaryService.SearchAsync issues `lower(name_en) LIKE '%q%'`
            // (leading wildcard), which a b-tree can't serve. GIN trigram indexes accelerate it.
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_illnesses_name_en_trgm ON illnesses USING gin (lower(name_en) gin_trgm_ops);");
            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_illnesses_name_bg_trgm ON illnesses USING gin (lower(name_bg) gin_trgm_ops);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_illnesses_name_en_trgm;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_illnesses_name_bg_trgm;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_illness_aliases_alias_lower;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_illnesses_name_bg_lower;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_illnesses_name_en_lower;");
            // pg_trgm may be used elsewhere; leave the extension in place.
        }
    }
}
