using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedAssist.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBm25BookContributions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bm25_book_stats",
                columns: table => new
                {
                    book_id = table.Column<string>(type: "text", nullable: false),
                    chunk_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bm25_book_stats", x => x.book_id);
                });

            migrationBuilder.CreateTable(
                name: "bm25_book_terms",
                columns: table => new
                {
                    book_id = table.Column<string>(type: "text", nullable: false),
                    term = table.Column<string>(type: "text", nullable: false),
                    document_frequency = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bm25_book_terms", x => new { x.book_id, x.term });
                });

            migrationBuilder.CreateIndex(
                name: "IX_bm25_book_terms_book_id",
                table: "bm25_book_terms",
                column: "book_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bm25_book_stats");

            migrationBuilder.DropTable(
                name: "bm25_book_terms");
        }
    }
}
