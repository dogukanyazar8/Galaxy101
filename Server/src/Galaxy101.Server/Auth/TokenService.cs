using System.Security.Cryptography;
using System.Globalization;
using System.Security.Claims;
using Galaxy101.Contracts;
using Galaxy101.Server.Data;
using Galaxy101.Server.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Galaxy101.Server.Auth;

/// <summary>Sign-in or token renewal of an account under a moderator's ban (answered 403 "banned" with the seconds left).</summary>
public sealed class AccountBannedException(DateTimeOffset until) : Exception("Account banned.")
{
    public DateTimeOffset Until { get; } = until;
}

public sealed class AccountSessionEndedException() : Exception("Session ended.");

public sealed class TokenService(GameDb db, TimeProvider clock, IOptions<AuthOptions> options)
{
    private readonly AuthOptions auth = options.Value;
    private static readonly JsonWebTokenHandler Handler = new();
    public const string SessionVersionClaim = "session_version";

    public static bool TrySessionVersion(ClaimsPrincipal? principal, out long version) =>
        long.TryParse(principal?.FindFirst(SessionVersionClaim)?.Value, NumberStyles.None, CultureInfo.InvariantCulture, out version) && version >= 0;

    public static SymmetricSecurityKey SigningKey(AuthOptions auth)
    {
        var bytes = Convert.FromBase64String(auth.SigningKey);
        if (bytes.Length < 32) throw new InvalidOperationException("Auth:SigningKey must be at least 32 bytes (base64).");
        return new SymmetricSecurityKey(bytes);
    }

    /// <summary>Issues an access token and starts a new refresh family, serialized with password reset.</summary>
    public async Task<TokenResponse> IssueAsync(Guid accountId, bool isNew, CancellationToken ct, long? expectedVersion = null)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var account = await LockAccountAsync(db, accountId, ct) ?? throw new AccountSessionEndedException();
        if (expectedVersion is { } expected && account.SessionVersion != expected) throw new AccountSessionEndedException();
        ThrowIfBanned(account, clock.GetUtcNow());
        await PurgeExpiredAsync(accountId, ct);
        var providers = await ProvidersAsync(accountId, ct);
        var refresh = NewRefresh(accountId, Guid.NewGuid(), out var raw);
        db.RefreshTokens.Add(refresh);
        await db.SaveChangesAsync(ct);
        var response = Response(accountId, account.SessionVersion, raw, isNew, providers);
        await tx.CommitAsync(ct);
        return response;
    }

    // Reset, sign-in and refresh take the same account lock before changing any session rows. A refresh read before
    // reset must re-read its revocation state after acquiring this lock; it cannot resurrect a revoked family.
    internal static Task<Account?> LockAccountAsync(GameDb db, Guid accountId, CancellationToken ct) =>
        db.Accounts.FromSqlInterpolated($"SELECT * FROM accounts WHERE id = {accountId} FOR UPDATE")
            .AsNoTracking().SingleOrDefaultAsync(ct);

    private static void ThrowIfBanned(Account account, DateTimeOffset now)
    {
        if (account.BannedUntil is { } until && until > now) throw new AccountBannedException(until);
    }

    /// <summary>Rotates a refresh token. Presenting an already rotated token revokes its whole family.</summary>
    public async Task<TokenResponse?> RefreshAsync(string raw, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(raw) || raw.Length > 128) return null;
        var hash = Hash(raw);
        var current = await db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(x => x.TokenHash == hash, ct);
        var now = clock.GetUtcNow();
        if (current is null || current.RevokedAt is not null || current.ExpiresAt <= now) return null;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var account = await LockAccountAsync(db, current.AccountId, ct);
        if (account is null) return null;
        current = await db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(x => x.Id == current.Id, ct);
        now = clock.GetUtcNow();
        if (current is null || current.RevokedAt is not null || current.ExpiresAt <= now) return null;
        // A banned account keeps its token (unspent) for after the ban: nothing is rotated.
        ThrowIfBanned(account, now);
        // Housekeeping and reads come first: once the rotation commits, nothing may fail before the client has its new
        // token (retrying with the spent one would look like reuse and revoke the family).
        await PurgeExpiredAsync(current.AccountId, ct);
        var providers = await ProvidersAsync(current.AccountId, ct);
        // Rotation and its successor commit together: a failed insert must not leave the token spent with no heir (the
        // next refresh would then look like reuse and revoke the family).
        // The conditional update is the single winner between concurrent refreshes of one token.
        var rotated = await db.RefreshTokens.Where(x => x.Id == current.Id && x.RotatedAt == null && x.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RotatedAt, now), ct);
        if (rotated == 0)
        {
            await RevokeFamilyAsync(current.FamilyId, ct);
            await tx.CommitAsync(ct);
            return null;
        }
        db.RefreshTokens.Add(NewRefresh(current.AccountId, current.FamilyId, out var next));
        await db.SaveChangesAsync(ct);
        var response = Response(current.AccountId, account.SessionVersion, next, false, providers);
        await tx.CommitAsync(ct);
        return response;
    }

    public async Task RevokeAsync(Guid accountId, string raw, CancellationToken ct)
    {
        var hash = Hash(raw ?? "");
        var family = await db.RefreshTokens.Where(x => x.TokenHash == hash && x.AccountId == accountId).Select(x => (Guid?)x.FamilyId).FirstOrDefaultAsync(ct);
        if (family is { } id) await RevokeFamilyAsync(id, ct);
    }

    /// <summary>
    /// Retention: every rotation leaves a row, kept until it expires so a replayed token is still recognised; past its
    /// expiry it proves nothing and goes.
    /// </summary>
    private Task<int> PurgeExpiredAsync(Guid accountId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return db.RefreshTokens.Where(x => x.AccountId == accountId && x.ExpiresAt <= now).ExecuteDeleteAsync(ct);
    }

    private Task<int> RevokeFamilyAsync(Guid family, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return db.RefreshTokens.Where(x => x.FamilyId == family && x.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), ct);
    }

    private Task<List<string>> ProvidersAsync(Guid accountId, CancellationToken ct) =>
        db.ExternalLogins.Where(x => x.AccountId == accountId).Select(x => x.Provider).ToListAsync(ct);

    private TokenResponse Response(Guid accountId, long sessionVersion, string refresh, bool isNew, List<string> providers)
    {
        var now = clock.GetUtcNow();
        var access = Handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = auth.Issuer,
            Audience = auth.Audience,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = accountId.ToString(),
                [SessionVersionClaim] = sessionVersion.ToString(CultureInfo.InvariantCulture),
            },
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = now.AddMinutes(auth.AccessTokenMinutes).UtcDateTime,
            SigningCredentials = new SigningCredentials(SigningKey(auth), SecurityAlgorithms.HmacSha256),
        });
        return new TokenResponse
        {
            AccountId = accountId, AccessToken = access, ExpiresIn = auth.AccessTokenMinutes * 60,
            RefreshToken = refresh, IsNewAccount = isNew, LinkedProviders = providers,
        };
    }

    private RefreshToken NewRefresh(Guid accountId, Guid family, out string raw)
    {
        raw = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var now = clock.GetUtcNow();
        return new RefreshToken
        {
            Id = Guid.CreateVersion7(), AccountId = accountId, FamilyId = family, TokenHash = Hash(raw),
            CreatedAt = now, ExpiresAt = now.AddDays(auth.RefreshTokenDays),
        };
    }

    private static byte[] Hash(string raw) => SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw));
}
