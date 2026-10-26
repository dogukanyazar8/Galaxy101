using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Galaxy101.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class LocalReliability : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "settlement_json",
                table: "matches",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "operation_key",
                table: "match_participants",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "session_version",
                table: "accounts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateIndex(
                name: "ix_match_participants_operation_key",
                table: "match_participants",
                column: "operation_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_match_participants_operation_key",
                table: "match_participants");

            migrationBuilder.DropColumn(
                name: "settlement_json",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "operation_key",
                table: "match_participants");

            migrationBuilder.DropColumn(
                name: "session_version",
                table: "accounts");
        }
    }
}
