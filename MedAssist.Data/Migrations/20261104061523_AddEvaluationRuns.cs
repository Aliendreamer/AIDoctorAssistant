using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MedAssist.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddEvaluationRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "evaluation_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    phase = table.Column<string>(type: "text", nullable: false),
                    done = table.Column<int>(type: "integer", nullable: false),
                    total = table.Column<int>(type: "integer", nullable: false),
                    started_by = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_baseline = table.Column<bool>(type: "boolean", nullable: false),
                    snapshot = table.Column<string>(type: "text", nullable: false),
                    summary = table.Column<string>(type: "text", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluation_runs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "evaluation_results",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    case_id = table.Column<string>(type: "text", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false),
                    details = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_evaluation_results", x => x.id);
                    table.ForeignKey(
                        name: "FK_evaluation_results_evaluation_runs_run_id",
                        column: x => x.run_id,
                        principalTable: "evaluation_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_evaluation_results_run_case",
                table: "evaluation_results",
                columns: new[] { "run_id", "case_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_evaluation_runs_created_at",
                table: "evaluation_runs",
                column: "created_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "evaluation_results");

            migrationBuilder.DropTable(
                name: "evaluation_runs");
        }
    }
}
