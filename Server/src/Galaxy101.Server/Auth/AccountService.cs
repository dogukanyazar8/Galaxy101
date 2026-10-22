using System.Security.Cryptography;
using Galaxy101.Server.Data;
using Galaxy101.Server.Infrastructure;
using Galaxy101.Server.Profile;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Galaxy101.Server.Auth;

public sealed class AccountService(GameDb db, GameCalendar calendar, IOptions<GameOptions> options)
{
    // Letters that cannot be misread as digits (no I, O).
    private const string CodeLetters = "ABCDEFGHJKLMNPQRSTUVWXYZ";

    /// <summary>Creates a guest account with its starting wallet. Friend-code collisions are retried.</summary>
    public async Task<Account> CreateAsync(string? platform, string? deviceId, CancellationToken ct)
    {
        var game = options.Value;
        for (var attempt = 0; ; attempt++)
        {
            var now = calendar.Now;
            var digits = RandomNumberGenerator.GetInt32(10_000);
            var account = new Account
            {
                Id = Guid.CreateVersion7(),
                FriendCode = $"{CodeLetters[RandomNumberGenerator.GetInt32(CodeLetters.Length)]}{CodeLetters[RandomNumberGenerator.GetInt32(CodeLetters.Length)]}-{digits:D4}",
                Name = $"Oyuncu {digits:D4}",
                Color = ProfileRules.AvatarColors[RandomNumberGenerator.GetInt32(ProfileRules.AvatarColors.Length)],
                SettingsJson = ProfileRules.DefaultSettingsJson,
                Platform = Trim(platform, 16), DeviceId = Trim(deviceId, 128),
                PlanetsTried = ProfileRules.PlanetBit(ProfileRules.Planets[0]),
                CreatedAt = now, LastSeenAt = now,
            };
            db.Accounts.Add(account);
            db.Wallets.Add(new Wallet { AccountId = account.Id, Chips = game.StartingChips, Gems = game.StartingGems });
            db.Stats.Add(new PlayerStats { AccountId = account.Id });
            db.DailyStates.Add(new DailyState { AccountId = account.Id, QuestDay = calendar.Today });
            if (game.StartingChips > 0) db.Ledger.Add(Grant(account.Id, "chips", game.StartingChips, now));
            if (game.StartingGems > 0) db.Ledger.Add(Grant(account.Id, "gems", game.StartingGems, now));
            try
            {
                await db.SaveChangesAsync(ct);
                return account;
            }
            catch (DbUpdateException error) when (error.IsUniqueViolation() && attempt < 8)
            {
                db.ChangeTracker.Clear();
            }
        }
    }

    private static LedgerEntry Grant(Guid account, string currency, long amount, DateTimeOffset now) => new()
    {
        AccountId = account, Currency = currency, Delta = amount, BalanceAfter = amount, Reason = "signup",
        IdempotencyKey = "signup#" + currency, CreatedAt = now,
    };

    private static string? Trim(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Length <= max ? value : value[..max];
}
