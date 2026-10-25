using System.Net.Http.Headers;
using System.Net.Http.Json;
using Galaxy101.Contracts;
using Galaxy101.Server.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Testcontainers.PostgreSql;

namespace Galaxy101.Server.Tests;

/// <summary>One PostgreSQL container and one server per test collection; tests isolate by creating fresh accounts.</summary>
public sealed class ServerFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    public FakeTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 10, 22, 9, 0, 0, TimeSpan.Zero));
    public FakeIdentityValidator Identities { get; } = new();
    public FakeEmailSender Mail { get; } = new();
    public const string AdminKey = "test-admin-key-0123456789-abcdefghij";

    public async Task InitializeAsync() => await postgres.StartAsync();
    /// <summary>The test PostgreSQL server (migration tests create databases of their own on it).</summary>
    public string DatabaseServer => postgres.GetConnectionString();
    async Task IAsyncLifetime.DisposeAsync() { await DisposeAsync(); await postgres.DisposeAsync(); }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Game", postgres.GetConnectionString());
        builder.UseSetting("Auth:SigningKey", Convert.ToBase64String(new byte[32]));
        builder.UseSetting("RateLimits:AuthPerMinute", "100000");
        builder.UseSetting("RateLimits:RequestBurst", "100000");
        builder.UseSetting("Game:HandsPerMatch", "2");
        builder.UseSetting("Game:TimeoutsBeforeAuto", "1");
        builder.UseSetting("Admin:Key", AdminKey);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.RemoveAll<IExternalIdentityValidator>();
            services.AddSingleton<IExternalIdentityValidator>(Identities);
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Mail);
        });
    }

    /// <summary>A client signed in as a brand-new guest account.</summary>
    public async Task<(HttpClient Client, TokenResponse Tokens)> GuestAsync()
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/v1/auth/guest", new GuestLoginRequest { Platform = "test" });
        response.EnsureSuccessStatusCode();
        var tokens = (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return (client, tokens);
    }

    /// <summary>Moves the server clock forward and renews the client's (now expired) access token.</summary>
    public async Task<TokenResponse> AdvanceAsync(TimeSpan by, HttpClient client, TokenResponse tokens)
    {
        Clock.Advance(by);
        var response = await CreateClient().PostAsJsonAsync("/v1/auth/refresh", new RefreshRequest { RefreshToken = tokens.RefreshToken });
        var renewed = (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", renewed.AccessToken);
        return renewed;
    }
}

/// <summary>Keeps the e-mails the server would send.</summary>
public sealed class FakeEmailSender : IEmailSender
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<(string To, string Body)> sent = new();
    public bool Configured => true;
    public Task SendAsync(string to, string subject, string body, CancellationToken ct) { sent.Enqueue((to, body)); return Task.CompletedTask; }
    public int CountFor(string to) => sent.Count(m => m.To == to);
    /// <summary>The six-digit code in the latest e-mail to <paramref name="to"/>.</summary>
    public string? CodeFor(string to) =>
        sent.Where(m => m.To == to).Select(m => System.Text.RegularExpressions.Regex.Match(m.Body, @"[0-9]{6}").Value).LastOrDefault();
}

public sealed class FakeIdentityValidator : IExternalIdentityValidator
{
    /// <summary>Token format for tests: "provider:subject".</summary>
    public Task<ExternalIdentity?> ValidateAsync(string provider, string idToken, CancellationToken ct) =>
        Task.FromResult(idToken?.Split(':', 2) is [var p, var s] && p == provider ? new ExternalIdentity(p, s) : null);
}

[CollectionDefinition(Name)]
public sealed class ServerCollection : ICollectionFixture<ServerFixture>
{
    public const string Name = "server";
}
