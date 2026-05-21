using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MedAssist.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNewQueryTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_chat_messages_query_type",
                table: "chat_messages");

            migrationBuilder.AddCheckConstraint(
                name: "ck_chat_messages_query_type",
                table: "chat_messages",
                sql: "query_type IN ('disease', 'symptoms', 'treatment', 'globalsearch', 'differentialdiagnosis')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_chat_messages_query_type",
                table: "chat_messages");

            migrationBuilder.AddCheckConstraint(
                name: "ck_chat_messages_query_type",
                table: "chat_messages",
                sql: "query_type IN ('disease', 'symptoms', 'treatment')");
        }
    }
}
