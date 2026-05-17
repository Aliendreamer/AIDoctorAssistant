using System;
using MedAssist.Shared.Models;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MedAssist.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:BookStatus", "failed,in_progress,indexed,pending");

            migrationBuilder.CreateTable(
                name: "bm25_stats",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    total_documents = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bm25_stats", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bm25_vocab",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityAlwaysColumn),
                    term = table.Column<string>(type: "text", nullable: false),
                    document_frequency = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bm25_vocab", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "books",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    book_id = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    author = table.Column<string>(type: "text", nullable: false),
                    language = table.Column<string>(type: "text", nullable: false),
                    edition = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    file_path = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    total_chunks = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    status = table.Column<BookStatus>(type: "\"BookStatus\"", nullable: false, defaultValue: BookStatus.Pending),
                    indexed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_books", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "illnesses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    icd_code = table.Column<string>(type: "text", nullable: false),
                    name_en = table.Column<string>(type: "text", nullable: false),
                    name_bg = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_illnesses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ingestion_checkpoints",
                columns: table => new
                {
                    book_id = table.Column<string>(type: "text", nullable: false),
                    total_chunks = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    indexed_chunks = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_chunk_index = table.Column<int>(type: "integer", nullable: false, defaultValue: -1),
                    status = table.Column<BookStatus>(type: "\"BookStatus\"", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingestion_checkpoints", x => x.book_id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    username = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    password_hash = table.Column<string>(type: "text", nullable: false),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "illness_aliases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    illness_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alias = table.Column<string>(type: "text", nullable: false),
                    language = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_illness_aliases", x => x.id);
                    table.ForeignKey(
                        name: "FK_illness_aliases_illnesses_illness_id",
                        column: x => x.illness_id,
                        principalTable: "illnesses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_bm25_vocab_term",
                table: "bm25_vocab",
                column: "term",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_books_book_id",
                table: "books",
                column: "book_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_illness_aliases_illness_id",
                table: "illness_aliases",
                column: "illness_id");

            migrationBuilder.CreateIndex(
                name: "IX_illnesses_icd_code",
                table: "illnesses",
                column: "icd_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_username",
                table: "users",
                column: "username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bm25_stats");

            migrationBuilder.DropTable(
                name: "bm25_vocab");

            migrationBuilder.DropTable(
                name: "books");

            migrationBuilder.DropTable(
                name: "illness_aliases");

            migrationBuilder.DropTable(
                name: "ingestion_checkpoints");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "illnesses");
        }
    }
}
