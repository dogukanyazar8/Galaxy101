using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Galaxy101.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    friend_code = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    name = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    frame = table.Column<int>(type: "integer", nullable: false),
                    planet = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    planets_tried = table.Column<int>(type: "integer", nullable: false),
                    settings_json = table.Column<string>(type: "jsonb", nullable: false),
                    settings_revision = table.Column<long>(type: "bigint", nullable: false),
                    accepted_privacy_version = table.Column<string>(type: "text", nullable: true),
                    platform = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    device_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_accounts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "matches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mode = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    stake = table.Column<long>(type: "bigint", nullable: false),
                    status = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    seats = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    hands = table.Column<int>(type: "integer", nullable: false),
                    first_starter = table.Column<int>(type: "integer", nullable: false),
                    left_mask = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_matches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "daily_states",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    streak_count = table.Column<int>(type: "integer", nullable: false),
                    last_claim_day = table.Column<DateOnly>(type: "date", nullable: true),
                    quest_day = table.Column<DateOnly>(type: "date", nullable: false),
                    quest_progress = table.Column<int[]>(type: "integer[]", nullable: false),
                    quest_claimed_mask = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_daily_states", x => x.account_id);
                    table.ForeignKey(
                        name: "fk_daily_states_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "external_logins",
                columns: table => new
                {
                    provider = table.Column<string>(type: "text", nullable: false),
                    subject = table.Column<string>(type: "text", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_external_logins", x => new { x.provider, x.subject });
                    table.ForeignKey(
                        name: "fk_external_logins_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "friendships",
                columns: table => new
                {
                    low_id = table.Column<Guid>(type: "uuid", nullable: false),
                    high_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accepted = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_friendships", x => new { x.low_id, x.high_id });
                    table.CheckConstraint("ck_friendships_ordered", "low_id < high_id");
                    table.ForeignKey(
                        name: "fk_friendships_accounts_high_id",
                        column: x => x.high_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_friendships_accounts_low_id",
                        column: x => x.low_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "leaderboard_scores",
                columns: table => new
                {
                    board = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    points = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_leaderboard_scores", x => new { x.board, x.account_id });
                    table.ForeignKey(
                        name: "fk_leaderboard_scores_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ledger",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    currency = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    delta = table.Column<long>(type: "bigint", nullable: false),
                    balance_after = table.Column<long>(type: "bigint", nullable: false),
                    reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ledger", x => x.id);
                    table.ForeignKey(
                        name: "fk_ledger_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "owned_items",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<string>(type: "text", nullable: false),
                    acquired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_owned_items", x => new { x.account_id, x.item_id });
                    table.ForeignKey(
                        name: "fk_owned_items_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    rotated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_refresh_tokens", x => x.id);
                    table.ForeignKey(
                        name: "fk_refresh_tokens_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "stats",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xp = table.Column<long>(type: "bigint", nullable: false),
                    lp = table.Column<int>(type: "integer", nullable: false),
                    hands = table.Column<int>(type: "integer", nullable: false),
                    wins = table.Column<int>(type: "integer", nullable: false),
                    okey_finishes = table.Column<int>(type: "integer", nullable: false),
                    pairs_openings = table.Column<int>(type: "integer", nullable: false),
                    best_opening = table.Column<int>(type: "integer", nullable: false),
                    current_streak = table.Column<int>(type: "integer", nullable: false),
                    best_streak = table.Column<int>(type: "integer", nullable: false),
                    mode_hands = table.Column<int[]>(type: "integer[]", nullable: false),
                    mode_wins = table.Column<int[]>(type: "integer[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stats", x => x.account_id);
                    table.ForeignKey(
                        name: "fk_stats_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "store_purchases",
                columns: table => new
                {
                    platform = table.Column<string>(type: "text", nullable: false),
                    transaction_id = table.Column<string>(type: "text", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    product_id = table.Column<string>(type: "text", nullable: false),
                    offer_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_store_purchases", x => new { x.platform, x.transaction_id });
                    table.ForeignKey(
                        name: "fk_store_purchases_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "wallets",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chips = table.Column<long>(type: "bigint", nullable: false),
                    gems = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_wallets", x => x.account_id);
                    table.CheckConstraint("ck_wallets_non_negative", "chips >= 0 AND gems >= 0");
                    table.ForeignKey(
                        name: "fk_wallets_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "match_hands",
                columns: table => new
                {
                    match_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hand_index = table.Column<int>(type: "integer", nullable: false),
                    seed = table.Column<long>(type: "bigint", nullable: false),
                    round_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scores = table.Column<int[]>(type: "integer[]", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_match_hands", x => new { x.match_id, x.hand_index });
                    table.ForeignKey(
                        name: "fk_match_hands_matches_match_id",
                        column: x => x.match_id,
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "match_moves",
                columns: table => new
                {
                    match_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hand_index = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    seat = table.Column<int>(type: "integer", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_match_moves", x => new { x.match_id, x.hand_index, x.version });
                    table.ForeignKey(
                        name: "fk_match_moves_matches_match_id",
                        column: x => x.match_id,
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "match_participants",
                columns: table => new
                {
                    match_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seat = table.Column<int>(type: "integer", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    mode = table.Column<string>(type: "character varying(12)", maxLength: 12, nullable: false),
                    place = table.Column<int>(type: "integer", nullable: false),
                    total = table.Column<int>(type: "integer", nullable: false),
                    chip_delta = table.Column<long>(type: "bigint", nullable: false),
                    lp_delta = table.Column<int>(type: "integer", nullable: true),
                    xp = table.Column<long>(type: "bigint", nullable: false),
                    left = table.Column<bool>(type: "boolean", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_match_participants", x => new { x.match_id, x.seat });
                    table.ForeignKey(
                        name: "fk_match_participants_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_match_participants_matches_match_id",
                        column: x => x.match_id,
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_accounts_friend_code",
                table: "accounts",
                column: "friend_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_external_logins_account_id",
                table: "external_logins",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_friendships_high_id",
                table: "friendships",
                column: "high_id");

            migrationBuilder.CreateIndex(
                name: "ix_leaderboard_scores_account_id",
                table: "leaderboard_scores",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_leaderboard_scores_board_points",
                table: "leaderboard_scores",
                columns: new[] { "board", "points" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_ledger_account_id_idempotency_key",
                table: "ledger",
                columns: new[] { "account_id", "idempotency_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_match_participants_account_id_finished_at",
                table: "match_participants",
                columns: new[] { "account_id", "finished_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_matches_status",
                table: "matches",
                column: "status",
                filter: "status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_account_id",
                table: "refresh_tokens",
                column: "account_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_family_id",
                table: "refresh_tokens",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "ix_refresh_tokens_token_hash",
                table: "refresh_tokens",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_store_purchases_account_id",
                table: "store_purchases",
                column: "account_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "daily_states");

            migrationBuilder.DropTable(
                name: "external_logins");

            migrationBuilder.DropTable(
                name: "friendships");

            migrationBuilder.DropTable(
                name: "leaderboard_scores");

            migrationBuilder.DropTable(
                name: "ledger");

            migrationBuilder.DropTable(
                name: "match_hands");

            migrationBuilder.DropTable(
                name: "match_moves");

            migrationBuilder.DropTable(
                name: "match_participants");

            migrationBuilder.DropTable(
                name: "owned_items");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "stats");

            migrationBuilder.DropTable(
                name: "store_purchases");

            migrationBuilder.DropTable(
                name: "wallets");

            migrationBuilder.DropTable(
                name: "matches");

            migrationBuilder.DropTable(
                name: "accounts");
        }
    }
}
