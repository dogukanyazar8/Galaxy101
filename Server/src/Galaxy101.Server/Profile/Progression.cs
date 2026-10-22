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

    /// <summary>Ascending (code, minimum LP). Codes follow the client tables: g2 = Gold II, d1 = Diamond I.</summary>
    public static readonly (string Code, int MinLp)[] Leagues =
        [("b2", 0), ("b1", 150), ("s2", 300), ("s1", 450), ("g2", 600), ("g1", 800), ("d2", 1000), ("d1", 1300)];

    public static string League(int lp)
    {
        var code = Leagues[0].Code;
        foreach (var (c, min) in Leagues) if (lp >= min) code = c;
        return code;
    }

    public static int NextLeagueLp(int lp)
    {
        foreach (var (_, min) in Leagues) if (lp < min) return min;
        return lp;
    }

    public static ProgressResponse Describe(long xp, int lp)
    {
        var (level, into, next) = Level(xp);
        return new ProgressResponse { Xp = xp, Level = level, LevelXp = into, NextLevelXp = next, Lp = lp, League = League(lp), NextLeagueLp = NextLeagueLp(lp) };
    }
}
