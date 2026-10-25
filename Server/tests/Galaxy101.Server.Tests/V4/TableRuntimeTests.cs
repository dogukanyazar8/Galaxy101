using System.Text.Json;
using Galaxy101.Contracts;
using Galaxy101.Table;
using Okey101.Core.V4;
using M = Galaxy101.Contracts.V4Messages;

namespace Galaxy101.Server.Tests.V4;

/// <summary>A deterministic host for the transport-free table runtime: a logical clock, a timer queue and every event per seat.</summary>
internal sealed class FakeTableHost : ITableHost
{
    private readonly SortedSet<(long At, long Seq)> queue = [];
    private readonly Dictionary<long, Action> actions = [];
    private long seq;
    public long Now { get; private set; }
    public List<(int Seat, string Type, object Payload, long At)> Sent { get; } = [];
    public bool Ended { get; private set; }

    public object Schedule(long atMs, Action action) { var id = ++seq; queue.Add((Math.Max(atMs, Now), id)); actions[id] = action; return id; }
    public void Cancel(object handle) => actions.Remove((long)handle);
    public void Send(int seat, string type, object payload) => Sent.Add((seat, type, payload, Now));
    public void Applied(TableRuntime table, IReadOnlyList<GameEvent> events) { }
    public void MatchEnded(TableRuntime table) => Ended = true;

    /// <summary>Runs timers until <paramref name="done"/> holds or nothing is left.</summary>
    public void RunUntil(Func<bool> done)
    {
        while (!done() && queue.Count > 0)
        {
            var next = queue.Min;
            queue.Remove(next);
            if (!actions.Remove(next.Seq, out var action)) continue;
            Now = next.At;
            action();
        }
    }

    public IEnumerable<Dictionary<string, object>> Events(int seat, string e) => Sent
        .Where(s => s.Seat == seat && s.Type == M.Game).Select(s => (Dictionary<string, object>)((V4GameEvent)s.Payload).Ev).Where(ev => (string)ev["e"] == e);
}

/// <summary>The table runtime without the server: what protocol.json hiddenInfo promises each seat and spectator.</summary>
public sealed class TableRuntimeTests
{
    private static (TableRuntime Run, FakeTableHost Host) Start(bool[] bots, DealRig? rig = null, uint seed = 7)
    {
        var host = new FakeTableHost();
        var run = new TableRuntime(host, new TableSetup { Seed = seed, Options = new MatchOptions { Mode = "solo", Hands = 1 }, FirstHandRig = rig }, bots, new RngState(1));
        run.Start(new V4Table());
        host.RunUntil(() => !run.Dealing && run.Match.Hand != null);
        return (run, host);
    }

    [Fact]
    public void A_short_opening_shows_its_tiles_only_to_the_player_who_tried()
    {
        var rig = G101Rules.RigFromHands(new ScenarioDeal
        {
            Dealer = 3, Dice = [4, 3], Indicator = "Y4a",
            Racks = [["R1a", "R2a", "R3a", "B7a", "K9a"], ["R1b", "R2b"]],
        });
        var (run, host) = Start([false, false, true, true], rig);
        Assert.Equal(0, run.Match.Hand.Turn.Seat);
        var error = run.Move(0, new GameAction { T = "open", Melds = [new MeldSpec("run", ["R1a", "R2a", "R3a"])] }, out _);
        Assert.Null(error);
        var mine = Assert.Single(host.Events(0, "openShort"));
        Assert.NotNull(mine["tiles"]);
        var theirs = Assert.Single(host.Events(1, "openShort"));
        Assert.Null(theirs["tiles"]);
        Assert.Null(Assert.Single(host.Events(-1, "openShort"))["tiles"]);
        // The penalty itself is public (R-23 +101).
        Assert.Contains(host.Events(1, "penalty"), p => (string)p["code"] == "P_OPEN_SHORT");
    }

    [Fact]
    public void Views_carry_only_the_own_rack_and_the_spectator_none()
    {
        var (run, host) = Start([false, false, true, true]);
        var view0 = run.GameView(0);
        var spectator = run.GameView(-1);
        Assert.Equal(run.Match.Hand.Racks[0].Count, ((List<object>)view0["rack"]).Count);
        Assert.Null(spectator["rack"]);
        Assert.Null(spectator["legal"]);
        var json = JsonSerializer.Serialize(view0);
        Assert.DoesNotContain("\"racks\"", json);
        Assert.DoesNotContain("\"deck\":[", json);
        Assert.DoesNotContain(run.Match.Hand.Racks[1][0] + "\"", JsonSerializer.Serialize(view0["rack"]));
        // handDealt: own rack to each seat, none to spectators, never the others'.
        var dealt = host.Sent.Where(s => s.Type == M.HandDealt).ToList();
        Assert.Equal(run.Match.Hand.Racks[1].Count >= 21, ((V4HandDealt)dealt.Single(s => s.Seat == 1).Payload).Rack.Count >= 21);
        Assert.Null(((V4HandDealt)dealt.Single(s => s.Seat == -1).Payload).Rack);
        Assert.DoesNotContain(dealt, s => s.Seat is 2 or 3);
    }

    [Fact]
    public void Dice_and_indicator_are_told_only_when_rolled_and_revealed()
    {
        var host = new FakeTableHost();
        var run = new TableRuntime(host, new TableSetup { Seed = GameFlowTests.BotDealerSeed(42), Options = new MatchOptions { Hands = 1 } }, [false, true, true, true], new RngState(1));
        run.Start(new V4Table());
        host.RunUntil(() => host.Sent.Any(s => s.Type == M.DealDie));
        var view = run.GameView(0);
        Assert.Null(view["indicator"]);
        Assert.Null(view["okey"]);
        Assert.Empty((List<object>)((Dictionary<string, object>)view["deal"])["dice"]);
        Assert.Null(view["rack"]);
        host.RunUntil(() => host.Sent.Any(s => s.Type == M.DealStep && ((V4DealStep)s.Payload).Step == "indicator"));
        Assert.NotNull(run.GameView(0)["indicator"]);
        Assert.Equal(2, ((List<object>)((Dictionary<string, object>)run.GameView(0)["deal"])["dice"]).Count);
    }

    [Fact]
    public void A_taken_seat_plays_on_from_its_total_and_a_left_seat_is_played_by_the_server()
    {
        var (run, host) = Start([false, true, true, true], seed: 11);
        Assert.Null(run.Leave(0));
        Assert.True(run.IsBotSeat(0));
        Assert.True(run.Match.Seats[0].Left);
        host.RunUntil(() => run.Match.Status != G101Rules.StatusPlaying);
        Assert.Equal(G101Rules.StatusMatchOver, run.Match.Status);
        Assert.True(host.Ended);
        // Leaving a solo match ranks the seat last (R-60).
        Assert.Equal(new[] { 0 }, run.Match.Ranking[^1].Seats);
    }
}
