using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Galaxy101.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class LobbyTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "bot_mask",
                table: "matches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "options_json",
                table: "matches",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "turn_seconds",
                table: "matches",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "bot_mask",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "options_json",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "turn_seconds",
                table: "matches");
        }
    }
}
