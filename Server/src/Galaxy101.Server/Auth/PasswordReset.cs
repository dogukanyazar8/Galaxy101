using System.Collections.Concurrent;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Galaxy101.Contracts;
using Galaxy101.Server.Data;
using Galaxy101.Server.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Galaxy101.Server.Auth;

public sealed class EmailOptions
{
    public const string Section = "Email";
    /// <summary>SMTP relay; empty = e-mail is not configured and password reset answers 503 email_unavailable.</summary>
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
    public string From { get; set; } = "";
    public string FromName { get; set; } = "Galaxy 101";
}

public interface IEmailSender
{
    bool Configured { get; }
    Task SendAsync(string to, string subject, string body, CancellationToken ct);
}

public sealed class SmtpEmailSender(IOptions<EmailOptions> options) : IEmailSender
{
    public bool Configured => !string.IsNullOrWhiteSpace(options.Value.Host) && !string.IsNullOrWhiteSpace(options.Value.From);

    public async Task SendAsync(string to, string subject, string body, CancellationToken ct)
    {
        var o = options.Value;
        using var client = new SmtpClient(o.Host, o.Port) { EnableSsl = o.EnableSsl, DeliveryMethod = SmtpDeliveryMethod.Network };
        if (!string.IsNullOrEmpty(o.User)) client.Credentials = new NetworkCredential(o.User, o.Password);
        using var message = new MailMessage(new MailAddress(o.From, o.FromName), new MailAddress(to))
        {
            Subject = subject, Body = body, BodyEncoding = Encoding.UTF8, SubjectEncoding = Encoding.UTF8,
        };
        await client.SendMailAsync(message, ct);
    }
}

/// <summary>
/// One-time six-digit codes for "Şifremi unuttum": 15 minutes, five tries, one new code a minute per e-mail. Kept in
/// memory like the socket tickets (single instance); a restart only means asking for a new code. Only a hash is held.
/// </summary>
public sealed class PasswordResetCodes(TimeProvider clock)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15), Cooldown = TimeSpan.FromMinutes(1);
    public const int MaxTries = 5;

    private sealed class Entry { public required byte[] Hash; public DateTimeOffset Issued, Expires; public int Tries; }
    private readonly ConcurrentDictionary<string, Entry> codes = new();

    /// <summary>A new code, or null while the previous one is younger than the cooldown.</summary>
    public string? Issue(string email)
    {
        var now = clock.GetUtcNow();
        if (codes.Count > 1024) foreach (var (key, value) in codes) if (value.Expires <= now) codes.TryRemove(key, out _);
        if (codes.TryGetValue(email, out var previous) && now - previous.Issued < Cooldown) return null;
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        codes[email] = new Entry { Hash = SHA256.HashData(Encoding.UTF8.GetBytes(code)), Issued = now, Expires = now + Lifetime };
        return code;
    }

    /// <summary>True once for the right, unexpired code; wrong tries count and the fifth one burns the code.</summary>
    public bool Redeem(string email, string? code)
    {
        if (!codes.TryGetValue(email, out var entry)) return false;
        lock (entry)
        {
            // Two requests with the same code: the first one takes it out, the second finds it gone (one use only).
            if (!codes.TryGetValue(email, out var current) || !ReferenceEquals(current, entry)) return false;
            if (entry.Expires <= clock.GetUtcNow() || entry.Tries >= MaxTries) { codes.TryRemove(email, out _); return false; }
            var ok = code is { Length: 6 } && CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(code)), entry.Hash);
            if (ok || ++entry.Tries >= MaxTries) codes.TryRemove(email, out _);
            return ok;
        }
    }
}

public static class PasswordResetEndpoints
{
    /// <summary>"Şifremi unuttum" (spec/18 S03): a code by e-mail, then a new password with it.</summary>
    public static void MapPasswordReset(this RouteGroupBuilder anonymous)
    {
        // The answer is the same whether the e-mail has an account or not (no account probing).
        anonymous.MapPost("/password/forgot", async (PasswordForgotRequest request, GameDb db, PasswordResetCodes codes, IEmailSender mail,
            IHostEnvironment environment, ILogger<PasswordResetCodes> log, CancellationToken ct) =>
        {
            var email = Passwords.Normalize(request.Email);
            if (!Passwords.ValidEmail(email)) return Api.Error(ErrorCodes.Validation);
            bool development = environment.IsDevelopment();
            if (!mail.Configured && !development) return Api.Error(ErrorCodes.EmailUnavailable, StatusCodes.Status503ServiceUnavailable);
            if (!await db.PasswordLogins.AnyAsync(x => x.Email == email, ct)) return Results.NoContent();
            if (codes.Issue(email) is not { } code) return Results.NoContent(); // one a minute; the last one still works
            if (!mail.Configured) { log.LogInformation("Development: password reset code for {Email} is {Code}", email, code); return Results.NoContent(); }
            try
            {
                await mail.SendAsync(email, "Galaxy 101 · Şifre sıfırlama kodu / Password reset code",
                    $"Şifre sıfırlama kodun: {code}\nKod 15 dakika geçerlidir. Bu isteği sen yapmadıysan bu e-postayı yok say.\n\n" +
                    $"Your password reset code: {code}\nIt is valid for 15 minutes. If you did not ask for it, ignore this e-mail.", ct);
            }
            catch (Exception error) when (error is SmtpException or InvalidOperationException or IOException)
            {
                log.LogError(error, "Password reset e-mail could not be sent");
                return Api.Error(ErrorCodes.EmailUnavailable, StatusCodes.Status503ServiceUnavailable);
            }
            return Results.NoContent();
        });

        // A new password ends every session of the account (all devices sign in again) and lifts a wrong-password lock.
        anonymous.MapPost("/password/reset", async (PasswordResetRequest request, GameDb db, PasswordResetCodes codes, SessionCutoffs sessions,
            Realtime.ConnectionRegistry live, TimeProvider clock, CancellationToken ct) =>
        {
            var email = Passwords.Normalize(request.Email);
            if (!Passwords.ValidPassword(request.Password)) return Api.Error(ErrorCodes.Validation, error: V4Errors.E_PASS_SHORT);
            if (!codes.Redeem(email, request.Code?.Trim())) return Api.Error(ErrorCodes.CodeInvalid, error: V4Errors.E_RESET_CODE);
            var login = await db.PasswordLogins.FirstOrDefaultAsync(x => x.Email == email, ct);
            if (login is null) return Api.Error(ErrorCodes.CodeInvalid, error: V4Errors.E_RESET_CODE);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            login.Hash = Passwords.Hash(request.Password!); login.FailedAttempts = 0; login.LockedUntil = null;
            await db.SaveChangesAsync(ct);
            var now = clock.GetUtcNow();
            await db.RefreshTokens.Where(x => x.AccountId == login.AccountId && x.RevokedAt == null).ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAt, now), ct);
            await tx.CommitAsync(ct);
            // Sessions already open end too: their access tokens stop working and the game socket closes.
            sessions.Cut(login.AccountId);
            live.Disconnect(login.AccountId);
            return Results.NoContent();
        });
    }
}
