using Galaxy101.Contracts;
using Galaxy101.Server.Realtime;
using Galaxy101.Server.Economy;
using Galaxy101.Server.Games;
using Galaxy101.Table;
using Okey101.Core.V4;

namespace Galaxy101.Server.V4;

public sealed partial class Hub
{
    /// <summary>The countdown (or the ranked loading) is over: the match runs on its own <see cref="TableRuntime"/>.</summary>
    private async Task StartMatchAsync(Table t)
    {
        if (t.Closed || t.Run is not null) return;
        t.Status = "playing";
        var bots = Enumerable.Range(0, 4).Select(k => t.Seats[k] is not { IsBot: false }).ToArray();
        var setup = new TableSetup
        {
            Options = new MatchOptions
            {
                Mode = t.Mode, Hands = t.Hands, Penalties = t.Rules.Penalties, Isle = t.Rules.Isle, Katlama = t.Katlama, KatlamaPartner = t.KatlamaPartner,
            },
            Seed = SeedSource(), TurnMs = t.TurnSec * 1000, Chat = t.Rules.Chat, FirstHandRig = NextRig,
        };
        NextRig = null;
        t.Run = new TableRuntime(t.Host, setup, bots, t.ChatRng);
        var humans = t.HumanSeats.Select(k => t.Seats[k]!.Account!.Value).ToList();
        foreach (var account in humans) live.SetPlaying(account, t.Id);
        t.Run.Start(TablePub(t));
        LobbyEmit(t);
        try
        {
            t.Blocks = await store.BlocksAsync(humans);
            await store.SeatsAsync(t.Id, SeatIds(t), BotMask(t), -1);
        }
        catch (Exception error) when (error is not OperationCanceledException) { log.LogWarning(error, "v4 table {Table}: seats not stored", t.Id); }
    }

    private static Guid[] SeatIds(Table t) => [.. Enumerable.Range(0, 4).Select(k => t.Seats[k] is { Account: { } a } ? a : Guid.Empty)];
    private static int BotMask(Table t) => Enumerable.Range(0, 4).Where(k => t.Seats[k] is not { IsBot: false }).Aggregate(0, (m, k) => m | 1 << k);

    /// <summary>Engine events of a table: quest counters, and the hand XP (R-93, D-42) written at once with its xp event.</summary>
    private void Applied(Table t, IReadOnlyList<GameEvent> events)
    {
        foreach (var e in events)
        {
            switch (e.E)
            {
                case "opened" when e.Str("mode") == G101Rules.KindPair:
                    t.Books[e.Int("seat")].PairsOpenings++;
                    break;
                case "added":
                    t.Books[e.Int("seat")].TilesAdded++;
                    break;
                case "handEnd":
                    var result = (Dictionary<string, object>)e["result"];
                    var finisher = Convert.ToInt32(result["finisher"]);
                    var okeyFinish = result["okeyFinish"] is true;
                    foreach (var k in t.HumanSeats)
                    {
                        var book = t.Books[k];
                        book.HandsPlayed++;
                        book.HandWins.Add(finisher == k);
                        if (finisher == k) { book.WinHands++; if (okeyFinish) book.OkeyFinishes++; }
                        // R-93: the finisher 340, everyone else 60 (deck out: everyone 60); D-42: against bots and ranked too.
                        long gained = finisher == k ? options.XpHandWin : options.XpHandPlayed;
                        book.HandXp += gained;
                        var account = t.Seats[k]!.Account!.Value;
                        Defer(() => XpAsync(account, gained, "hand"));
                    }
                    break;
            }
        }
    }

    private async Task XpAsync(Guid account, long gained, string reason)
    {
        if (gained <= 0) return;
        var line = await store.AddXpAsync(account, gained);
        if (line is not { } x) return;
        var p = P(account);
        if (p.Card is { } c) p.Card = c with { Xp = x.Xp, XpNext = x.Next, Level = x.Level };
        Send(account, V4Messages.Xp, new V4Xp { Gained = gained, Xp = x.Xp, XpNext = x.Next, Level = x.Level, LevelUp = x.LevelUp, Reason = reason, At = now });
    }

    /// <summary>
    /// The match is over: settle (R-91 pool, D-12 split, D-40/D-41 shared places, R-93 XP bonus, R-95/D-37 weekly, ranked LP),
    /// store it all or nothing (retried on a storage hiccup), then matchResult, balances, XP and rank changes; the table closes
    /// and everyone returns to the lobby (Q-12).
    /// </summary>
    private async Task MatchOverAsync(Table t)
    {
        if (t.Closed || t.Run?.Match is not { } s) return;
        var person = Enumerable.Range(0, 4).Select(t.Human).ToArray();
        var accounts = Enumerable.Range(0, 4).Select(k => person[k] ? t.Seats[k]!.Account : null).ToArray();
        var seasonLp = new int[4];
        var shields = new int[4];
        if (t.Ranked)
            for (var k = 0; k < 4; k++)
                if (accounts[k] is { } a) (seasonLp[k], shields[k]) = await store.RankAsync(a);
        var pool = t.Ranked || t.VsBots ? 0 : t.BetsPaid;
        var shares = TableSettlement.Compute(s.Ranking, person, pool, t.Ranked ? k => Profile.Ranked.Table(seasonLp[k]) : null,
            t.VsBots || t.Private ? null : options.WeeklyPoints, options.XpPlace);
        var outcome = Enumerable.Range(0, 4).Select(k => new SeatSettlement(
            k, accounts[k], shares.Place(k), s.Totals[k], shares.Payouts[k], t.Seats[k]?.StakeKey is null ? 0 : t.Bet, shares.Lp[k], shares.Weekly[k],
            t.Books[k].HandXp + shares.XpBonus[k], person[k] ? t.Books[k].Counters : new QuestCounters(0, 0, 0, 0, 0), person[k] ? t.Books[k].HandWins : [], false)).ToList();
        try { await store.SettleAsync(t.Id, t.RecordMode, SeatIds(t), BotMask(t), outcome); }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            log.LogWarning(error, "v4 table {Table}: settlement not stored; retrying", t.Id);
            Schedule(now + 5000, () => MatchOverAsync(t));
            return;
        }
        for (var k = 0; k < 4; k++)
        {
            if (accounts[k] is not { } account) continue;
            var p = P(account);
            if (shares.Payouts[k] > 0)
            {
                var card = await store.CardAsync(account);
                if (card is not null) { p.Card = card; Balance(p, card.Chips, card.Gems, shares.Payouts[k], "win", t.Id.ToString()); }
            }
            if (shares.Lp[k] is { } lp) RankChange(p, seasonLp[k], shields[k], lp, shares.Place(k));
        }
        TableEmit(t, V4Messages.MatchResult, new V4MatchResult
        {
            Ranking = s.Ranking.Select(u => u.ToTree()).ToList(), Totals = [.. s.Totals], History = s.ToTree()["history"], Pool = pool, Rake = TableEconomy.Rake,
            Payouts = [.. shares.Payouts], XpBonus = [.. shares.XpBonus], Lp = [.. shares.Lp], Weekly = [.. shares.Weekly], At = now,
        });
        // R-93 place bonus (the leaver gets none: their seat is a bot now).
        for (var k = 0; k < 4; k++)
            if (accounts[k] is { } account && shares.XpBonus[k] > 0) await XpAsync(account, shares.XpBonus[k], "match");
        t.Status = "over";
        t.Closed = true;
        t.Run.Stop();
        LobbyEmit(t, removed: true);
        foreach (var account in accounts.OfType<Guid>())
        {
            var p = P(account);
            p.Table = null; p.Seat = -1;
            lobby.Exit(account, LobbyPlace.Room);
            live.SetPlaying(account, null);
        }
        foreach (var w in t.Spectators) if (people.TryGetValue(w, out var watcher)) watcher.Table = null;
        tables.Remove(t.Id);
    }

    /// <summary>rankChange (Dereceli LP, D-30 protection) from the season LP before the match and the delta.</summary>
    private void RankChange(Person p, int before, int shield, int delta, int place)
    {
        var (after, left) = Profile.Ranked.Apply(before, shield, delta);
        int idx(int lp) => Store.RankIdx(lp);
        var b = idx(before);
        var a = idx(after);
        Send(p.Id, V4Messages.RankChange, new V4RankChange
        {
            Before = new V4RankPoint { Idx = b, Lp = before - b * Profile.Ranked.DivisionLp }, After = new V4RankPoint { Idx = a, Lp = after - a * Profile.Ranked.DivisionLp },
            Delta = delta, Place = place, TierUp = a > b, LeagueUp = a / 3 > b / 3, Down = a < b, ProtectedLeft = left, At = now,
        });
    }
}
