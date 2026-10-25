using Galaxy101.Server.Data;
using Galaxy101.Server.Economy;
using Galaxy101.Server.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Galaxy101.Server.Profile;

/// <summary>
/// Dereceli: five leagues of three divisions (Bronz III … Usta I), 100 LP each, and Galaksi above them — the season's
/// best 100 of those past Usta I, shown with their place. LP per match depends on the place and the league (the upper
/// leagues win less and lose more); a promotion to a new league is protected for three matches. Each 8-week season
/// starts four divisions lower and pays a reward for the best league reached. Pure rules here; the store applies them.
/// </summary>
public static class Ranked
{
    public const int DivisionLp = 100, Divisions = 15, ApexLp = Divisions * DivisionLp, ApexPlayers = 100;
    public const int ShieldMatches = 3, SeasonDrop = 4 * DivisionLp;
    public const string Apex = "gx";

    /// <summary>Division codes, lowest first: b3 = Bronz III … m1 = Usta I.</summary>
    public static readonly string[] Codes = ["b3", "b2", "b1", "s3", "s2", "s1", "g3", "g2", "g1", "d3", "d2", "d1", "m3", "m2", "m1"];

    /// <summary>Season reward by the best league reached: Bronz, Gümüş, Altın, Elmas, Usta, Galaksi.</summary>
    public static readonly long[] RewardChips = [2_000, 5_000, 10_000, 25_000, 50_000, 100_000];

    public static int Division(int lp) => Math.Clamp(lp / DivisionLp, 0, Divisions - 1);

    /// <summary>0 Bronz … 4 Usta, 5 Galaksi (needs the season place too).</summary>
    public static int League(int lp, int rank = 0) => IsApex(lp, rank) ? 5 : Division(lp) / 3;

    public static bool IsApex(int lp, int rank) => lp >= ApexLp && rank is > 0 and <= ApexPlayers;

    public static string Code(int lp, int rank = 0) => IsApex(lp, rank) ? Apex : Codes[Division(lp)];

    /// <summary>LP into the current division (0–99); past Usta I, the LP above it.</summary>
    public static int IntoDivision(int lp) => lp >= ApexLp ? lp - ApexLp : lp % DivisionLp;

    /// <summary>The next division's code, or null at the top (Usta I and Galaksi go on by LP and place).</summary>
    public static string? NextCode(int lp) => lp >= ApexLp - DivisionLp ? null : Codes[Division(lp) + 1];

    /// <summary>LP by place 1–4: Bronz–Altın +30 / +10 / −10 / −20; Elmas and above +25 / +5 / −12 / −22.</summary>
    public static int[] Table(int lp) => Division(lp) / 3 >= 3 ? [25, 5, -12, -22] : [30, 10, -10, -20];

    /// <summary>The LP where the league of <paramref name="lp"/> begins (Usta's for anything past it).</summary>
    public static int LeagueFloor(int lp) => Division(lp) / 3 * 3 * DivisionLp;

    /// <summary>
    /// A match's LP: never below 0; while protected, a loss stops at the floor of the league just reached. Reaching a
    /// higher league arms three protected matches; every other ranked match uses one up.
    /// </summary>
    public static (int Lp, int Shield) Apply(int lp, int shield, int delta)
    {
        var next = Math.Max(0, lp + delta);
        if (delta < 0 && shield > 0) next = Math.Max(next, LeagueFloor(lp));
        var promoted = Division(next) / 3 > Division(lp) / 3;
        return (next, promoted ? ShieldMatches : Math.Max(0, shield - 1));
    }

    /// <summary>A new season: four divisions lower, from the start of the division (never below Bronz III).</summary>
    public static int SeasonReset(int lp, int seasons = 1)
    {
        for (var i = 0; i < seasons && lp > 0; i++) lp = Math.Max(0, Math.Min(lp, ApexLp) / DivisionLp * DivisionLp - SeasonDrop);
        return lp;
    }

    /// <summary>The LP a player shows in <paramref name="season"/> even before their row was rolled over to it.</summary>
    public static int SeasonLp(int lp, int lpSeason, int season) => lpSeason >= season ? lp : SeasonReset(lp, season - lpSeason);

    /// <summary>Season place on the season board: 1 + those above (0 = not on it).</summary>
    public static async Task<int> PlaceAsync(GameDb db, string board, Guid account, long points, CancellationToken ct) =>
        points <= 0 ? 0 : 1 + await db.LeaderboardScores.CountAsync(x => x.Board == board && x.Points > points, ct);

    /// <summary>
    /// Brings a player's stats into the current season inside the caller's transaction (the row is locked): the past
    /// season's best is recorded and rewarded once (idempotent ledger key), the LP drops, the protection ends.
    /// </summary>
    public static async Task RollOverAsync(GameDb db, Ledger ledger, PlayerStats stats, int season, CancellationToken ct)
    {
        if (stats.LpSeason >= season) return;
        if (stats.LpSeason > 0 && stats.BestLp > 0)
        {
            var board = Social.LeaderboardEndpoints.SeasonBoard(stats.LpSeason);
            var place = stats.BestLp >= ApexLp ? await PlaceAsync(db, board, stats.AccountId, stats.Lp, ct) : 0;
            var league = League(stats.BestLp, place);
            if (!await db.SeasonResults.AnyAsync(r => r.AccountId == stats.AccountId && r.Season == stats.LpSeason, ct))
                db.SeasonResults.Add(new SeasonResult { AccountId = stats.AccountId, Season = stats.LpSeason, BestLp = stats.BestLp, League = Code(stats.BestLp, place) });
            await ledger.ApplyAsync(stats.AccountId, RewardChips[league], 0, "season", $"season:{stats.LpSeason}:reward", ct);
        }
        stats.Lp = stats.LpSeason == 0 ? stats.Lp : SeasonReset(stats.Lp, season - stats.LpSeason);
        stats.BestLp = stats.Lp;
        stats.Shield = 0;
        stats.LpSeason = season;
    }
}
