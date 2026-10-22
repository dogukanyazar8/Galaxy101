using System.Text.RegularExpressions;
using Galaxy101.Contracts;
using Galaxy101.Server.Data;
using Galaxy101.Server.Profile;
using Microsoft.EntityFrameworkCore;

namespace Galaxy101.Server.Social;

public static partial class Players
{
    /// <summary>Public cards for a set of accounts, keyed by id (deleted accounts are simply absent).</summary>
    public static async Task<Dictionary<Guid, PlayerSummary>> SummariesAsync(GameDb db, IReadOnlyCollection<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        var rows = await (from a in db.Accounts.AsNoTracking()
                          join s in db.Stats on a.Id equals s.AccountId
                          where ids.Contains(a.Id)
                          select new { a.Id, a.Name, a.Color, a.Frame, a.FriendCode, s.Xp, s.Lp }).ToListAsync(ct);
        return rows.ToDictionary(r => r.Id, r => new PlayerSummary
        {
            Id = r.Id, Name = r.Name, Color = r.Color, Frame = r.Frame, FriendCode = r.FriendCode,
            Level = Progression.Level(r.Xp).Level, League = Progression.League(r.Lp),
        });
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
