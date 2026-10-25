using System.Text.RegularExpressions;
using Galaxy101.Contracts;
using Galaxy101.Server.Data;
using Galaxy101.Server.Profile;
using Microsoft.EntityFrameworkCore;

namespace Galaxy101.Server.Social;

public static partial class Players
{
    /// <summary>Public cards for a set of accounts, keyed by id (deleted accounts are simply absent).</summary>
    /// <summary>
    /// <paramref name="season"/> &gt; 0: leagues as of that season (an account not yet rolled over shows its reset LP, and
    /// Galaksi is checked against the season board); 0: the stored LP as it is.
    /// </summary>
    public static async Task<Dictionary<Guid, PlayerSummary>> SummariesAsync(GameDb db, IReadOnlyCollection<Guid> ids, CancellationToken ct, int season = 0)
    {
        if (ids.Count == 0) return [];
        var rows = await (from a in db.Accounts.AsNoTracking()
                          join s in db.Stats on a.Id equals s.AccountId
                          where ids.Contains(a.Id)
                          select new { a.Id, a.Name, a.Color, a.Frame, a.FriendCode, s.Xp, s.Lp, s.LpSeason, s.Hands, s.Wins }).ToListAsync(ct);
        var result = new Dictionary<Guid, PlayerSummary>();
        foreach (var r in rows)
        {
            var lp = season > 0 ? Ranked.SeasonLp(r.Lp, r.LpSeason, season) : r.Lp;
            var place = season > 0 && lp >= Ranked.ApexLp ? await Ranked.PlaceAsync(db, LeaderboardEndpoints.SeasonBoard(season), r.Id, lp, ct) : 0;
            result[r.Id] = new PlayerSummary
            {
                Id = r.Id, Name = r.Name, Color = r.Color, Frame = r.Frame, FriendCode = r.FriendCode,
                Level = Progression.Level(r.Xp).Level, League = Ranked.Code(lp, place),
                Hands = r.Hands, WinRate = r.Hands == 0 ? 0 : (int)Math.Round(100.0 * r.Wins / r.Hands),
            };
        }
        return result;
    }

    public static async Task<PlayerSummary?> SummaryAsync(GameDb db, Guid id, CancellationToken ct) =>
        (await SummariesAsync(db, [id], ct)).GetValueOrDefault(id);

    [GeneratedRegex(@"\A([A-Z]{2})-?([0-9]{4})\z")]
    private static partial Regex CodePattern();

    /// <summary>"dy 4821", "#DY-4821", "DY4821" → "DY-4821"; null when the text is not a friend code.</summary>
    public static string? NormalizeCode(string? value)
    {
        var compact = string.Concat((value ?? "").Where(c => c != '#' && !char.IsWhiteSpace(c))).ToUpperInvariant();
        var match = CodePattern().Match(compact);
        return match.Success ? $"{match.Groups[1].Value}-{match.Groups[2].Value}" : null;
    }

    public static (Guid Low, Guid High) Pair(Guid a, Guid b) => a.CompareTo(b) < 0 ? (a, b) : (b, a);
}
