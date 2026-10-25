using Galaxy101.Contracts;

namespace Galaxy101.Server.Profile;

/// <summary>Level from total XP and league from season LP. Pure functions; the menu shows both.</summary>
public static class Progression
{
    /// <summary>XP needed to go from level n to n+1 (level 24 → 9 000, as in the profile reference).</summary>
    public static long XpForLevel(int level) => 375L * level;

    public static (int Level, long Into, long Next) Level(long xp)
    {
        var level = 1;
        while (xp >= XpForLevel(level) && level < 999) { xp -= XpForLevel(level); level++; }
        return (level, xp, XpForLevel(level));
    }

    /// <summary>Level and the season's league (<see cref="Ranked"/>) for the profile.</summary>
    public static ProgressResponse Describe(long xp, int lp, int place, int bestLp, int shield, int season, DateTimeOffset seasonEnd)
    {
        var (level, into, next) = Level(xp);
        var nextCode = Ranked.NextCode(lp);
        return new ProgressResponse
        {
            Xp = xp, Level = level, LevelXp = into, NextLevelXp = next,
            Lp = lp, League = Ranked.Code(lp, place), NextLeague = nextCode, NextLeagueLp = nextCode is null ? lp : (Ranked.Division(lp) + 1) * Ranked.DivisionLp,
            DivisionLp = Ranked.IntoDivision(lp), Season = season, SeasonEndsAt = seasonEnd, SeasonRank = place,
            BestLeague = Ranked.Code(Math.Max(bestLp, lp), place), Protected = shield,
        };
    }
}
