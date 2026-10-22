using System.Security.Cryptography;
using Galaxy101.Contracts;
using Galaxy101.Server.Data;
using Galaxy101.Server.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Galaxy101.Server.Auth;

public sealed class TokenService(GameDb db, TimeProvider clock, IOptions<AuthOptions> options)
{
    private readonly AuthOptions auth = options.Value;
    private static readonly JsonWebTokenHandler Handler = new();

    public static SymmetricSecurityKey SigningKey(AuthOptions auth)
    {
        var bytes = Convert.FromBase64String(auth.SigningKey);
        if (bytes.Length < 32) throw new InvalidOperationException("Auth:SigningKey must be at least 32 bytes (base64).");
        return new SymmetricSecurityKey(bytes);
    }

    /// <summary>Issues an access token and starts a new refresh family (sign-in). Caller saves changes.</summary>
    public async Task<TokenResponse> IssueAsync(Guid accountId, bool isNew, CancellationToken ct)
    {
        var refresh = NewRefresh(accountId, Guid.NewGuid(), out var raw);
        db.RefreshTokens.Add(refresh);
        await db.SaveChangesAsync(ct);
        return await Response(accountId, raw, isNew, ct);
    }

    /// <summary>Rotates a refresh token. Presenting an already rotated token revokes its whole family.</summary>
    public async Task<TokenResponse?> RefreshAsync(string raw, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(raw) || raw.Length > 128) return null;
        var hash = Hash(raw);
        var current = await db.RefreshTokens.AsNoTracking().FirstOrDefaultAsync(x => x.TokenHash == hash, ct);
        var now = clock.GetUtcNow();
        if (current is null || current.RevokedAt is not null || current.ExpiresAt <= now) return null;
        // The conditional update is the single winner between concurrent refreshes of one token.
        var rotated = await db.RefreshTokens.Where(x => x.Id == current.Id && x.RotatedAt == null && x.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.RotatedAt, now), ct);
        if (rotated == 0)
        {
            await RevokeFamilyAsync(current.FamilyId, ct);
            return null;
        }
        db.RefreshTokens.Add(NewRefresh(current.AccountId, current.FamilyId, out var next));
        await db.SaveChangesAsync(ct);
        return await Response(current.AccountId, next, false, ct);
    }

    public async Task RevokeAsync(Guid accountId, string raw, CancellationToken ct)
    {
        var hash = Hash(raw ?? "");
        var family = await db.RefreshTokens.Where(x => x.TokenHash == hash && x.AccountId == accountId).Select(x => (Guid?)x.FamilyId).FirstOrDefaultAsync(ct);
        if (family is { } id) await RevokeFamilyAsync(id, ct);
    }

    private Task<int> RevokeFamilyAsync(Guid family, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        return db.RefreshTokens.Where(x => x.FamilyId == family && x.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), ct);
    }

    private async Task<TokenResponse> Response(Guid accountId, string refresh, bool isNew, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var access = Handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = auth.Issuer,
            Audience = auth.Audience,
            Claims = new Dictionary<string, object> { [JwtRegisteredClaimNames.Sub] = accountId.ToString() },
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = now.AddMinutes(auth.AccessTokenMinutes).UtcDateTime,
            SigningCredentials = new SigningCredentials(SigningKey(auth), SecurityAlgorithms.HmacSha256),
        });
        var providers = await db.ExternalLogins.Where(x => x.AccountId == accountId).Select(x => x.Provider).ToListAsync(ct);
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
