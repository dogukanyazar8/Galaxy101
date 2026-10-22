using Galaxy101.Contracts;
using Galaxy101.Server.Games;
using Galaxy101.Server.Infrastructure;
using Okey101.Core.Model;
using Okey101.Core.Turns;

namespace Galaxy101.Server.Tests;

public sealed class SettlementTests
{
    private static readonly GameOptions Options = new();

    [Fact]
    public void Solo_winner_takes_the_pot_and_chips_are_conserved()
    {
        var o = Settlement.Settle(GameModes.Solo, 1000, [120, -101, 300, 45], 0, Options);
        Assert.Equal([3, 1, 4, 2], o.Select(x => x.Place));
        Assert.Equal([-1000, 3000, -1000, -1000], o.Select(x => x.ChipDelta));
        Assert.Equal(0, o.Sum(x => x.ChipDelta));
        Assert.All(o, x => Assert.Null(x.LpDelta));
        Assert.Equal([5, 30, 1, 15], o.Select(x => x.WeeklyPoints));
    }

    [Fact]
    public void Tied_winners_split_and_share_the_place()
    {
        var o = Settlement.Settle(GameModes.Solo, 1001, [10, 10, 50, 60], 0, Options);
        Assert.Equal([1, 1, 3, 4], o.Select(x => x.Place));
        Assert.Equal(0, o.Sum(x => x.ChipDelta));
        Assert.Equal([2002, 2002, 0, 0], o.Select(x => x.Payout));
        Assert.Equal((30 + 15) / 2, o[0].WeeklyPoints);
    }

    [Fact]
    public void A_leaver_ranks_last_and_gets_nothing_even_with_the_best_total()
    {
        var o = Settlement.Settle(GameModes.Ranked, 0, [-200, 50, 60, 70], 0b0001, Options);
        Assert.Equal(4, o[0].Place);
        Assert.Equal(-16, o[0].LpDelta);
        Assert.Equal(0, o[0].WeeklyPoints);
        Assert.Equal(1, o[1].Place);
        Assert.Equal(24, o[1].LpDelta);
    }

    [Fact]
    public void Pairs_score_by_team_and_the_winning_team_splits_the_pot()
    {
        // Team 0 (seats 0, 2): 100 + 20 = 120. Team 1 (seats 1, 3): 30 + 40 = 70 → team 1 wins.
        var o = Settlement.Settle(GameModes.Pairs, 5000, [100, 30, 20, 40], 0, Options);
        Assert.Equal([2, 1, 2, 1], o.Select(x => x.Place));
        Assert.Equal([-5000, 5000, -5000, 5000], o.Select(x => x.ChipDelta));
    }

    [Fact]
    public void Pairs_team_with_a_leaver_forfeits()
    {
        var o = Settlement.Settle(GameModes.Pairs, 1000, [-500, 30, -500, 40], 0b0100, Options);
        Assert.Equal([2, 1, 2, 1], o.Select(x => x.Place));
        Assert.Equal(-1000, o[0].ChipDelta);
    }

    [Fact]
    public void Private_tables_do_not_feed_the_weekly_board_and_rake_is_kept()
    {
        var o = Settlement.Settle(GameModes.Private, 1000, [1, 2, 3, 4], 0, new GameOptions { RakePercent = 10 });
        Assert.All(o, x => Assert.Equal(0, x.WeeklyPoints));
        Assert.Equal(3600, o[0].Payout);
        Assert.Equal(-400, o.Sum(x => x.ChipDelta));
    }
}

public sealed class WireTests
{
    [Fact]
    public void Bot_plans_survive_the_wire_round_trip()
    {
        // Play a few real turns with the bot and check every command converts to the wire and back unchanged.
        var state = RoundFactory.Create(12345, Guid.NewGuid(), 0);
        var planner = new Okey101.Bots.BotPlanner();
        for (var turn = 0; turn < 12 && state.Phase is RoundPhase.AwaitingDraw or RoundPhase.PlanningTurn; turn++)
        {
            var seat = state.ActivePlayer;
            GameCommand command = state.Phase == RoundPhase.AwaitingDraw
                ? new DrawCommand(Meta(state, seat), planner.ChooseDraw(PlayerViewProjector.ForPlayer(state, seat), default).Source)
                : new CommitTurnCommand(Meta(state, seat), planner.PlanTurn(PlayerViewProjector.ForPlayer(state, seat), default));
            var json = System.Text.Json.JsonSerializer.Serialize(Wire.ToMessage(command));
            var back = Wire.ToCommand(System.Text.Json.JsonSerializer.Deserialize<MatchCommandMessage>(json)!, command.Meta);
            Assert.Equal(json, System.Text.Json.JsonSerializer.Serialize(Wire.ToMessage(back)));
            var applied = GameEngine.Apply(state, back, default);
            Assert.True(applied.Success, applied.Error.ToString());
            state = applied.Value;
        }
    }

    [Fact]
    public void Malformed_moves_are_rejected_not_truncated()
    {
        var meta = new CommandMeta(Guid.NewGuid(), 0, 1, 0);
        Assert.ThrowsAny<ArgumentException>(() => Wire.ToCommand(new MatchCommandMessage { Kind = "draw", Source = "deck" }, meta));
        Assert.ThrowsAny<ArgumentException>(() => Wire.ToCommand(new MatchCommandMessage { Kind = "commit" }, meta));
        Assert.ThrowsAny<ArgumentException>(() => Wire.ToCommand(new MatchCommandMessage { Kind = "commit", Plan = new TurnPlanDto { Discard = 999 } }, meta));
        var huge = new TurnPlanDto { Actions = [.. Enumerable.Repeat(new TableActionDto { Type = "swap", JokerId = 1, ReplacementId = 2 }, 33)] };
        Assert.ThrowsAny<ArgumentException>(() => Wire.ToCommand(new MatchCommandMessage { Kind = "commit", Plan = huge }, meta));
    }

    private static CommandMeta Meta(RoundState s, int seat) => new(s.RoundId, seat, s.LastAcceptedSequences[seat] + 1, s.Version);
}
