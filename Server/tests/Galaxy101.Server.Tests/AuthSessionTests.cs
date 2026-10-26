using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Data.Common;
using Galaxy101.Contracts;
using Galaxy101.Server.Auth;
using Galaxy101.Server.Data;
using Galaxy101.Server.Infrastructure;
using Galaxy101.Server.Realtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Galaxy101.Server.Tests;

[Collection(ServerCollection.Name)]
public sealed class AuthSessionTests(ServerFixture server)
{
    [Fact]
    public async Task Reset_in_the_issue_second_revokes_old_access_and_refresh_but_allows_new_login()
    {
        var (anonymous, old, tokens, email) = await RegisterAsync();
        using (anonymous) using (old)
        {
            await ResetAsync(anonymous, email);
            Assert.Equal(HttpStatusCode.Unauthorized, (await old.GetAsync("/v1/profile")).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/v1/auth/refresh",
                new RefreshRequest { RefreshToken = tokens.RefreshToken })).StatusCode);
            var login = await anonymous.PostAsJsonAsync("/v1/auth/login", new PasswordLoginRequest { Email = email, Password = "Newpass101" });
            login.EnsureSuccessStatusCode();
            var fresh = (await login.Content.ReadFromJsonAsync<TokenResponse>())!;
            old.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", fresh.AccessToken);
            Assert.Equal(HttpStatusCode.OK, (await old.GetAsync("/v1/profile")).StatusCode);
        }
    }

    [Fact]
    public async Task Reset_rejects_an_unused_ticket_and_disconnects_the_open_socket()
    {
        var (anonymous, old, _, email) = await RegisterAsync();
        using (anonymous) using (old)
        {
            await using var live = await RealtimeClient.ConnectAsync(server, old);
            var response = await old.PostAsync("/v1/realtime/ticket", null);
            response.EnsureSuccessStatusCode();
            var ticket = (await response.Content.ReadFromJsonAsync<RealtimeTicketResponse>())!.Ticket;
            await ResetAsync(anonymous, email);
            await live.ClosedAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var error = await Record.ExceptionAsync(async () =>
            {
                using var socket = await server.Server.CreateWebSocketClient().ConnectAsync(
                    new Uri("ws://localhost/v1/ws?ticket=" + Uri.EscapeDataString(ticket)), timeout.Token);
                socket.Abort();
            });
            Assert.NotNull(error);
            Assert.Contains("401", error.Message);
        }
    }

    [Fact]
    public async Task Reset_revocation_survives_a_new_server_instance()
    {
        var (anonymous, old, _, email) = await RegisterAsync();
        using (anonymous) using (old)
        {
            await ResetAsync(anonymous, email);
            using var restarted = server.WithWebHostBuilder(_ => { });
            using var client = restarted.CreateClient();
            client.DefaultRequestHeaders.Authorization = old.DefaultRequestHeaders.Authorization;
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/v1/profile")).StatusCode);
        }
    }

    [Fact]
    public async Task Signed_tokens_without_a_session_version_are_rejected()
    {
        var (client, tokens) = await server.GuestAsync();
        using (client)
        {
            var auth = server.Services.GetRequiredService<IOptions<AuthOptions>>().Value;
            var now = server.Clock.GetUtcNow();
            var legacy = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Issuer = auth.Issuer, Audience = auth.Audience,
                Claims = new Dictionary<string, object> { [JwtRegisteredClaimNames.Sub] = tokens.AccountId.ToString() },
                IssuedAt = now.UtcDateTime, NotBefore = now.UtcDateTime, Expires = now.AddMinutes(5).UtcDateTime,
                SigningCredentials = new SigningCredentials(TokenService.SigningKey(auth), SecurityAlgorithms.HmacSha256),
            });
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", legacy);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/v1/profile")).StatusCode);
        }
    }

    [Fact]
    public async Task Refresh_read_before_reset_cannot_create_a_session_after_reset_commits()
    {
        var (anonymous, old, tokens, email) = await RegisterAsync();
        using (anonymous) using (old)
        {
            var barrier = new RefreshReadBarrier();
            var options = new DbContextOptionsBuilder<GameDb>().UseNpgsql(server.DatabaseServer)
                .UseSnakeCaseNamingConvention().AddInterceptors(barrier).Options;
            await using var db = new GameDb(options);
            var service = new TokenService(db, server.Clock, server.Services.GetRequiredService<IOptions<AuthOptions>>());
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var refresh = service.RefreshAsync(tokens.RefreshToken, timeout.Token);
            try
            {
                await barrier.Entered.Task.WaitAsync(timeout.Token);
                await ResetAsync(anonymous, email);
            }
            finally { barrier.Resume.TrySetResult(); }
            Assert.Null(await refresh);
        }
    }

    [Fact]
    public async Task Password_verified_before_reset_cannot_issue_a_new_epoch_session()
    {
        var (anonymous, old, tokens, email) = await RegisterAsync();
        using (anonymous) using (old)
        {
            await using var scope = server.Services.CreateAsyncScope();
            var passwords = scope.ServiceProvider.GetRequiredService<Passwords>();
            var verified = await passwords.LoginAsync(new PasswordLoginRequest { Email = email, Password = "Oldpass101" }, CancellationToken.None);
            Assert.Equal(tokens.AccountId, verified.Account);
            Assert.NotNull(verified.SessionVersion);
            await ResetAsync(anonymous, email);
            var service = scope.ServiceProvider.GetRequiredService<TokenService>();
            await Assert.ThrowsAsync<AccountSessionEndedException>(() => service.IssueAsync(tokens.AccountId, false,
                CancellationToken.None, verified.SessionVersion));
        }
    }

    [Fact]
    public async Task An_old_handshake_and_delayed_reset_disconnect_preserve_the_new_epoch_socket()
    {
        var (client, tokens) = await server.GuestAsync();
        using (client)
        using (var freshSocket = WebSocket.CreateFromStream(new MemoryStream(), true, null, Timeout.InfiniteTimeSpan))
        using (var staleSocket = WebSocket.CreateFromStream(new MemoryStream(), true, null, Timeout.InfiniteTimeSpan))
        {
            var registry = server.Services.GetRequiredService<ConnectionRegistry>();
            var fresh = new Connection(tokens.AccountId, freshSocket, NullLogger<Connection>.Instance, sessionVersion: 1);
            var stale = new Connection(tokens.AccountId, staleSocket, NullLogger<Connection>.Instance, sessionVersion: 0);
            try
            {
                registry.Register(fresh);
                registry.Register(stale);
                Assert.True(stale.Closing.IsCancellationRequested);
                Assert.False(fresh.Closing.IsCancellationRequested);
                registry.Disconnect(tokens.AccountId, beforeVersion: 1);
                Assert.True(registry.IsOnline(tokens.AccountId));
                Assert.False(fresh.Closing.IsCancellationRequested);
                registry.Disconnect(tokens.AccountId, beforeVersion: 2);
                Assert.False(registry.IsOnline(tokens.AccountId));
                Assert.True(fresh.Closing.IsCancellationRequested);
            }
            finally { registry.Disconnect(tokens.AccountId); }
        }
    }

    private sealed class RefreshReadBarrier : DbCommandInterceptor
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Resume { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int armed = 1;

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FROM refresh_tokens", StringComparison.OrdinalIgnoreCase) && Interlocked.Exchange(ref armed, 0) == 1)
            {
                Entered.TrySetResult();
                await Resume.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }

    private async Task<(HttpClient Anonymous, HttpClient Old, TokenResponse Tokens, string Email)> RegisterAsync()
    {
        var email = "session" + Guid.NewGuid().ToString("N")[..12] + "@example.test";
        var anonymous = server.CreateClient();
        var response = await anonymous.PostAsJsonAsync("/v1/auth/register",
            new RegisterRequest { Name = "Session", Email = email, Password = "Oldpass101", AcceptTerms = true });
        response.EnsureSuccessStatusCode();
        var tokens = (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
        var old = server.CreateClient();
        old.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return (anonymous, old, tokens, email);
    }

    private async Task ResetAsync(HttpClient anonymous, string email)
    {
        Assert.Equal(HttpStatusCode.NoContent, (await anonymous.PostAsJsonAsync("/v1/auth/password/forgot", new PasswordForgotRequest { Email = email })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await anonymous.PostAsJsonAsync("/v1/auth/password/reset",
            new PasswordResetRequest { Email = email, Code = server.Mail.CodeFor(email), Password = "Newpass101" })).StatusCode);
    }
}
