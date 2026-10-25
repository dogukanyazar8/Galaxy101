using System.Net.Http.Json;
using Galaxy101.Contracts;
using Galaxy101.Server.Data;
using Galaxy101.Server.Games;
using Galaxy101.Server.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Galaxy101.Server.Tests;

[Collection(ServerCollection.Name)]
public sealed class WatchJoinTests(ServerFixture server)
{
    private sealed record Player(HttpClient Http, TokenResponse Tokens, RealtimeClient Live);

    // ---- Settlement: a bot stakes nothing and takes nothing ----------------------------------------------------

    [Fact]
    public void A_table_with_bots_pays_out_only_what_the_people_put_in()
    {
        var options = new GameOptions();
        // One person (seat 0) and three bots: the person wins back only their own stake.
        var alone = Settlement.Settle(GameModes.Solo, 100_000, [-101, 50, 60, 70], 0, options, botMask: 0b1110);
        Assert.Equal(100_000, alone[0].Payout);
        Assert.Equal(0, alone[0].ChipDelta);
        Assert.All(alone.Skip(1), o => Assert.Equal(0, o.Payout));

        // Two people, a bot wins: both stakes are lost, nothing is paid to anyone.
        var botWins = Settlement.Settle(GameModes.Solo, 1000, [50, -101, 60, 70], 0, options, botMask: 0b0010);
        Assert.Equal(0, botWins.Sum(o => o.Payout));
        Assert.Equal(-1000, botWins[0].ChipDelta);
        Assert.Equal(0, botWins[1].ChipDelta);

        // Pairs, a person with a bot partner wins: the person takes the whole pot of the three stakes.
        var pairs = Settlement.Settle(GameModes.Pairs, 1000, [-101, 50, 20, 70], 0, options, botMask: 0b0100);
        Assert.Equal(3000, pairs[0].Payout);
        Assert.Equal(0, pairs[2].Payout);
        Assert.Equal(3000, pairs.Sum(o => o.Payout));

        // Without bots nothing changes: the winner takes the four stakes.
        Assert.Equal(4000, Settlement.Settle(GameModes.Solo, 1000, [-101, 50, 60, 70], 0, options)[0].Payout);
    }

    // ---- İzle and Oyuna katıl at a Klasik table -----------------------------------------------------------------

    [Fact]
    public async Task A_spectator_sees_no_tiles_and_a_newcomer_takes_a_bot_seat_and_pays_in()
    {
        var host = await ConnectAsync();
        var watcher = await ConnectAsync();
        var newcomer = await ConnectAsync();
        try
        {
            // Host + three bots, spectators allowed.
            Assert.True((await host.Live.RequestAsync(LobbyMessageTypes.Create, new TableCreateMessage { Options = Options(1000) })).Ok);
            var room = await host.Live.WaitAsync<TableStateMessage>(LobbyMessageTypes.State);
            for (var seat = 1; seat < 4; seat++)
                Assert.True((await host.Live.RequestAsync(LobbyMessageTypes.AddBot, new TableSeatMessage { Seat = seat })).Ok);
            Assert.True((await host.Live.RequestAsync(LobbyMessageTypes.Start, new { })).Ok);
            for (var i = 0; i < 8 && !host.Live.Received(MessageTypes.MatchStart); i++) { server.Clock.Advance(TimeSpan.FromSeconds(1)); await Task.Delay(50); }
            var start = await host.Live.WaitAsync<MatchStartMessage>(MessageTypes.MatchStart);
            await host.Live.WaitAsync<MatchStateMessage>(MessageTypes.MatchState);
            var tableId = room.Table.Id;

            // İzle: the match from seat 0, with nobody's tiles.
            Assert.True((await watcher.Live.RequestAsync(LobbyMessageTypes.Watch, new TableJoinMessage { TableId = tableId })).Ok);
            var seen = await watcher.Live.WaitAsync<MatchStartMessage>(MessageTypes.MatchStart);
            Assert.True(seen.Spectator);
            Assert.Equal(start.MatchId, seen.MatchId);
            var view = await watcher.Live.WaitAsync<MatchStateMessage>(MessageTypes.MatchState);
            Assert.Empty(view.View.Hand);
            Assert.Null(view.View.PendingDraw);
            Assert.Equal(21 * 3 + 22, view.View.Players.Sum(p => p.HandCount));
            // A spectator cannot play.
            var move = await watcher.Live.RequestAsync(MessageTypes.MatchCommand, new MatchCommandMessage { MatchId = start.MatchId, Kind = "draw", Source = "stock" });
            Assert.False(move.Ok);

            // Oyuna katıl: a bot's seat, paid with the table's stake.
            var before = (await newcomer.Http.GetFromJsonAsync<WalletResponse>("/v1/wallet"))!.Chips;
            Assert.True((await newcomer.Live.RequestAsync(LobbyMessageTypes.Join, new TableJoinMessage { TableId = tableId, Seat = 2 })).Ok);
            var mine = await newcomer.Live.WaitAsync<MatchStartMessage>(MessageTypes.MatchStart);
            Assert.False(mine.Spectator);
            Assert.Equal(2, mine.YourSeat);
            Assert.Equal(before - 1000, (await newcomer.Http.GetFromJsonAsync<WalletResponse>("/v1/wallet"))!.Chips);
            var seats = await host.Live.WaitAsync<MatchSeatsMessage>(MessageTypes.MatchSeats);
            Assert.Equal(newcomer.Tokens.AccountId, seats.Seats.Single(s => s.Seat == 2).Player.Id);
            // The newcomer sees their own tiles; the spectator still none.
            var own = await newcomer.Live.WaitAsync<MatchStateMessage>(MessageTypes.MatchState);
            Assert.NotEmpty(own.View.Hand);
            Assert.Empty((await watcher.Live.WaitAsync<MatchStateMessage>(MessageTypes.MatchState, s => s.Seats.Any(x => x.Seat == 2 && x.Player.Id == newcomer.Tokens.AccountId))).View.Hand);
            // The seat is no longer free; being seated, the newcomer cannot take another.
            Assert.False((await newcomer.Live.RequestAsync(LobbyMessageTypes.Join, new TableJoinMessage { TableId = tableId, Seat = 1 })).Ok);

            // match.leave stops watching.
            Assert.True((await watcher.Live.RequestAsync(MessageTypes.MatchLeave, new MatchRefMessage { MatchId = start.MatchId })).Ok);
        }
        finally
        {
            foreach (var p in new[] { host, newcomer })
            {
                var current = p.Live.Received(MessageTypes.MatchStart) ? (await p.Live.WaitAsync<MatchStartMessage>(MessageTypes.MatchStart)).MatchId : Guid.Empty;
                if (current != Guid.Empty) await p.Live.SendAsync(MessageTypes.MatchLeave, new MatchRefMessage { MatchId = current });
                await p.Live.SendAsync(LobbyMessageTypes.Leave);
            }
            await Task.Delay(100);
            foreach (var p in new[] { host, watcher, newcomer }) await p.Live.DisposeAsync();
        }
    }

    [Fact]
    public async Task Watching_needs_the_table_to_allow_spectators()
    {
        var host = await ConnectAsync();
        var watcher = await ConnectAsync();
        try
        {
            var options = Options(1000); options.Spectators = false;
            Assert.True((await host.Live.RequestAsync(LobbyMessageTypes.Create, new TableCreateMessage { Options = options })).Ok);
            var room = await host.Live.WaitAsync<TableStateMessage>(LobbyMessageTypes.State);
            Assert.Equal(ErrorCodes.Validation, (await watcher.Live.RequestAsync(LobbyMessageTypes.Watch, new TableJoinMessage { TableId = room.Table.Id })).Code);
        }
        finally
        {
            await host.Live.SendAsync(LobbyMessageTypes.Leave);
            await host.Live.DisposeAsync(); await watcher.Live.DisposeAsync();
        }
    }

    // ---- The participants migration keeps existing rows ------------------------------------------------------------

    [Fact]
    public async Task Moving_to_a_row_key_keeps_every_stored_participant()
    {
        var builder = new NpgsqlConnectionStringBuilder(server.DatabaseServer) { Database = "migration_" + Guid.NewGuid().ToString("N")[..12] };
        var options = new DbContextOptionsBuilder<GameDb>().UseNpgsql(builder.ConnectionString).UseSnakeCaseNamingConvention().Options;
        await using var db = new GameDb(options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync("20261024153823_ModerationAndSearch");
        var match = Guid.NewGuid();
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO matches (id, mode, stake, status, seats, hands, first_starter, left_mask, bot_mask, turn_seconds, options_json, created_at)
            VALUES (@id, 'solo', 1000, 'Completed', @seats, 4, 0, 0, 0, 0, '{{}}', now())
            """, new NpgsqlParameter("id", match), new NpgsqlParameter("seats", new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() }));
        for (var seat = 0; seat < 4; seat++)
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO match_participants (match_id, seat, mode, place, total, chip_delta, xp, "left", finished_at)
                VALUES ({match}, {seat}, 'solo', {seat + 1}, 0, 0, 0, false, now())
                """);
        await migrator.MigrateAsync();
        var ids = await db.MatchParticipants.Where(p => p.MatchId == match).Select(p => p.Id).ToListAsync();
        Assert.Equal(4, ids.Distinct().Count());
        // New rows keep numbering after the old ones.
        db.MatchParticipants.Add(new MatchParticipant { MatchId = match, Seat = 0, Mode = "solo", Place = 4, FinishedAt = DateTimeOffset.UtcNow, Left = true });
        await db.SaveChangesAsync();
        await db.Database.EnsureDeletedAsync();
    }

    // ---- A lost write answer is retried without wedging the match --------------------------------------------------

    [Fact]
    public async Task A_hand_or_move_stored_twice_is_written_over_but_a_played_hand_is_kept()
    {
        var store = (MatchStore)server.Services.GetService(typeof(MatchStore))!;
        var (_, a) = await server.GuestAsync(); var (_, b) = await server.GuestAsync(); var (_, c) = await server.GuestAsync(); var (_, d) = await server.GuestAsync();
        var record = new MatchRecord
        {
            Id = Guid.CreateVersion7(), Mode = GameModes.Private, Stake = 0, Status = MatchStatus.Active, Hands = 2,
            Seats = [a.AccountId, b.AccountId, c.AccountId, d.AccountId], CreatedAt = DateTimeOffset.UtcNow,
        };
        Assert.Empty(await store.CreateAsync(record, default));
        await store.StartHandAsync(record.Id, 0, 7, Guid.NewGuid());
        await store.StartHandAsync(record.Id, 0, 8, Guid.NewGuid()); // the first answer was "lost"
        await store.AppendMoveAsync(record.Id, 0, 0, 1, """{"kind":"draw"}""");
        await store.AppendMoveAsync(record.Id, 0, 0, 1, """{"kind":"draw","source":"stock"}""");
        // Once moves are stored the hand is in play: dealing it again is refused, not silently replaced.
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.StartHandAsync(record.Id, 0, 9, Guid.NewGuid()));
    }

    // ---- helpers ------------------------------------------------------------------------------------------------

    private static TableOptions Options(long stake) => new() { Name = "İzle Test", Type = "single", Visibility = "public", Hands = 3, Stake = stake, TurnSeconds = 30, Spectators = true };

    private async Task<Player> ConnectAsync()
    {
        var (http, tokens) = await server.GuestAsync();
        return new Player(http, tokens, await RealtimeClient.ConnectAsync(server, http));
    }
}
