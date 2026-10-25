using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Galaxy101.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class RankedSeasons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "best_lp",
                table: "stats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "lp_season",
                table: "stats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "shield",
                table: "stats",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "season_results",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    season = table.Column<int>(type: "integer", nullable: false),
                    best_lp = table.Column<int>(type: "integer", nullable: false),
                    league = table.Column<string>(type: "character varying(4)", maxLength: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_season_results", x => new { x.account_id, x.season });
                    table.ForeignKey(
                        name: "fk_season_results_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "season_results");

            migrationBuilder.DropColumn(
                name: "best_lp",
                table: "stats");

            migrationBuilder.DropColumn(
                name: "lp_season",
                table: "stats");

            migrationBuilder.DropColumn(
                name: "shield",
                table: "stats");
        }
    }
}
