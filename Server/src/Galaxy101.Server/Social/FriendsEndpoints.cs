using Galaxy101.Contracts;
using Galaxy101.Server.Data;
using Galaxy101.Server.Infrastructure;
using Galaxy101.Server.Profile;
using Galaxy101.Server.Realtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Galaxy101.Server.Social;

public static class FriendsEndpoints
{
    public static void MapFriends(this IEndpointRouteBuilder app)
    {
        var friends = app.MapGroup("/v1/friends").RequireAuthorization();

        friends.MapGet("", async (HttpContext http, GameDb db, ConnectionRegistry live, CancellationToken ct) =>
        {
            var me = http.User.AccountId();
            var pairs = await db.Friendships.AsNoTracking().Where(f => f.LowId == me || f.HighId == me).ToListAsync(ct);
            var others = pairs.Select(f => f.LowId == me ? f.HighId : f.LowId).ToList();
            var cards = await Players.SummariesAsync(db, others, ct);
            var details = await db.Accounts.AsNoTracking().Where(a => others.Contains(a.Id))
                .Select(a => new { a.Id, a.Status, a.LastSeenAt }).ToDictionaryAsync(a => a.Id, ct);
            var response = new FriendsResponse();
            foreach (var f in pairs)
            {
                var other = f.LowId == me ? f.HighId : f.LowId;
                if (!cards.TryGetValue(other, out var card)) continue;
                if (f.Accepted)
                    response.Friends.Add(new FriendResponse { Player = card, Presence = live.PresenceOf(other), Status = details[other].Status, LastSeen = details[other].LastSeenAt });
                else (f.RequesterId == me ? response.Outgoing : response.Incoming).Add(new FriendRequestResponse { Player = card, SentAt = f.CreatedAt });
            }
            // Online first, then by name, like the reference list.
            response.Friends = [.. response.Friends.OrderBy(x => x.Presence == Presence.Offline).ThenBy(x => x.Player.Name, StringComparer.Create(new System.Globalization.CultureInfo("tr-TR"), true))];
            return Results.Ok(response);
        });

        friends.MapPost("/requests", async (FriendCodeRequest request, HttpContext http, GameDb db, ConnectionRegistry live,
            TimeProvider clock, IOptions<GameOptions> game, CancellationToken ct) =>
        {
            var me = http.User.AccountId();
            var code = Players.NormalizeCode(request.Code);
            var target = code is null ? null : await db.Accounts.AsNoTracking().Where(a => a.FriendCode == code).Select(a => new { a.Id, a.SettingsJson }).FirstOrDefaultAsync(ct);
            if (target is null || target.Id == me) return Api.NotFound();
            if (await ModerationEndpoints.BlockedAsync(db, me, target.Id, ct)) return Api.NotFound(); // a block reads as no such player
            var (low, high) = Players.Pair(me, target.Id);
            // Counted and changed under both accounts' row locks: parallel requests and accepts cannot pass the limit together.
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await LockPairAsync(db, low, high, ct);
            var existing = await db.Friendships.FirstOrDefaultAsync(f => f.LowId == low && f.HighId == high, ct);
            if (existing is { Accepted: true }) return Api.Conflict(ErrorCodes.AlreadyFriends);
            if (existing is not null && existing.RequesterId == me) return Results.NoContent();
            if (await FriendCount(db, me, ct) >= game.Value.MaxFriends || await FriendCount(db, target.Id, ct) >= game.Value.MaxFriends)
                return Api.Conflict(ErrorCodes.FriendLimit);
            var card = await Players.SummaryAsync(db, me, ct);
            if (existing is not null)
            {
                // They already asked me: asking back is accepting.
                existing.Accepted = true;
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                live.Send(target.Id, MessageTypes.FriendAccepted, new FriendRequestResponse { Player = card, SentAt = clock.GetUtcNow() });
                return Results.NoContent();
            }
            if (!ProfileRules.ReadSettings(target.SettingsJson).FriendReq) return Api.Conflict(ErrorCodes.RequestsDisabled);
            db.Friendships.Add(new Friendship { LowId = low, HighId = high, RequesterId = me, CreatedAt = clock.GetUtcNow() });
            try { await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); }
            catch (DbUpdateException error) when (error.IsUniqueViolation()) { return Results.NoContent(); }
            live.Send(target.Id, MessageTypes.FriendRequest, new FriendRequestResponse { Player = card, SentAt = clock.GetUtcNow() });
            return Results.NoContent();
        });

        friends.MapPost("/requests/{id:guid}/accept", async (Guid id, HttpContext http, GameDb db, ConnectionRegistry live, TimeProvider clock,
            IOptions<GameOptions> game, CancellationToken ct) =>
        {
            var me = http.User.AccountId();
            var (low, high) = Players.Pair(me, id);
            // The limit holds on acceptance too (requests may have piled up while either list filled), counted under both
            // accounts' row locks so two parallel accepts cannot both pass it.
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await LockPairAsync(db, low, high, ct);
            if (await FriendCount(db, me, ct) >= game.Value.MaxFriends || await FriendCount(db, id, ct) >= game.Value.MaxFriends)
                return Api.Conflict(ErrorCodes.FriendLimit);
            var accepted = await db.Friendships.Where(f => f.LowId == low && f.HighId == high && !f.Accepted && f.RequesterId == id)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.Accepted, true), ct);
            if (accepted == 0) return Api.NotFound();
            await tx.CommitAsync(ct);
            live.Send(id, MessageTypes.FriendAccepted, new FriendRequestResponse { Player = await Players.SummaryAsync(db, me, ct), SentAt = clock.GetUtcNow() });
            return Results.NoContent();
        });

        // Declines an incoming request or withdraws an outgoing one.
        friends.MapPost("/requests/{id:guid}/decline", async (Guid id, HttpContext http, GameDb db, CancellationToken ct) =>
        {
            var (low, high) = Players.Pair(http.User.AccountId(), id);
            var removed = await db.Friendships.Where(f => f.LowId == low && f.HighId == high && !f.Accepted).ExecuteDeleteAsync(ct);
            return removed == 0 ? Api.NotFound() : Results.NoContent();
        });

        friends.MapDelete("/{id:guid}", async (Guid id, HttpContext http, GameDb db, CancellationToken ct) =>
        {
            var (low, high) = Players.Pair(http.User.AccountId(), id);
            var removed = await db.Friendships.Where(f => f.LowId == low && f.HighId == high && f.Accepted).ExecuteDeleteAsync(ct);
            return removed == 0 ? Api.NotFound() : Results.NoContent();
        });

        // Code lookup is exact; name lookup is a short prefix search over display names.
        app.MapGet("/v1/players/search", async (string? q, HttpContext http, GameDb db, CancellationToken ct) =>
        {
            var me = http.User.AccountId();
            var text = (q ?? "").Trim();
            List<Guid> ids;
            if (Players.NormalizeCode(text) is { } code)
                ids = await db.Accounts.Where(a => a.FriendCode == code).Select(a => a.Id).ToListAsync(ct);
            else if (text.Length >= 3 && text.Length <= 16)
            {
                var pattern = text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
                ids = await db.Accounts.Where(a => a.Id != me && EF.Functions.ILike(a.Name, pattern)).OrderBy(a => a.Name).Take(10).Select(a => a.Id).ToListAsync(ct);
            }
            else return Api.Error(ErrorCodes.Validation);
            var cards = await Players.SummariesAsync(db, ids, ct);
            var links = await db.Friendships.AsNoTracking().Where(f => (f.LowId == me && ids.Contains(f.HighId)) || (f.HighId == me && ids.Contains(f.LowId))).ToListAsync(ct);
            return Results.Ok(ids.Where(cards.ContainsKey).Select(id =>
            {
                var link = links.FirstOrDefault(f => f.LowId == id || f.HighId == id);
                return new PlayerSearchResponse
                {
                    Player = cards[id], IsFriend = link is { Accepted: true },
                    RequestSent = link is { Accepted: false } && link.RequesterId == me,
                    RequestReceived = link is { Accepted: false } && link.RequesterId == id,
                };
            }).ToList());
        }).RequireAuthorization();

        app.MapGet("/v1/share", async (HttpContext http, GameDb db, IOptions<GameOptions> game, CancellationToken ct) =>
        {
            var code = await db.Accounts.Where(a => a.Id == http.User.AccountId()).Select(a => a.FriendCode).FirstAsync(ct);
            return Results.Ok(ShareLink(game.Value, code));
        }).RequireAuthorization();

        // Public landing page behind shared links: opens the game on the friend card, or shows the code to type in.
        app.MapGet("/p/{code}", async (string code, GameDb db, IOptions<GameOptions> game, CancellationToken ct) =>
        {
            var normalized = Players.NormalizeCode(code);
            var name = normalized is null ? null : await db.Accounts.Where(a => a.FriendCode == normalized).Select(a => a.Name).FirstOrDefaultAsync(ct);
            if (name is null) return Results.NotFound();
            var link = ShareLink(game.Value, normalized!);
            Func<string?, string?> e = System.Net.WebUtility.HtmlEncode;
            return Results.Content($"""
                <!doctype html><html lang="tr"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
                <title>{e(name)} · Galaxy 101</title><meta property="og:title" content="{e(name)} seni Galaxy 101'e çağırıyor">
                <meta property="og:description" content="Arkadaş kodu: {e(normalized!)}"></head>
                <body style="font-family:system-ui;background:#0A0C28;color:#fff;text-align:center;padding:48px 16px">
                <h1>{e(name)}</h1><p>Galaxy 101 arkadaş kodu</p><p style="font-size:32px;letter-spacing:4px">{e(normalized!)}</p>
                <p><a style="color:#FFD964" href="{e(link.DeepLink)}">Oyunda aç</a></p></body></html>
                """, "text/html; charset=utf-8");
        });
    }

    public static ShareLinkResponse ShareLink(GameOptions game, string code) => new()
    {
        FriendCode = code, Url = $"{game.PublicBaseUrl.TrimEnd('/')}/p/{code}", DeepLink = $"{game.DeepLinkScheme}://friend/{code}",
    };

    /// <summary>Row locks on both accounts, lowest id first (every caller locks in the same order: no deadlock).</summary>
    private static Task<int> LockPairAsync(GameDb db, Guid low, Guid high, CancellationToken ct) =>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT 1 FROM accounts WHERE id IN ({low}, {high}) ORDER BY id FOR UPDATE", ct);

    private static Task<int> FriendCount(GameDb db, Guid account, CancellationToken ct) =>
        db.Friendships.CountAsync(f => f.Accepted && (f.LowId == account || f.HighId == account), ct);
}
