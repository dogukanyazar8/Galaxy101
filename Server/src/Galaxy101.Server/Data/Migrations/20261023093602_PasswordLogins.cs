using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Galaxy101.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class PasswordLogins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "password_logins",
                columns: table => new
                {
                    email = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    failed_attempts = table.Column<int>(type: "integer", nullable: false),
                    locked_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_password_logins", x => x.email);
                    table.ForeignKey(
                        name: "fk_password_logins_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_password_logins_account_id",
                table: "password_logins",
                column: "account_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "password_logins");
        }
    }
}
