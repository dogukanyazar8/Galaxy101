using System.Net.Mail;
using System.Security.Cryptography;
using Galaxy101.Contracts;
using Galaxy101.Server.Data;
using Galaxy101.Server.Infrastructure;
using Galaxy101.Server.Profile;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Galaxy101.Server.Auth;

/// <summary>
/// E-mail + password accounts (spec/18 S03–S04). PBKDF2-SHA256 with a per-user salt; three wrong passwords lock the
/// e-mail for <see cref="GameOptions.PasswordLockSeconds"/>; the answer tells how many tries are left.
/// </summary>
public sealed class Passwords(GameDb db, AccountService accounts, TimeProvider clock, IOptions<GameOptions> options)
{
    private const int Iterations = 120_000, SaltBytes = 16, HashBytes = 32;

    public sealed record Outcome(Guid? Account, string? Error, int Remaining = 0, int RetryAfter = 0);

    public static string Normalize(string? email) => (email ?? "").Trim().ToLowerInvariant();

    public static bool ValidEmail(string email)
    {
        if (email.Length is < 5 or > 120 || email.Contains(' ')) return false;
        try { return new MailAddress(email).Address == email && email.IndexOf('.', email.IndexOf('@')) > 0; }
        catch (FormatException) { return false; }
    }

    public static bool ValidPassword(string? password) => password is { Length: >= 6 and <= 128 };

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashBytes);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string stored)
    {
        var parts = stored.Split('.');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations)) return false;
        var salt = Convert.FromBase64String(parts[1]); var expected = Convert.FromBase64String(parts[2]);
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public async Task<Outcome> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = Normalize(request.Email);
        var name = (request.Name ?? "").Trim();
        if (!ProfileRules.ValidName(name) || !ValidEmail(email) || !ValidPassword(request.Password)) return new(null, ErrorCodes.Validation);
        if (!request.AcceptTerms) return new(null, ErrorCodes.TermsRequired);
        var color = ProfileRules.AvatarColors.Contains(request.Color) ? request.Color : null;
        if (await db.PasswordLogins.AnyAsync(x => x.Email == email, ct)) return new(null, ErrorCodes.EmailTaken);
        // Account and login in one transaction: a lost race on the e-mail leaves no orphan account behind.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var account = await accounts.CreateAsync(request.Platform, request.DeviceId, ct, registered: true, name: name, color: color);
        db.PasswordLogins.Add(new PasswordLogin { Email = email, AccountId = account.Id, Hash = Hash(request.Password!), CreatedAt = clock.GetUtcNow() });
        try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
        catch (DbUpdateException error) when (error.IsUniqueViolation()) { return new(null, ErrorCodes.EmailTaken); }
        return new(account.Id, null);
    }

    public async Task<Outcome> LoginAsync(PasswordLoginRequest request, CancellationToken ct)
    {
        var email = Normalize(request.Email);
        var login = await db.PasswordLogins.FirstOrDefaultAsync(x => x.Email == email, ct);
        if (login is null) return new(null, ErrorCodes.AccountNotFound);
        var now = clock.GetUtcNow(); var game = options.Value;
        if (login.LockedUntil is { } until && until > now) return new(null, ErrorCodes.AccountLocked, 0, (int)Math.Ceiling((until - now).TotalSeconds));
        if (!Verify(request.Password ?? "", login.Hash))
        {
            login.FailedAttempts = login.LockedUntil is { } expired && expired <= now ? 1 : login.FailedAttempts + 1;
            if (login.FailedAttempts >= game.PasswordAttempts)
            {
                login.LockedUntil = now.AddSeconds(game.PasswordLockSeconds); login.FailedAttempts = 0;
                await db.SaveChangesAsync(ct);
                return new(null, ErrorCodes.AccountLocked, 0, game.PasswordLockSeconds);
            }
            login.LockedUntil = null;
            await db.SaveChangesAsync(ct);
            return new(null, ErrorCodes.WrongPassword, game.PasswordAttempts - login.FailedAttempts);
        }
        login.FailedAttempts = 0; login.LockedUntil = null;
        await db.SaveChangesAsync(ct);
        return new(login.AccountId, null);
    }
}
