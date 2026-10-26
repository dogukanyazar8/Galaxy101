using Galaxy101.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Galaxy101.Server.Tests;

[Collection(ServerCollection.Name)]
public sealed class LocalReliabilityMigrationTests(ServerFixture server)
{
    [Fact]
    public async Task Upgrade_preserves_legacy_rows_sets_safe_defaults_and_enforces_unique_operation_keys()
    {
        // This test owns this database, including cleanup. Never migrate or delete the fixture's application database.
        var connection = new NpgsqlConnectionStringBuilder(server.DatabaseServer)
        {
            Database = "local_reliability_" + Guid.NewGuid().ToString("N"),
        };
        var options = new DbContextOptionsBuilder<GameDb>().UseNpgsql(connection.ConnectionString)
            .UseSnakeCaseNamingConvention().Options;
        await using var db = new GameDb(options);
        try
        {
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261024171803_RankedSeasons");
            var account = Guid.NewGuid();
            var match = Guid.NewGuid();
            await InsertLegacyAccountAsync(db, account, "LEGACY1");
            await db.Database.ExecuteSqlInterpolatedAsync($$"""
                INSERT INTO matches (id, mode, stake, status, seats, hands, first_starter, left_mask, bot_mask, turn_seconds, options_json, created_at)
                VALUES ({{match}}, 'solo', 1000, 'Completed', {{new[] { account, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() }}}, 4, 0, 0, 0, 30, '{"legacy":true}', now())
                """);
            await db.Database.ExecuteSqlInterpolatedAsync($$"""
                INSERT INTO match_participants (match_id, seat, account_id, mode, place, total, chip_delta, xp, "left", finished_at)
                VALUES ({{match}}, 0, {{account}}, 'solo', 2, 42, 1234, 777, false, now())
                """);

            await migrator.MigrateAsync();

            var savedAccount = await db.Accounts.AsNoTracking().SingleAsync(x => x.Id == account);
            Assert.Equal("Legacy Player", savedAccount.Name);
            Assert.Equal(0L, savedAccount.SessionVersion);
            Assert.Equal(7L, savedAccount.SettingsRevision);
            var savedMatch = await db.Matches.AsNoTracking().SingleAsync(x => x.Id == match);
            Assert.Equal(1000L, savedMatch.Stake);
            Assert.Equal(MatchStatus.Completed, savedMatch.Status);
            using var matchOptions = System.Text.Json.JsonDocument.Parse(savedMatch.OptionsJson);
            Assert.True(matchOptions.RootElement.GetProperty("legacy").GetBoolean());
            Assert.Null(savedMatch.SettlementJson);
            var participant = await db.MatchParticipants.AsNoTracking().SingleAsync(x => x.MatchId == match);
            Assert.Equal(account, participant.AccountId);
            Assert.Equal(42, participant.Total);
            Assert.Equal(1234L, participant.ChipDelta);
            Assert.Equal(777L, participant.Xp);
            Assert.Null(participant.OperationKey);

            // The database default also protects inserts made by a client that still omits the new column.
            var nextAccount = Guid.NewGuid();
            await InsertLegacyAccountAsync(db, nextAccount, "LEGACY2");
            Assert.Equal(0L, await db.Accounts.Where(x => x.Id == nextAccount).Select(x => x.SessionVersion).SingleAsync());

            MatchParticipant NewParticipant(string? key) => new()
            {
                MatchId = match, AccountId = account, Seat = 0, Mode = "solo", Place = 2,
                FinishedAt = DateTimeOffset.UtcNow, OperationKey = key,
            };
            db.MatchParticipants.AddRange(NewParticipant(null), NewParticipant(null), NewParticipant("migration:operation"));
            await db.SaveChangesAsync();
            Assert.Equal(3, await db.MatchParticipants.CountAsync(x => x.MatchId == match && x.OperationKey == null));
            db.MatchParticipants.Add(NewParticipant("migration:operation"));
            var duplicate = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(duplicate.GetBaseException()).SqlState);
            Assert.Equal(4, await db.MatchParticipants.CountAsync(x => x.MatchId == match));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    private static Task<int> InsertLegacyAccountAsync(GameDb db, Guid account, string friendCode) =>
        db.Database.ExecuteSqlInterpolatedAsync($$"""
            INSERT INTO accounts (id, friend_code, name, color, status, frame, planet, planets_tried, settings_json, settings_revision, created_at, last_seen_at)
            VALUES ({{account}}, {{friendCode}}, 'Legacy Player', '#3D7BFF', '', 0, 'Satürn', 0, '{}', 7, now(), now())
            """);
}
