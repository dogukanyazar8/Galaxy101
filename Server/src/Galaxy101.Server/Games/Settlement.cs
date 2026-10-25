using Galaxy101.Contracts;
using Galaxy101.Server.Economy;
using Galaxy101.Server.Infrastructure;
using Okey101.Core.Model;

namespace Galaxy101.Server.Games;

/// <param name="XpBonus">Match-end place bonus (R-93), shared like the other place values (D-40, D-41); 0 for a leaver.</param>
public sealed record SeatOutcome(int Seat, int Place, long Payout, long ChipDelta, int? LpDelta, int WeeklyPoints, int XpBonus = 0);

/// <summary>
/// End-of-match arithmetic, pure and deterministic. Lower 101 totals rank higher. In pairs (seats 0+2 vs 1+3) a seat's
/// key is its team's total. A seat that left receives nothing and ranks last; in solo it also ranks below everyone who
/// stayed, while in pairs a bot plays it to the end and the team still ranks by its total — if the team wins, the partner
/// who stayed takes the whole team prize (25 Ekim 2026). Tied seats share the payout, LP and weekly points of the positions
/// they occupy.
/// </summary>
public static class Settlement
{
    /// <param name="seasonLp">Ranked: each seat's season LP (its league sets the LP table); null for other modes.</param>
    public static SeatOutcome[] Settle(string mode, long stake, IReadOnlyList<int> totals, int leftMask, GameOptions options, int botMask = 0,
        IReadOnlyList<int>? seasonLp = null)
    {
        var pairs = mode == GameModes.Pairs;
        bool Left(int s) => (leftMask & (1 << s)) != 0;
        bool Bot(int s) => (botMask & (1 << s)) != 0;
        // Solo: a leaver forfeits. Pairs: the leaver's seat plays on, so the team keeps its place.
        bool Forfeit(int s) => !pairs && Left(s);
        (int, long) Key(int s) => (Forfeit(s) ? 1 : 0, pairs ? (long)totals[s] + totals[(s + 2) % 4] : totals[s]);

        var order = Enumerable.Range(0, 4).OrderBy(Key).ThenBy(s => s).ToArray();
        var groups = order.GroupBy(Key).Select(g => g.ToArray()).ToList();
        var payoutTable = pairs ? [0.5, 0.5, 0, 0] : options.SoloPayout;
        // Only the people at the table paid in: a bot's seat adds nothing to the pot.
        var pot = stake * Enumerable.Range(0, 4).Count(s => !Bot(s));
        var distributable = pot - pot * options.RakePercent / 100;

        var place = new int[4];
        var payout = new long[4];
        var lp = new int[4];
        var weekly = new int[4];
        var xpBonus = new int[4];
        var position = 0;
        for (var g = 0; g < groups.Count; g++)
        {
            var seats = groups[g];
            var span = Enumerable.Range(position, seats.Length).ToArray();
            // A bot's or a leaver's share goes to the people placed with it (a pairs partner, a tie), or to nobody when no
            // person placed there.
            var people = seats.Count(s => !Left(s) && !Bot(s));
            foreach (var s in seats)
            {
                place[s] = 1 + (pairs ? g : position);
                var table = Profile.Ranked.Table(seasonLp?[s] ?? 0);
                if (Left(s)) { if (pairs) place[s] = 2; lp[s] = table[3]; continue; }
                if (!Bot(s)) payout[s] = (long)Math.Floor(distributable * span.Average(p => payoutTable[p]) * seats.Length / people);
                // D-40: the average of the positions the tie occupies, rounded to nearest (the package's Math.round); in pairs
                // a team occupies two positions (D-41: +20 / −15 LP, 23 / 3 weekly, 350 / 40 XP).
                lp[s] = Galaxy101.Table.TableSettlement.Shared(table, span);
                weekly[s] = Galaxy101.Table.TableSettlement.Shared(options.WeeklyPoints, span);
                xpBonus[s] = Galaxy101.Table.TableSettlement.Shared(options.XpPlace, span);
            }
            position += seats.Length;
        }
        // Rounding remainders go to the best-placed seat that stayed, so no chips vanish when nobody forfeited.
        var paid = payout.Sum();
        var winners = groups[0].Where(s => !Left(s) && !Bot(s)).ToArray();
        if (stake > 0 && leftMask == 0 && winners.Length > 0) payout[winners[0]] += distributable - paid;

        var ranked = mode == GameModes.Ranked;
        var rated = mode != GameModes.Private;
        return [.. Enumerable.Range(0, 4).Select(s => new SeatOutcome(s, place[s], payout[s], payout[s] - (Bot(s) ? 0 : stake),
            ranked ? lp[s] : null, rated ? weekly[s] : 0, xpBonus[s]))];
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
