using System.Text.Json;
using Galaxy101.Contracts;
using Okey101.Core.V4;
using M = Galaxy101.Contracts.V4Messages;

namespace Galaxy101.Server.Tests.V4;

/// <summary>
/// The package's server flows (tests/server.tests.js) against the real server, virtual time (docs/v4/KARARLAR.md K-05):
/// matches against bots, the deal ceremony, hidden information, reconnecting and determinism.
/// </summary>
[Collection(ServerCollection.Name)]
public sealed class GameFlowTests(ServerFixture server)
{
    /// <summary>A seed whose first dealer is not seat 0 (the person): the ceremony runs with a bot dealer.</summary>
    internal static uint BotDealerSeed(uint from)
    {
        for (var seed = from; ; seed++)
            if (G101Rules.Apply(G101Rules.NewMatch(new MatchOptions(), seed), GameAction.StartHand()).State.Hand.Deal.Dealer != 0) return seed;
    }

    [Fact]
    public async Task SRV_SC1_Scenario_R43_the_bot_opens_with_your_K9_and_you_get_90()
    {
        await using var env = new V4Env(server);
        var racks = new List<List<string>>
        {
            new() { "K9b", "R1a", "R3a", "R5b", "R7b", "R9a", "R11b", "B2a", "B5a", "B7b", "B11a", "B13b", "K4a", "K7b", "Y1a", "Y3b", "Y6a", "Y10b", "Y12b", "R10b", "B4b", "K2b" },
            new() { "R13a", "B13a", "Y13a", "R12a", "B12a", "Y12a", "K10a", "K11a", "R2a", "R4a", "R6a", "R8a", "B1a", "B3a", "B6a", "B9a", "K1a", "K3a", "K5a", "Y8a", "Y11a" },
        };
        env.Hub.SeedSource = () => 7;
        env.Hub.NextRig = G101Rules.RigFromHands(new ScenarioDeal { Dealer = 3, Dice = [4, 3], Indicator = "Y4a", Racks = racks });
        var c = await env.GuestAsync();
        await c.SendAsync(M.Hello);
        Assert.True((await c.SendAsync(M.VsBotsStart, new { mode = "solo", hands = 1 })).Ok);
        Assert.True(await env.UntilAsync(() => c.Of(M.Turn).Count > 0));
        var dealt = c.Of(M.HandDealt)[0]["rack"].EnumerateArray().Select(x => x.GetString()!).Order().ToList();
        Assert.Equal(racks[0].Order().ToList(), dealt);
        Assert.Equal(0, c.Of(M.Turn)[0].Int("seat"));
        Assert.True((await c.SendAsync(M.GDiscard, new { tile = "K9b" })).Ok);
        Assert.True(await env.UntilAsync(() => c.Game("penalty").Count > 0, 200));
        var penalty = c.Game("penalty")[0].Ev;
        Assert.Equal("P_OPENED_WITH", penalty.GetProperty("code").GetString());
        Assert.Equal(90, penalty.GetProperty("amount").GetInt32());
        Assert.Equal(0, penalty.GetProperty("seat").GetInt32());
        await c.SendAsync(M.TableLeave);
    }

    [Fact]
    public async Task SRV_01_Against_bots_a_whole_match_without_errors()
    {
        await using var env = new V4Env(server);
        env.Hub.SeedSource = () => 42;
        var c = await env.GuestAsync();
        await c.SendAsync(M.Hello);
        await c.SendAsync(M.VsBotsStart, new { mode = "solo", hands = 2 });
        Assert.True(await env.UntilAsync(() => c.Of(M.MatchResult).Count > 0, 4000), "matchResult gelmedi: " + string.Join(" ", c.Inbox.Take(40).Select(f => f.Type == "ack" ? "ack:" + f.Code : f.Type)));
        Assert.DoesNotContain(c.Of(MessageTypes.Ack), a => !a.Ok);
        Assert.Equal(2, c.Of(M.HandDealt).Count);
        Assert.Single(c.Of(M.HandWait));
    }

    [Fact]
    public async Task SRV_02_Deal_ceremony_with_a_bot_dealer_takes_20358_ms()
    {
        await using var env = new V4Env(server);
        var seed = BotDealerSeed(42);
        env.Hub.SeedSource = () => seed;
        var c = await env.GuestAsync();
        await c.SendAsync(M.Hello);
        await c.SendAsync(M.VsBotsStart, new { mode = "solo", hands = 1 });
        Assert.True(await env.UntilAsync(() => c.Of(M.Turn).Count > 0));
        var deal = c.Last(M.DealStart)!;
        Assert.False(deal["humanDealer"].GetBoolean());
        Assert.Equal(20358, c.Of(M.Turn)[0].At - deal.At);
        Assert.Equal(["dice", "indicator", "extra", "distribute", "center"], c.Of(M.DealStep).Select(s => s.Str("step")));
        // Current prototip/galaxy101_handoff_v4/logic/mockServer.js, from dealStart; older timing docs differ.
        long T(string type, Func<Frame, bool>? f = null) => c.Of(type).First(x => f is null || f(x)).At - deal.At;
        Assert.Equal(2572, T(M.DealStep, s => s.Str("step") == "dice"));
        Assert.Equal(3272, T(M.DealDie, d => d.Int("index") == 0));
        Assert.Equal(5492, T(M.DealDie, d => d.Int("index") == 1));
        Assert.Equal(8482, T(M.DealStep, s => s.Str("step") == "indicator"));
        Assert.Equal(10782, T(M.DealStep, s => s.Str("step") == "extra"));
        Assert.Equal(12082, T(M.DealStep, s => s.Str("step") == "distribute"));
        Assert.Equal(16222, T(M.DealStep, s => s.Str("step") == "center"));
        Assert.Equal(18222, T(M.HandDealt));
        await c.SendAsync(M.TableLeave);
    }

    [Fact]
    public async Task SRV_02c_Skip_deal_is_authorized_by_the_server_and_keeps_the_fast_fill_gate()
    {
        await using var env = new V4Env(server);
        env.Hub.SeedSource = () => 7;
        env.Hub.NextRig = G101Rules.RigFromHands(new ScenarioDeal { Dealer = 3, Dice = [4, 3], Indicator = "Y4a" });
        var client = await env.GuestAsync(); await client.SendAsync(M.Hello);
        Assert.Equal(V4Errors.E_NOT_SEATED, (await client.SendAsync(M.GSkipDeal)).Code);
        Assert.True((await client.SendAsync(M.VsBotsStart, new { mode = "solo", hands = 1 })).Ok);
        Assert.True(await env.UntilAsync(() => client.Of(M.DealStart).Count > 0));
        Assert.True((await client.SendAsync(M.GSkipDeal)).Ok);
        await env.SettleAsync();
        var skip = Assert.Single(client.Of(M.DealSkip));
        Assert.Equal(new[] { 4, 3 }, skip["dice"].EnumerateArray().Select(value => value.GetInt32()));
        Assert.Equal("Y4a", skip.Str("indicator")); Assert.False(skip.Has("rack"));
        Assert.Single(client.Of(M.HandDealt)); Assert.Empty(client.Of(M.Turn));
        await env.AdvanceAsync(1099); Assert.Empty(client.Of(M.Turn));
        await env.AdvanceAsync(1);
        Assert.Equal(1100, Assert.Single(client.Of(M.Turn)).At - skip.At);
        Assert.Equal(V4Errors.E_PHASE, (await client.SendAsync(M.GSkipDeal)).Code);
        await client.SendAsync(M.TableLeave);
    }

    [Fact]
    public async Task SRV_02b_A_human_dealer_rolls_with_gRollDice_or_the_server_rolls_after_15_s()
    {
        await using var env = new V4Env(server);
        env.Hub.SeedSource = () => 42; // seed 42 deals from seat 0 (R-02 example)
        var c = await env.GuestAsync();
        await c.SendAsync(M.Hello);
        await c.SendAsync(M.VsBotsStart, new { mode = "solo", hands = 1 });
        Assert.True(await env.UntilAsync(() => c.Of(M.DealStep).Count > 0));
        var deal = c.Last(M.DealStart)!;
        Assert.True(deal["humanDealer"].GetBoolean());
        Assert.Equal(15000, c.Of(M.DealStep)[0]["rollWaitMs"].GetInt64());
        Assert.Equal(V4Errors.E_DEALING, (await c.SendAsync(M.GDraw, new { src = "deck" })).Code);
        Assert.True((await c.SendAsync(M.GRollDice)).Ok);                       // first die at once
        Assert.Equal(V4Errors.E_PHASE, (await c.SendAsync(M.GRollDice)).Code);  // still rolling
        Assert.Single(c.Of(M.DealDie));
        Assert.True(await env.UntilAsync(() => c.Of(M.DealDie).Count == 2, 100)); // second die: the server after 15 s
        Assert.Equal(15000, c.Of(M.DealDie)[1].At - c.Of(M.DealDie)[0].At - TableRoll1);
        await c.SendAsync(M.TableLeave);
    }

    private const long TableRoll1 = Galaxy101.Table.TableTimings.Roll1;

    [Fact]
    public async Task SRV_03_Hidden_information_never_leaves_early()
    {
        await using var env = new V4Env(server);
        env.Hub.SeedSource = () => 7;
        var c = await env.GuestAsync();
        await c.SendAsync(M.Hello);
        await c.SendAsync(M.VsBotsStart, new { mode = "solo", hands = 1 });
        Assert.True(await env.UntilAsync(() => c.Of(M.MatchResult).Count > 0, 4000));
        Assert.DoesNotContain(c.Game("drew"), f => f.Ev.GetProperty("src").GetString() == "deck" && f.Ev.GetProperty("seat").GetInt32() != 0 && f.Ev.GetProperty("tile").ValueKind != JsonValueKind.Null);
        Assert.Contains(c.Game("drew"), f => f.Ev.GetProperty("seat").GetInt32() == 0 && f.Ev.GetProperty("tile").ValueKind == JsonValueKind.String);
        foreach (var hs in c.Game("handStart"))
            foreach (var key in new[] { "dice", "indicator", "okey" }) Assert.Equal(JsonValueKind.Null, hs.Ev.GetProperty(key).ValueKind);
        Assert.True(c.Of(M.DealStep).First(s => s.Str("step") == "indicator").At > c.Of(M.DealDie)[0].At);
        foreach (var h in c.Of(M.HandDealt)) { Assert.True(h["rack"].GetArrayLength() >= 21); Assert.False(h.Data.TryGetProperty("racks", out _)); }
        // Every view this seat got: its own rack only, the others as counts.
        foreach (var v in c.Of(M.TableView)) Assert.False(v["view"].TryGetProperty("racks", out _));
    }

    [Fact]
    public async Task SRV_11_Reconnect_returns_to_the_table_with_your_rack_and_no_one_elses()
    {
        await using var env = new V4Env(server);
        env.Hub.SeedSource = () => 42;
        var c = await env.GuestAsync();
        await c.SendAsync(M.Hello);
        await c.SendAsync(M.VsBotsStart, new { mode = "solo", hands = 1 });
        Assert.True(await env.UntilAsync(() => c.Of(M.Turn).Count > 0));
        await env.DropAsync(c);
        await env.AdvanceAsync(4000);
        var d = await env.ReconnectAsync(c);
        Assert.True((await d.SendAsync(M.Hello, new { token = "t" })).Ok);
        var s = d.Last(M.Snapshot)!;
        Assert.Equal("table", s.Str("screen"));
        Assert.True(s["game"].GetProperty("rack").GetArrayLength() >= 21);
        Assert.DoesNotContain("\"racks\"", s["game"].GetRawText());
        var other = await env.GuestAsync();
        await other.SendAsync(M.Hello);
        Assert.Equal("menu", other.Last(M.Snapshot)!.Str("screen"));
        await d.SendAsync(M.TableLeave);
    }

    [Fact]
    public async Task SRV_15_Same_seed_same_event_stream()
    {
        async Task<string> Trace()
        {
            await using var env = new V4Env(server);
            env.Hub.SeedSource = () => 99;
            var c = await env.GuestAsync();
            await c.SendAsync(M.Hello);
            await c.SendAsync(M.VsBotsStart, new { mode = "pairs", hands = 1 });
            Assert.True(await env.UntilAsync(() => c.Of(M.MatchResult).Count > 0, 4000));
            var frames = c.Inbox.Where(f => f.Type != MessageTypes.Ack && f.Type != M.Session && f.Type != M.Snapshot && f.Type != M.TableState && f.Type != M.Balance && f.Type != M.Xp).ToList();
            var t0 = frames.First(f => f.Type == M.MatchStart).At;
            return JsonSerializer.Serialize(frames.Select(f => new object?[]
            {
                f.Type, f.At - t0, f.E, f.Type == M.Game && f.Ev.TryGetProperty("tile", out var tile) ? tile.ToString() : null,
            }));
        }
        Assert.Equal(await Trace(), await Trace());
    }
}
