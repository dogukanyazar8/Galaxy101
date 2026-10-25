using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Galaxy101.Contracts;
using Galaxy101.Online;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Galaxy101
{
    /// <summary>
    /// The menu's server data while signed in: fetched once after sign-in and again whenever its panel opens, kept in the
    /// shapes the screens read (those of the design's sample content), and the actions that change it. Signed out
    /// (developer tools, tests, captures) the sample services answer instead.
    /// </summary>
    public sealed class OnlineMenu
    {
        private static readonly string[] QuestIds = { "winHands", "pairsOpenings", "handsPlayed", "okeyFinishes", "tilesAdded" };
        private static readonly string[] Modes = { GameModes.Solo, GameModes.Pairs, GameModes.Doubling, GameModes.Ranked, "bots", GameModes.Private };

        private readonly MenuModel model;
        private readonly JObject sample;
        private readonly Dictionary<string, JObject> boards = new Dictionary<string, JObject>();
        private long settingsRevision = -1;

        public OnlineMenu(MenuModel model, JObject sample) { this.model = model; this.sample = sample; }

        public static GalaxyOnline Session => GalaxyOnline.Instance != null && GalaxyOnline.Instance.SignedIn ? GalaxyOnline.Instance : null;
        public bool Active => Session != null;
        /// <summary>The outcome of an action, for the menu to show: Turkish, English and the flash kind ("info" / "bad").</summary>
        public event Action<string, string, string> Notice;
        public void Tell(string tr, string en, string kind) => Notice?.Invoke(tr, en, kind);

        // Nothing loaded yet reads as empty, never as the design's sample figures.
        public JObject Stats { get; private set; }
        public JArray Badges { get; private set; } = new JArray();
        public JArray History { get; private set; } = new JArray();
        public JArray Streak { get; private set; } = new JArray();
        public JArray Quests { get; private set; } = new JArray();
        public bool CanClaimDay { get; private set; }
        public DateTime ResetTime { get; private set; } = DateTime.Today.AddDays(1);
        public JArray Friends { get; private set; } = new JArray();
        public JArray Requests { get; private set; } = new JArray();
        public CatalogResponse Catalog { get; private set; }
        public string ShareUrl { get; private set; }
        public JObject Board(string scope) => boards.TryGetValue(scope, out var board) ? board : new JObject { ["podium"] = new JArray(), ["rows"] = new JArray(), ["me"] = Row(null, 0) };

        /// <summary>Fetches what <paramref name="panel"/> shows (null: everything the main menu and its badges need).</summary>
        public void Load(string panel) => _ = LoadAsync(panel);

        private async Task LoadAsync(string panel)
        {
            var session = Session; if (session == null) return;
            try
            {
                switch (panel)
                {
                    case null: await Task.WhenAll(LoadProfile(session), LoadDaily(session), LoadFriends(session), LoadSettings(session)); break;
                    case "me": await LoadProfile(session); break;
                    case "daily": await LoadDaily(session); break;
                    case "friends": case "add": await LoadFriends(session); break;
                    case "leader": await Task.WhenAll(LoadBoard(session, "weekly"), LoadBoard(session, "season"), LoadBoard(session, "friends")); break;
                    case "shop": await LoadShop(session); break;
                    case "share": await LoadShare(session); break;
                    default: return;
                }
                if (Session == session) model.NotifyChanged();
            }
            catch (ApiException e) { if (!e.Offline) Debug.LogWarning("Menu data (" + (panel ?? "start") + ") not loaded: " + e.Code); }
            catch (Exception e) { Debug.LogException(e); } // fire-and-forget: nothing else would see it
        }

        // ---------- profile ----------
        private async Task LoadProfile(GalaxyOnline session)
        {
            var stats = session.Api.GetAsync<StatsResponse>("/v1/profile/stats");
            var badges = session.Api.GetAsync<List<BadgeResponse>>("/v1/profile/badges");
            var history = session.Api.GetAsync<List<HistoryEntryResponse>>("/v1/profile/history");
            await session.RefreshAsync(); // the level, XP and LP on the card
            var s = await stats; var progress = session.Profile?.Progress ?? new ProgressResponse { Level = 1, NextLevelXp = 375, League = "b3", NextLeague = "b2" };
            var league = progress.League ?? "b3";
            Stats = new JObject
            {
                ["hands"] = s.Hands, ["wins"] = s.Wins, ["winRate"] = s.WinRate, ["bestOpening"] = s.BestOpening, ["okeyFinish"] = s.OkeyFinishes,
                ["level"] = progress.Level, ["xp"] = progress.LevelXp, ["nextXp"] = Math.Max(1, progress.NextLevelXp),
                // The bar is the division: 0–99 of 100 LP (Usta I and Galaksi: the LP above it, no next division).
                ["lp"] = progress.DivisionLp, ["nextLp"] = progress.NextLeague == null ? Math.Max(1, progress.DivisionLp) : 100, ["league"] = league,
                ["nextLeague"] = progress.NextLeague, ["bestLeague"] = progress.BestLeague ?? league, ["season"] = progress.Season, ["seasonRank"] = progress.SeasonRank,
                // The v3 rows: Klasik (every unranked table), Dereceli, Botlara karşı (offline, not counted here).
                ["modeWinRates"] = new JArray(s.WinRate, s.ModeWinRates.Count > 3 ? s.ModeWinRates[3] : 0, 0, 0),
            };
            // The server lists the badges in the reference panel's order; the art and names are the panel's.
            var art = (JArray)sample["badges"]; var list = await badges; var mapped = new JArray();
            for (int i = 0; i < art.Count && i < list.Count; i++)
            {
                var badge = (JObject)art[i].DeepClone(); var b = list[i];
                badge["locked"] = !b.Unlocked; badge["cur"] = b.Unlocked ? 0 : b.Current; badge["max"] = b.Unlocked ? 0 : b.Target;
                mapped.Add(badge);
            }
            Badges = mapped;
            var now = DateTimeOffset.UtcNow;
            History = new JArray((await history).Select(h => new JObject
            {
                ["mode"] = Math.Max(0, Array.IndexOf(Modes, h.Mode)), ["place"] = h.Place, ["delta"] = h.ChipDelta,
                ["minutes"] = (int)Math.Max(0, (now - h.FinishedAt).TotalMinutes), ["stake"] = h.Stake,
                ["lp"] = h.LpDelta is int lp ? (lp > 0 ? "+" : "") + lp + " LP" : null,
            }));
        }

        // ---------- daily rewards ----------
        private async Task LoadDaily(GalaxyOnline session) => ApplyDaily(session, await session.Api.GetAsync<DailyResponse>("/v1/daily"));

        private void ApplyDaily(GalaxyOnline session, DailyResponse daily)
        {
            Streak = new JArray(daily.Streak.Select(d => new JObject { ["day"] = d.Day, ["chips"] = d.Chips, ["today"] = d.Today, ["claimed"] = d.Claimed }));
            CanClaimDay = daily.CanClaimStreak;
            var names = (JArray)model.Content["dailyQuests"];
            Quests = new JArray(daily.Quests.Select(q =>
            {
                int i = Array.IndexOf(QuestIds, q.Id); var name = i >= 0 && i < names.Count ? names[i] : null;
                return new JObject
                {
                    ["id"] = q.Id, ["tr"] = (string)name?["tr"] ?? q.Id, ["en"] = (string)name?["en"] ?? q.Id, ["cur"] = q.Progress, ["max"] = q.Target,
                    ["rw"] = q.Chips, ["gem"] = q.Gems, ["claimed"] = q.Claimed, ["complete"] = q.Complete,
                };
            }));
            ResetTime = daily.NextReset.LocalDateTime;
            if (daily.Wallet != null) session.Apply(daily.Wallet);
        }

        public async Task<string> ClaimDayAsync() => await Daily("/v1/daily/claim", "Günlük ödül toplandı", "Daily reward collected");

        public async Task<string> ClaimQuestAsync(int index)
        {
            var id = index >= 0 && index < Quests.Count ? (string)Quests[index]["id"] : null;
            if (id == null) return ErrorCodes.NotFound;
            return await Daily("/v1/daily/quests/" + id + "/claim", "Görev ödülü alındı", "Quest reward claimed");
        }

        private async Task<string> Daily(string path, string tr, string en)
        {
            var session = Session; if (session == null) return "offline";
            try { ApplyDaily(session, await session.Api.PostAsync<DailyResponse>(path, null)); }
            catch (ApiException e) { await Reload(session, LoadDaily); return Fail(e); }
            model.NotifyChanged(); Notice?.Invoke(tr, en, "info");
            return null;
        }

        // ---------- shop ----------
        private async Task LoadShop(GalaxyOnline session) { Catalog = await session.Api.GetAsync<CatalogResponse>("/v1/shop/catalog"); await session.RefreshAsync(); }

        /// <summary>A gem-priced item is bought on the server; real-money packs need the platform store, which is not open yet.</summary>
        public async Task<string> PurchaseAsync(string id)
        {
            var session = Session; if (session == null) return "offline";
            if (id == null || !id.StartsWith("skin-", StringComparison.Ordinal)) return ErrorCodes.StoreUnavailable;
            try
            {
                var bought = await session.Api.PostAsync<PurchaseResponse>("/v1/shop/purchase", new GemPurchaseRequest { OfferId = id });
                if (bought?.Wallet != null) session.Apply(bought.Wallet);
            }
            catch (ApiException e) { return e.Offline ? "offline" : e.Code; }
            try { await LoadShop(session); } catch (ApiException) { } // bought; the catalog catches up on the next opening
            model.NotifyChanged();
            return null;
        }

        // ---------- friends ----------
        private async Task LoadFriends(GalaxyOnline session)
        {
            var list = await session.Api.GetAsync<FriendsResponse>("/v1/friends");
            Friends = new JArray(list.Friends.Select(Friend));
            Requests = new JArray(list.Incoming.Select(r => Person(r.Player, new JArray("Sana istek gönderdi", "Sent you a request"))));
            foreach (var sent in list.Outgoing) if (!string.IsNullOrEmpty(sent.Player?.FriendCode)) model.SentRequests.Add(sent.Player.FriendCode);
        }

        private static JObject Friend(FriendResponse f)
        {
            bool playing = f.Presence == "playing", on = f.Presence == "online" || playing;
            var status = playing ? new JArray("Masada", "At a table") : on ? new JArray("Çevrimiçi", "Online") : new JArray("Çevrimdışı", "Offline");
            var row = Person(f.Player, status);
            row["on"] = on; row["sc"] = playing ? "var(--a1)" : "#8EF0B0"; row["act"] = playing ? "watch" : on ? "invite" : "msg";
            return row;
        }

        private static JObject Person(PlayerSummary p, JArray status) => new JObject
        {
            ["id"] = p.Id.ToString(), ["name"] = p.Name, ["ini"] = ProfileValidation.Initials(p.Name ?? ""), ["c"] = p.Color, ["lv"] = Math.Max(1, p.Level),
            ["code"] = p.FriendCode, ["frame"] = p.Frame, ["on"] = false, ["st"] = status,
        };

        public async Task<JObject> SearchAsync(string query)
        {
            var session = Session; if (session == null || string.IsNullOrWhiteSpace(query)) return null;
            List<PlayerSearchResponse> found;
            try { found = await session.Api.GetAsync<List<PlayerSearchResponse>>("/v1/players/search?q=" + Uri.EscapeDataString(query.Trim())); }
            catch (ApiException e) { if (e.Offline) Fail(e); return null; } // a too short name is simply "not found"
            var hit = found?.FirstOrDefault(); if (hit == null) return null;
            var result = Person(hit.Player, null);
            result["isFriend"] = hit.IsFriend; result["sent"] = hit.RequestSent;
            if (hit.RequestSent) model.SentRequests.Add(hit.Player.FriendCode);
            return result;
        }

        public Task<string> SendRequestAsync(string code) => Friendly(async s =>
        {
            await s.Api.PostAsync<JObject>("/v1/friends/requests", new FriendCodeRequest { Code = code });
            model.SentRequests.Add(code);
        }, "İstek gönderildi", "Request sent");

        public Task<string> AcceptAsync(string id) => Friendly(s => s.Api.PostAsync<JObject>("/v1/friends/requests/" + id + "/accept", null), "Artık arkadaşsınız", "You are now friends");
        public Task<string> DeclineAsync(string id) => Friendly(s => s.Api.PostAsync<JObject>("/v1/friends/requests/" + id + "/decline", null), null, null);

        private async Task<string> Friendly(Func<GalaxyOnline, Task> action, string tr, string en)
        {
            var session = Session; if (session == null) return "offline";
            try { await action(session); }
            catch (ApiException e) { await Reload(session, LoadFriends); return Fail(e); }
            await Reload(session, LoadFriends);
            model.NotifyChanged();
            if (tr != null) Notice?.Invoke(tr, en, "info");
            return null;
        }

        // ---------- leaderboard ----------
        private async Task LoadBoard(GalaxyOnline session, string scope)
        {
            var board = await session.Api.GetAsync<LeaderboardResponse>("/v1/leaderboard/" + scope);
            var rows = board.Rows.Select(r => Row(r.Player, r.Points, r.Rank, r.Me)).ToList();
            boards[scope] = new JObject
            {
                ["podium"] = new JArray(rows.Take(3)), ["rows"] = new JArray(rows.Skip(3)),
                ["me"] = board.Me != null ? Row(board.Me.Player, board.Me.Points, board.Me.Rank, true) : Row(null, 0),
            };
        }

        private JObject Row(PlayerSummary p, long points, int rank = 0, bool me = true)
        {
            string name = p?.Name ?? (string)model.Profile["name"];
            return new JObject
            {
                ["rank"] = rank, ["name"] = name, ["ini"] = ProfileValidation.Initials(name ?? ""), ["c"] = p?.Color ?? (string)model.Profile["color"],
                ["points"] = points, ["league"] = p?.League ?? "b2", ["me"] = me,
            };
        }

        // ---------- settings ----------
        private async Task LoadSettings(GalaxyOnline session)
        {
            var saved = await session.Api.GetAsync<SettingsResponse>("/v1/settings");
            settingsRevision = saved.Revision;
            if (saved.Revision == 0) { await PushSettings(session); return; } // never saved on the account: this device's choice
            var next = (JObject)model.Settings.DeepClone();
            foreach (var p in JObject.FromObject(saved.Settings, Newtonsoft.Json.JsonSerializer.Create(GalaxyApi.Json)).Properties())
                if (next[p.Name] != null) next[p.Name] = p.Value;
            if (!JToken.DeepEquals(next, model.Settings) && SettingsValidation.IsValid(next, model.DefaultSettings()))
            {
                var previous = model.Settings; model.Settings = next;
                if (!model.Persist()) model.Settings = previous;
            }
            if (!string.IsNullOrEmpty(saved.Planet) && model.Themes[saved.Planet] != null && saved.Planet != model.Planet)
            {
                var previous = model.Planet; model.Planet = saved.Planet;
                if (!model.Persist()) model.Planet = previous;
            }
        }

        /// <summary>The account keeps the game settings (display mode, resolution and frame rate stay per device).</summary>
        public void SaveSettings() { var session = Session; if (session != null) _ = PushSettingsSafe(session); }

        private async Task PushSettingsSafe(GalaxyOnline session)
        {
            try { await PushSettings(session); }
            catch (ApiException e) { if (!e.Offline) Debug.LogWarning("Settings not saved on the server: " + e.Code); }
        }

        private async Task PushSettings(GalaxyOnline session)
        {
            for (int attempt = 0; attempt < 2; attempt++)
            {
                if (settingsRevision < 0) settingsRevision = (await session.Api.GetAsync<SettingsResponse>("/v1/settings")).Revision;
                var dto = model.Settings.ToObject<SettingsDto>(Newtonsoft.Json.JsonSerializer.Create(GalaxyApi.Json));
                try { settingsRevision = (await session.Api.PutAsync<SettingsResponse>("/v1/settings", new SettingsUpdateRequest { Settings = dto, Revision = settingsRevision })).Revision; return; }
                catch (ApiException e) when (e.Status == 409) { settingsRevision = -1; } // another device saved: this explicit save wins
            }
        }

        public void SavePlanet(string planet)
        {
            var session = Session; if (session == null) return;
            _ = Send(session, () => session.Api.PutAsync<JObject>("/v1/settings/planet", new PlanetUpdateRequest { Planet = planet }));
        }

        private static async Task Send(GalaxyOnline session, Func<Task> request)
        {
            try { await request(); }
            catch (ApiException e) { if (!e.Offline) Debug.LogWarning("Menu change not saved on the server: " + e.Code); }
        }

        // ---------- share ----------
        private async Task LoadShare(GalaxyOnline session) => ShareUrl = (await session.Api.GetAsync<ShareLinkResponse>("/v1/share"))?.Url;

        // ---------- helpers ----------
        private static async Task Reload(GalaxyOnline session, Func<GalaxyOnline, Task> load)
        {
            try { await load(session); } catch (ApiException) { }
        }

        private string Fail(ApiException e)
        {
            var code = e.Offline ? "offline" : e.Code;
            var (tr, en) = Describe(code);
            Notice?.Invoke(tr, en, "bad");
            model.NotifyChanged();
            return code;
        }

        public static (string Tr, string En) Describe(string code)
        {
            switch (code)
            {
                case "offline": return ("Sunucuya ulaşılamadı", "Server unreachable");
                case ErrorCodes.InsufficientFunds: return ("Yetersiz bakiye", "Not enough balance");
                case ErrorCodes.AlreadyOwned: return ("Zaten sahipsin", "Already owned");
                case ErrorCodes.AlreadyClaimed: return ("Zaten alındı", "Already claimed");
                case ErrorCodes.NotComplete: return ("Görev henüz tamamlanmadı", "The quest is not complete yet");
                case ErrorCodes.StoreUnavailable: return ("Ödeme ile satın alma yakında", "Paid purchases are coming soon");
                case ErrorCodes.AlreadyFriends: return ("Zaten arkadaşsınız", "You are already friends");
                case ErrorCodes.RequestsDisabled: return ("Bu oyuncu arkadaşlık isteği almıyor", "This player does not take friend requests");
                case ErrorCodes.FriendLimit: return ("Arkadaş sınırına ulaşıldı", "Friend limit reached");
                case ErrorCodes.NotFound: return ("Bulunamadı", "Not found");
                case ErrorCodes.RateLimited: return ("Biraz bekle", "Wait a moment");
                default: return ("İşlem yapılamadı. Tekrar dene.", "Could not complete. Try again.");
            }
        }
    }
}
