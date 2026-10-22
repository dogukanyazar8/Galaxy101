using Galaxy101.Contracts;
using Galaxy101.Server.Economy;
using Galaxy101.Server.Infrastructure;
using Okey101.Core.Model;

namespace Galaxy101.Server.Games;

public sealed record SeatOutcome(int Seat, int Place, long Payout, long ChipDelta, int? LpDelta, int WeeklyPoints);

/// <summary>
/// End-of-match arithmetic, pure and deterministic. Lower 101 totals rank higher. In pairs (seats 0+2 vs 1+3) a seat's
/// key is its team's total. A seat that left ranks below everyone who stayed and receives nothing.
/// Tied seats share the payout, LP and weekly points of the positions they occupy.
/// </summary>
public static class Settlement
{
    public static SeatOutcome[] Settle(string mode, long stake, IReadOnlyList<int> totals, int leftMask, GameOptions options)
    {
        var pairs = mode == GameModes.Pairs;
        bool Left(int s) => (leftMask & (1 << s)) != 0;
        // Pairs: a team with a leaver forfeits as a team.
        bool Forfeit(int s) => pairs ? Left(s) || Left((s + 2) % 4) : Left(s);
        (int, long) Key(int s) => (Forfeit(s) ? 1 : 0, pairs ? (long)totals[s] + totals[(s + 2) % 4] : totals[s]);

        var order = Enumerable.Range(0, 4).OrderBy(Key).ThenBy(s => s).ToArray();
        var groups = order.GroupBy(Key).Select(g => g.ToArray()).ToList();
        var payoutTable = pairs ? [0.5, 0.5, 0, 0] : options.SoloPayout;
        var pot = stake * 4;
        var distributable = pot - pot * options.RakePercent / 100;

        var place = new int[4];
        var payout = new long[4];
        var lp = new int[4];
        var weekly = new int[4];
        var position = 0;
        for (var g = 0; g < groups.Count; g++)
        {
            var seats = groups[g];
            var span = Enumerable.Range(position, seats.Length).ToArray();
            foreach (var s in seats)
            {
                place[s] = 1 + (pairs ? g : position);
                if (Left(s)) { lp[s] = options.RankedLp[3]; continue; }
                payout[s] = (long)Math.Floor(distributable * span.Average(p => payoutTable[p]));
                lp[s] = (int)Math.Round(span.Average(p => (double)options.RankedLp[p]), MidpointRounding.ToZero);
                weekly[s] = (int)Math.Round(span.Average(p => (double)options.WeeklyPoints[p]), MidpointRounding.ToZero);
            }
            position += seats.Length;
        }
        // Rounding remainders go to the best-placed seat that stayed, so no chips vanish when nobody forfeited.
        var paid = payout.Sum();
        var winners = groups[0].Where(s => !Left(s)).ToArray();
        if (stake > 0 && leftMask == 0 && winners.Length > 0) payout[winners[0]] += distributable - paid;

        var ranked = mode == GameModes.Ranked;
        var rated = mode != GameModes.Private;
        return [.. Enumerable.Range(0, 4).Select(s => new SeatOutcome(s, place[s], payout[s], payout[s] - stake,
            ranked ? lp[s] : null, rated ? weekly[s] : 0))];
    }

    /// <summary>What a finished hand contributes to each seat's quests and stats.</summary>
    public static QuestCounters[] HandCounters(RoundState final, IEnumerable<GameCommand> commands, int leftMask)
    {
        var finish = final.Result?.FinishInfo;
        var tilesAdded = new int[4];
        foreach (var c in commands.OfType<CommitTurnCommand>())
            tilesAdded[c.Meta.Actor] += c.Plan.Actions.OfType<ExtendMeld>().Sum(e => e.Tiles.Count);
        return [.. Enumerable.Range(0, 4).Select(s => (leftMask & (1 << s)) != 0
            ? new QuestCounters(0, 0, 0, 0, 0)
            : new QuestCounters(
                finish?.Winner == s ? 1 : 0,
                final.Players[s].Opening == OpeningKind.Pairs ? 1 : 0,
                1,
                finish?.Winner == s && finish.DiscardedRealJoker ? 1 : 0,
                tilesAdded[s]))];
    }

    /// <summary>A hand's score per seat (lower is better); a cancelled hand scores zero for everyone.</summary>
    public static int[] HandScores(RoundState final) =>
        [.. Enumerable.Range(0, 4).Select(s => final.Result?.Scores[s].Total ?? 0)];
}
