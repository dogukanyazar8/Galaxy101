using System.Net.Http.Json;
using Galaxy101.Contracts;
using Galaxy101.Server.Data;
using Galaxy101.Server.Games;
using Galaxy101.Server.Infrastructure;
using Galaxy101.Server.Profile;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Galaxy101.Server.Tests;

public sealed class RankedRuleTests
{
    [Theory]
    [InlineData(0, "b3")] [InlineData(99, "b3")] [InlineData(100, "b2")] [InlineData(299, "b1")] [InlineData(300, "s3")]
    [InlineData(720, "g2")] [InlineData(900, "d3")] [InlineData(1200, "m3")] [InlineData(1499, "m1")] [InlineData(4000, "m1")]
    public void Every_hundred_points_is_a_division(int lp, string code) => Assert.Equal(code, Ranked.Code(lp));

    [Fact]
    public void Galaksi_is_past_Usta_I_and_in_the_seasons_top_hundred()
    {
        Assert.Equal("gx", Ranked.Code(1500, 1));
        Assert.Equal("gx", Ranked.Code(1720, 100));
        Assert.Equal("m1", Ranked.Code(1720, 101));
        Assert.Equal("m1", Ranked.Code(1499, 1));
        Assert.Equal(220, Ranked.IntoDivision(1720));
        Assert.Equal(72, Ranked.IntoDivision(772));
        Assert.Equal("g1", Ranked.NextCode(772));
        Assert.Null(Ranked.NextCode(1450));
    }

    [Fact]
    public void Upper_leagues_win_less_and_lose_more()
    {
        Assert.Equal([30, 10, -10, -20], Ranked.Table(899));
        Assert.Equal([25, 5, -12, -22], Ranked.Table(900));
        // Settlement uses each seat's own table: an Altın and an Elmas player taking the same places.
        var o = Settlement.Settle(GameModes.Ranked, 0, [-101, 20, 30, 40], 0, new GameOptions(), seasonLp: [880, 950, 100, 1300]);
        Assert.Equal([30, 5, -10, -22], o.Select(x => x.LpDelta!.Value));
    }

    [Fact]
    public void A_new_league_is_protected_for_three_matches_and_lp_never_goes_below_zero()
    {
        // Gümüş I (590) + 30 → Altın III (620): protected.
        var (lp, shield) = Ranked.Apply(590, 0, 30);
        Assert.Equal((620, 3), (lp, shield));
        (lp, shield) = Ranked.Apply(lp, shield, -20); // 600: still Altın
        (lp, shield) = Ranked.Apply(lp, shield, -20); // would be 580: held at 600
        Assert.Equal((600, 1), (lp, shield));
        (lp, shield) = Ranked.Apply(lp, shield, -20); // the last protected match
        Assert.Equal((600, 0), (lp, shield));
        (lp, shield) = Ranked.Apply(lp, shield, -20); // unprotected: down to Gümüş I
        Assert.Equal((580, 0), (lp, shield));
        Assert.Equal((0, 0), Ranked.Apply(10, 0, -20));
    }

    [Theory]
    [InlineData(1050, 600)] [InlineData(1499, 1000)] [InlineData(3000, 1100)] [InlineData(450, 0)] [InlineData(399, 0)]
    public void A_season_starts_four_divisions_lower_from_the_division_start(int lp, int reset) => Assert.Equal(reset, Ranked.SeasonReset(lp));
}

[Collection(ServerCollection.Name)]
public sealed class RankedTests(ServerFixture server)
{
    private sealed record Player(HttpClient Http, TokenResponse Tokens, RealtimeClient Live);

    [Fact]
    public async Task Dereceli_waits_for_close_leagues_widens_after_thirty_seconds_and_scores_league_points()
    {
        var players = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => ConnectAsync()));
        try
        {
            // Three Bronz players and one Altın (two leagues up): no table until the window widens.
            await SetLpAsync(players[3].Tokens.AccountId, 650);
            foreach (var p in players)
                Assert.True((await p.Live.RequestAsync(MessageTypes.QueueJoin, new QueueJoinMessage { Mode = GameModes.Ranked, Stake = 0 })).Ok);
            await Task.Delay(200);
            Assert.False(players[0].Live.Received(MessageTypes.MatchStart));
            for (var i = 0; i < 8 && !players[0].Live.Received(MessageTypes.MatchStart); i++) { server.Clock.Advance(TimeSpan.FromSeconds(5)); await Task.Delay(100); }
            var starts = await Task.WhenAll(players.Select(p => p.Live.WaitAsync<MatchStartMessage>(MessageTypes.MatchStart)));
            Assert.All(starts, s => Assert.Equal(GameModes.Ranked, s.Mode));

            for (var i = 0; i < 20_000 && !players.All(p => p.Live.Received(MessageTypes.MatchEnd)); i++) { server.Clock.Advance(TimeSpan.FromSeconds(1.1)); await Task.Delay(2); }
            var end = await players[0].Live.WaitAsync<MatchEndMessage>(MessageTypes.MatchEnd);
            foreach (var p in players)
            {
                var mine = end.Placements.Single(x => x.Seat == starts[Array.IndexOf(players, p)].YourSeat);
                var before = p == players[3] ? 650 : 0;
                var table = Ranked.Table(before);
                Assert.Equal(table[mine.Place - 1], mine.LpDelta);
                var progress = (await p.Http.GetFromJsonAsync<ProfileResponse>("/v1/profile"))!.Progress;
                Assert.Equal(Math.Max(0, before + mine.LpDelta!.Value), progress.Lp);
                Assert.Equal(Ranked.Code(progress.Lp), progress.League);
                Assert.Equal(Ranked.IntoDivision(progress.Lp), progress.DivisionLp);
            }
        }
        finally
        {
            foreach (var p in players) { await p.Live.SendAsync(MessageTypes.QueueLeave); await p.Live.DisposeAsync(); }
        }
    }

    [Fact]
    public async Task A_new_season_rewards_the_best_league_once_and_starts_four_divisions_lower()
    {
        var (client, tokens) = await server.GuestAsync();
        var calendar = server.Services.GetRequiredService<GameCalendar>();
        var season = calendar.Season;
        await SetLpAsync(tokens.AccountId, 1050, season, best: 1120); // Elmas II, best Elmas I
        var chips = (await client.GetFromJsonAsync<WalletResponse>("/v1/wallet"))!.Chips;
        var progress = (await client.GetFromJsonAsync<ProfileResponse>("/v1/profile"))!.Progress;
        Assert.Equal(("d2", "d1", season), (progress.League, progress.BestLeague, progress.Season));

        // Into the next season.
        server.Clock.Advance(calendar.SeasonEnd - calendar.Now + TimeSpan.FromHours(1));
        tokens = await server.AdvanceAsync(TimeSpan.Zero, client, tokens);
        progress = (await client.GetFromJsonAsync<ProfileResponse>("/v1/profile"))!.Progress;
        Assert.Equal(season + 1, progress.Season);
        Assert.Equal(600, progress.Lp);
        Assert.Equal("g3", progress.League);
        Assert.Equal(chips + Ranked.RewardChips[3], (await client.GetFromJsonAsync<WalletResponse>("/v1/wallet"))!.Chips); // Elmas
        // Looking again pays nothing more.
        await client.GetFromJsonAsync<ProfileResponse>("/v1/profile");
        Assert.Equal(chips + Ranked.RewardChips[3], (await client.GetFromJsonAsync<WalletResponse>("/v1/wallet"))!.Chips);
        using var scope = server.Services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<GameDb>().SeasonResults.SingleAsync(r => r.AccountId == tokens.AccountId);
        Assert.Equal((season, 1120, "d1"), (result.Season, result.BestLp, result.League));
    }

    private async Task SetLpAsync(Guid account, int lp, int? season = null, int? best = null)
    {
        using var scope = server.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        var current = season ?? scope.ServiceProvider.GetRequiredService<GameCalendar>().Season;
        await db.Stats.Where(x => x.AccountId == account).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.Lp, lp).SetProperty(x => x.LpSeason, current).SetProperty(x => x.BestLp, best ?? lp));
    }

    private async Task<Player> ConnectAsync()
    {
        var (http, tokens) = await server.GuestAsync();
        return new Player(http, tokens, await RealtimeClient.ConnectAsync(server, http));
    }
}
