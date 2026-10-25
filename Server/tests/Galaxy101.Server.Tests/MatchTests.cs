using System.Net;
using System.Net.Http.Json;
using Galaxy101.Contracts;
using Galaxy101.Server.Economy;
using Galaxy101.Server.Games;
using Galaxy101.Server.Infrastructure;
using Galaxy101.Server.Realtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Galaxy101.Server.Tests;

[Collection(ServerCollection.Name)]
public sealed class MatchTests(ServerFixture server)
{
    private sealed record Player(HttpClient Http, TokenResponse Tokens, RealtimeClient Live)
    {
        public TokenResponse Tokens { get; set; } = Tokens;
        public MatchStartMessage Start { get; set; } = null!;
    }

    [Fact]
    public async Task Queue_forms_a_table_with_private_hands_and_server_checked_moves()
    {
        var table = await SeatAsync(GameModes.Solo, 1000);
        try
        {
            var states = await Task.WhenAll(table.Select(p => p.Live.WaitAsync<MatchStateMessage>(MessageTypes.MatchState)));
            Assert.Single(states.Select(s => s.MatchId).Distinct());
            Assert.Equal([0, 1, 2, 3], states.Select(s => s.View.Seat).Order());
            Assert.All(states, s => Assert.Contains(s.View.Hand.Count, new[] { 21, 22 }));
            Assert.Equal(21 * 3 + 22, states[0].View.Players.Sum(p => p.HandCount));
            // Every seat sees only its own tiles.
            Assert.Equal(states.Sum(s => s.View.Hand.Count), states.SelectMany(s => s.View.Hand).Distinct().Count());
            foreach (var p in table) Assert.Equal(9000, (await p.Http.GetFromJsonAsync<WalletResponse>("/v1/wallet"))!.Chips);

            var activeSeat = states[0].View.ActiveSeat;
            var active = table.Single(p => p.Start.YourSeat == activeSeat);
            var activeState = states.Single(s => s.View.Seat == activeSeat);
            var other = table.First(p => p.Start.YourSeat != activeSeat);
            var matchId = activeState.MatchId;

            var wrong = await other.Live.RequestAsync(MessageTypes.MatchCommand, Commit(matchId, activeState.View.Version, activeState.View.Hand[0], false));
            Assert.Equal("rule.wrongPlayer", wrong.Code);
            var garbage = await active.Live.RequestAsync(MessageTypes.MatchCommand, new { matchId, kind = "teleport" });
            Assert.Equal(ErrorCodes.Validation, garbage.Code);

            // The starter discards (some tiles may need the penalty confirmation; one of them is always legal).
            int discarded = -1;
            foreach (var tile in activeState.View.Hand)
            {
                foreach (var penalty in new[] { false, true })
                    if ((await active.Live.RequestAsync(MessageTypes.MatchCommand, Commit(matchId, activeState.View.Version, tile, penalty))).Ok) { discarded = tile; break; }
                if (discarded >= 0) break;
            }
            Assert.True(discarded >= 0);
            var next = await other.Live.WaitAsync<MatchStateMessage>(MessageTypes.MatchState, s => s.LastEvent?.Kind == "commit");
            Assert.Equal(discarded, next.LastEvent!.Tile);
            Assert.Equal((activeSeat + 1) % 4, next.View.ActiveSeat);
            Assert.Equal("awaitingDraw", next.View.Phase);

            var stale = await active.Live.RequestAsync(MessageTypes.MatchCommand, new MatchCommandMessage { MatchId = matchId, ExpectedVersion = 0, Kind = "draw", Source = "stock" });
            Assert.Equal("rule.wrongPlayer", stale.Code);

            // Table extras: emotes are rate limited, gifts are paid for.
            Assert.True((await active.Live.RequestAsync(MessageTypes.MatchEmote, new EmoteMessage { MatchId = matchId, Emote = "🔥" })).Ok);
            Assert.Equal(ErrorCodes.RateLimited, (await active.Live.RequestAsync(MessageTypes.MatchEmote, new EmoteMessage { MatchId = matchId, Emote = "🔥" })).Code);
            Assert.True((await active.Live.RequestAsync(MessageTypes.MatchGift, new GiftMessage { MatchId = matchId, Gift = "tea", TargetSeat = other.Start.YourSeat })).Ok);
            await other.Live.WaitAsync<GiftMessage>(MessageTypes.MatchGift);
            Assert.Equal(9000 - 100, (await active.Live.WaitAsync<WalletResponse>(MessageTypes.Wallet)).Chips);
            // Chat reaches every seat: text up to 80 characters or a ready phrase by index, rate limited.
            Assert.True((await active.Live.RequestAsync(MessageTypes.MatchChat, new ChatMessage { MatchId = matchId, Text = " merhaba " })).Ok);
            Assert.Equal("merhaba", (await other.Live.WaitAsync<ChatMessage>(MessageTypes.MatchChat)).Text);
            Assert.Equal(ErrorCodes.RateLimited, (await active.Live.RequestAsync(MessageTypes.MatchChat, new ChatMessage { MatchId = matchId, Quick = 2 })).Code);
            Assert.Equal(ErrorCodes.Validation, (await other.Live.RequestAsync(MessageTypes.MatchChat, new ChatMessage { MatchId = matchId, Quick = 9 })).Code);

            var current = await active.Http.GetFromJsonAsync<MatchRefMessage>("/v1/matches/current");
            Assert.Equal(matchId, current!.MatchId);
        }
        finally { await LeaveAllAsync(table); }
    }

    [Fact]
    public async Task Idle_table_is_played_out_by_the_server_and_settled()
    {
        var table = await SeatAsync(GameModes.Solo, 5000);
        try
        {
            await PumpUntilAsync(() => table.All(p => p.Live.Received(MessageTypes.MatchEnd)));
            foreach (var p in table) p.Tokens = await server.AdvanceAsync(TimeSpan.Zero, p.Http, p.Tokens);

            var end = await table[0].Live.WaitAsync<MatchEndMessage>(MessageTypes.MatchEnd);
            Assert.False(end.Aborted);
            Assert.Equal(1, end.Placements.Min(x => x.Place));
            // Two hands (60 XP at least each) and the place bonus 500 / 200 / 80 / 0.
            Assert.All(end.Placements, x => Assert.True(x.Xp >= 2 * 60 + new[] { 500, 200, 80, 0 }[x.Place - 1]));
            Assert.Equal(2, table[0].Live.Count(MessageTypes.MatchHandEnd));

            var wallets = await Task.WhenAll(table.Select(p => p.Http.GetFromJsonAsync<WalletResponse>("/v1/wallet")));
            Assert.Equal(4 * 10_000, wallets.Sum(w => w!.Chips)); // stakes are only moved between players
            foreach (var p in table)
            {
                var stats = (await p.Http.GetFromJsonAsync<StatsResponse>("/v1/profile/stats"))!;
                Assert.Equal(2, stats.Hands);
                var history = (await p.Http.GetFromJsonAsync<List<HistoryEntryResponse>>("/v1/profile/history"))!;
                var entry = Assert.Single(history);
                Assert.Equal(GameModes.Solo, entry.Mode);
                Assert.Equal(5000, entry.Stake);
                Assert.True((await p.Http.GetFromJsonAsync<LeaderboardResponse>("/v1/leaderboard/weekly"))!.Me.Points > 0);
                Assert.True((await p.Http.GetFromJsonAsync<ProfileResponse>("/v1/profile"))!.Progress.Xp >= 2 * 60);
                Assert.Equal(HttpStatusCode.NoContent, (await p.Http.GetAsync("/v1/matches/current")).StatusCode);
                var daily = (await p.Http.GetFromJsonAsync<DailyResponse>("/v1/daily"))!;
                Assert.Equal(2, daily.Quests.Single(q => q.Id == "handsPlayed").Progress);
            }
        }
        finally { await LeaveAllAsync(table); }
    }

    [Fact]
    public async Task A_restarted_server_replays_the_stored_moves_to_the_same_table()
    {
        var table = await SeatAsync(GameModes.Pairs, 1000);
        try
        {
            var manager = server.Services.GetRequiredService<MatchManager>();
            var session = manager.Current(table[0].Tokens.AccountId)!;
            var state = await table[0].Live.WaitAsync<MatchStateMessage>(MessageTypes.MatchState);
            // Let the server play a few turns so there are stored moves to replay.
            await PumpUntilAsync(() => session.ProbeAsync(0).Result.Version >= 6);
            var live = await session.ProbeAsync(0);

            var store = server.Services.GetRequiredService<MatchStore>();
            var stored = (await store.LoadActiveAsync(default)).Single(m => m.Record.Id == session.Id);
            var restored = new MatchSession(stored.Record, store,
                new ConnectionRegistry(server.Services.GetRequiredService<IServiceScopeFactory>(), server.Clock, NullLogger<ConnectionRegistry>.Instance),
                server.Services.GetRequiredService<Catalog>(), server.Services.GetRequiredService<IOptions<GameOptions>>().Value,
                server.Clock, NullLogger.Instance, _ => { });
            await restored.RestoreAsync(stored);
            var replayed = await restored.ProbeAsync(0);
            await restored.StopAsync();

            Assert.Equal(live.Hand, replayed.Hand);
            Assert.Equal(live.Version, replayed.Version);
            Assert.Equal(live.Tiles, replayed.Tiles);
            Assert.Equal(state.MatchId, session.Id);
        }
        finally { await LeaveAllAsync(table); }
    }

    [Fact]
    public async Task A_blocked_seats_chat_skips_its_blocker_and_everyone_reads_it_masked()
    {
        var table = await SeatAsync(GameModes.Solo, 1000);
        try
        {
            await Task.WhenAll(table.Select(p => p.Live.WaitAsync<MatchStateMessage>(MessageTypes.MatchState)));
            var matchId = table[0].Start.MatchId;
            Assert.Equal(HttpStatusCode.NoContent, (await table[0].Http.PostAsync($"/v1/players/{table[1].Tokens.AccountId}/block", null)).StatusCode);
            Assert.True((await table[1].Live.RequestAsync(MessageTypes.MatchChat, new ChatMessage { MatchId = matchId, Text = "selam siktir" })).Ok);
            Assert.Equal("selam ******", (await table[2].Live.WaitAsync<ChatMessage>(MessageTypes.MatchChat)).Text);
            Assert.Equal("selam ******", (await table[1].Live.WaitAsync<ChatMessage>(MessageTypes.MatchChat)).Text);
            await table[3].Live.WaitAsync<ChatMessage>(MessageTypes.MatchChat);
            Assert.Equal(0, table[0].Live.Count(MessageTypes.MatchChat));
        }
        finally { await LeaveAllAsync(table); }
    }

    [Fact]
    public async Task Private_room_needs_the_password_and_starts_when_full()
    {
        var players = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => ConnectAsync()));
        try
        {
            var host = players[0];
            Assert.True((await host.Live.RequestAsync(MessageTypes.RoomCreate, new RoomCreateMessage { Stake = 0, Password = "1907" })).Ok);
            var room = await host.Live.WaitAsync<RoomStateMessage>(MessageTypes.RoomState);
            Assert.True(room.HasPassword);

            await SocialTests.MakeFriends(host.Http, players[1].Http);
            Assert.Equal(HttpStatusCode.NoContent, (await host.Http.PostAsJsonAsync($"/v1/friends/{players[1].Tokens.AccountId}/invite", new InviteRequest { RoomCode = room.Code })).StatusCode);
            Assert.Equal(room.Code, (await players[1].Live.WaitAsync<InviteMessage>(MessageTypes.Invite)).RoomCode);
            // Inviting a stranger is not possible.
            Assert.Equal(HttpStatusCode.NotFound, (await host.Http.PostAsJsonAsync($"/v1/friends/{players[2].Tokens.AccountId}/invite", new InviteRequest { RoomCode = room.Code })).StatusCode);

            Assert.Equal(ErrorCodes.Unauthorized, (await players[1].Live.RequestAsync(MessageTypes.RoomJoin, new RoomJoinMessage { Code = room.Code, Password = "0000" })).Code);
            foreach (var p in players.Skip(1))
                Assert.True((await p.Live.RequestAsync(MessageTypes.RoomJoin, new RoomJoinMessage { Code = room.Code.ToLowerInvariant(), Password = "1907" })).Ok);
            var starts = await Task.WhenAll(players.Select(p => p.Live.WaitAsync<MatchStartMessage>(MessageTypes.MatchStart)));
            Assert.All(starts, s => Assert.Equal(GameModes.Private, s.Mode));
            for (var i = 0; i < 4; i++) players[i].Start = starts[i];
            // Busy players cannot queue elsewhere.
            Assert.Equal(ErrorCodes.Conflict, (await host.Live.RequestAsync(MessageTypes.QueueJoin, new QueueJoinMessage { Mode = GameModes.Solo, Stake = 1000 })).Code);
        }
        finally { await LeaveAllAsync(players); }
    }

    [Fact]
    public async Task Queue_refuses_unknown_stakes_and_players_who_cannot_pay()
    {
        var p = await ConnectAsync();
        try
        {
            Assert.Equal(ErrorCodes.Validation, (await p.Live.RequestAsync(MessageTypes.QueueJoin, new QueueJoinMessage { Mode = GameModes.Solo, Stake = 7 })).Code);
            Assert.Equal(ErrorCodes.Validation, (await p.Live.RequestAsync(MessageTypes.QueueJoin, new QueueJoinMessage { Mode = GameModes.Private, Stake = 0 })).Code);
            Assert.Equal(ErrorCodes.InsufficientFunds, (await p.Live.RequestAsync(MessageTypes.QueueJoin, new QueueJoinMessage { Mode = GameModes.Solo, Stake = 25_000 })).Code);
            Assert.True((await p.Live.RequestAsync(MessageTypes.QueueJoin, new QueueJoinMessage { Mode = GameModes.Doubling, Stake = 10_000 })).Ok);
            Assert.True((await p.Live.WaitAsync<QueueStatusMessage>(MessageTypes.QueueStatus)).Queued);
            Assert.True((await p.Live.RequestAsync(MessageTypes.QueueLeave, new { })).Ok);
            Assert.False((await p.Live.WaitAsync<QueueStatusMessage>(MessageTypes.QueueStatus, s => !s.Queued)).Queued);
        }
        finally { await p.Live.DisposeAsync(); }
    }

    // ---- helpers -------------------------------------------------------------------------------------------------

    private async Task<Player> ConnectAsync()
    {
        var (http, tokens) = await server.GuestAsync();
        return new Player(http, tokens, await RealtimeClient.ConnectAsync(server, http));
    }

    private async Task<Player[]> SeatAsync(string mode, long stake)
    {
        var players = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => ConnectAsync()));
        foreach (var p in players)
            Assert.True((await p.Live.RequestAsync(MessageTypes.QueueJoin, new QueueJoinMessage { Mode = mode, Stake = stake })).Ok);
        foreach (var p in players) p.Start = await p.Live.WaitAsync<MatchStartMessage>(MessageTypes.MatchStart);
        Assert.Single(players.Select(p => p.Start.MatchId).Distinct());
        return players;
    }

    private static MatchCommandMessage Commit(Guid matchId, long version, int tile, bool penalty) => new()
    {
        MatchId = matchId, ExpectedVersion = version, Kind = "commit", Plan = new TurnPlanDto { Discard = tile, AcceptPenalty = penalty },
    };

    /// <summary>Advances the shared fake clock in small steps so turn timers fire and the server plays idle seats.</summary>
    private async Task PumpUntilAsync(Func<bool> done, int maxSteps = 20_000)
    {
        for (var i = 0; i < maxSteps && !done(); i++)
        {
            server.Clock.Advance(TimeSpan.FromSeconds(1.1));
            await Task.Delay(2);
        }
        Assert.True(done(), "The table did not reach the expected state.");
    }

    private static async Task LeaveAllAsync(IEnumerable<Player> table)
    {
        foreach (var p in table)
        {
            if (p.Start is not null) await p.Live.SendAsync(MessageTypes.MatchLeave, new MatchRefMessage { MatchId = p.Start.MatchId });
            await p.Live.SendAsync(MessageTypes.RoomLeave);
            await p.Live.SendAsync(MessageTypes.QueueLeave);
        }
        await Task.Delay(100);
        foreach (var p in table) await p.Live.DisposeAsync();
    }
}
