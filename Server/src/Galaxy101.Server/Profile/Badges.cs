using System.Numerics;
using Galaxy101.Contracts;
using Galaxy101.Server.Data;

namespace Galaxy101.Server.Profile;

/// <summary>Profile badges, in the order of the reference profile panel. Progress is derived from stats; nothing is stored.</summary>
public static class Badges
{
    private const int Doubling = 2;

    public static List<BadgeResponse> Evaluate(PlayerStats s, int planetsTried) =>
    [
        Badge("first_win", s.Wins, 1),
        Badge("okey_master", s.OkeyFinishes, 50),
        Badge("five_pairs", s.PairsOpenings, 5),
        Badge("hundred_hands", s.Hands, 100),
        Badge("doubling_king", s.ModeWins[Doubling], 25),
        Badge("win_streak", s.BestStreak, 7),
        Badge("diamond_league", s.Lp, 1000),
        Badge("galaxy_traveller", BitOperations.PopCount((uint)planetsTried), ProfileRules.Planets.Length),
        Badge("legend", s.Wins, 1000),
    ];

    private static BadgeResponse Badge(string id, long current, long target) =>
        new() { Id = id, Current = Math.Min(current, target), Target = target, Unlocked = current >= target };
}
