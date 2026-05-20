using MedAssist.Shared.Models;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedAssist.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBookOutline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:book_status", "failed,in_progress,indexed,pending")
                .OldAnnotation("Npgsql:Enum:BookStatus", "failed,in_progress,indexed,pending");

            migrationBuilder.AlterColumn<BookStatus>(
                name: "status",
                table: "ingestion_checkpoints",
                type: "book_status",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "\"BookStatus\"");

            migrationBuilder.AlterColumn<BookStatus>(
                name: "status",
                table: "books",
                type: "book_status",
                nullable: false,
                defaultValue: BookStatus.Pending,
                oldClrType: typeof(int),
                oldType: "\"BookStatus\"",
                oldDefaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Outline",
                table: "books",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Outline",
                table: "books");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:Enum:BookStatus", "failed,in_progress,indexed,pending")
                .OldAnnotation("Npgsql:Enum:book_status", "failed,in_progress,indexed,pending");

            migrationBuilder.AlterColumn<int>(
                name: "status",
                table: "ingestion_checkpoints",
                type: "\"BookStatus\"",
                nullable: false,
                oldClrType: typeof(BookStatus),
                oldType: "book_status");

            migrationBuilder.AlterColumn<int>(
                name: "status",
                table: "books",
                type: "\"BookStatus\"",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(BookStatus),
                oldType: "book_status",
                oldDefaultValue: BookStatus.Pending);
        }
    }
}
