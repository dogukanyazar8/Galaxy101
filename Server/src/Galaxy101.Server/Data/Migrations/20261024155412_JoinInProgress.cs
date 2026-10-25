using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Galaxy101.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class JoinInProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_match_participants",
                table: "match_participants");

            migrationBuilder.AddColumn<long>(
                name: "id",
                table: "match_participants",
                type: "bigint",
                nullable: false)
                // An identity column numbers the existing rows itself (no shared default that would collide).
                .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

            migrationBuilder.AddPrimaryKey(
                name: "pk_match_participants",
                table: "match_participants",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "ix_match_participants_match_id_seat",
                table: "match_participants",
                columns: new[] { "match_id", "seat" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_match_participants",
                table: "match_participants");

            migrationBuilder.DropIndex(
                name: "ix_match_participants_match_id_seat",
                table: "match_participants");

            migrationBuilder.DropColumn(
                name: "id",
                table: "match_participants");

            migrationBuilder.AddPrimaryKey(
                name: "pk_match_participants",
                table: "match_participants",
                columns: new[] { "match_id", "seat" });
        }
    }
}
