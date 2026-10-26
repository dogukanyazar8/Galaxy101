using Galaxy101.Contracts;
using Galaxy101.Server.Realtime;
using Galaxy101.Server.Games;
using Galaxy101.Table;

namespace Galaxy101.Server.V4;

/// <summary>
/// Dereceli (R-94, spec/27): a real queue of parties — solo (4 × 1), duo (2 × 2, seats 0+2 and 1+3: D-29) and squad (a full
/// party of 4). A table forms from parties within ±1 league of each other, ±2 once the oldest has waited rankedWiden (30 s);
/// everyone accepts within rankedAccept (10 s), then "Masa hazırlanıyor" (rankedLoading 1.8 s) and the match. The package's
/// bot opponents and their timed accepts (DEMO-09) are not part of the real server.
/// </summary>
public sealed partial class Hub
{
    private sealed class RankedEntry
    {
        public required string Mode { get; init; }
        public required List<Guid> Members { get; init; }
        public long Since { get; set; }
        public FoundMatch? Found { get; set; }
        public object? Widen { get; set; }
    }

    private sealed class FoundMatch
    {
        public required string Id { get; init; }
        public required List<RankedEntry> Entries { get; init; }
        /// <summary>Players in seat order (duo: 0, 2, 1, 3 → teams).</summary>
        public required List<Guid> Players { get; init; }
        public bool[] Accepted { get; } = new bool[4];
        public long ExpiresAt { get; init; }
        public object? Timer { get; set; }
    }

    private readonly List<RankedEntry> queue = [];
    private long foundSeq;

    private int League(Guid account) => (people.TryGetValue(account, out var p) ? p.Card?.RankIdx ?? 0 : 0) / 3;

    private V4Queue QueuePub(RankedEntry q) => new()
    {
        Mode = q.Mode, Since = q.Since, Range = now - q.Since >= TableTimings.RankedWiden ? 2 : 1,
        Filled = q.Found is null ? q.Members.Count : 4, Found = q.Found is null ? null : FoundPub(q.Found),
    };

    private V4RankedFound FoundPub(FoundMatch f) => new()
    {
        MatchId = f.Id, Players = [.. f.Players.Select(id => new V4RankedPlayer { User = Pub(P(id)), Bot = false })], ExpiresAt = f.ExpiresAt, Accepted = [.. f.Accepted],
    };

    private void ToEntry(RankedEntry q, string type, object payload) { foreach (var m in q.Members) Send(m, type, payload); }

    private void SearchEvent(RankedEntry q)
    {
        var pub = QueuePub(q);
        ToEntry(q, V4Messages.RankedSearch, new V4RankedSearch { Mode = pub.Mode, Since = pub.Since, Range = pub.Range, Filled = pub.Filled, Found = pub.Found, At = now });
    }

    private async Task<Reply> RankedQueueAsync(Person p, V4RankedQueueRequest m)
    {
        if (p.Queue is not null) return Reply.Fail(V4Errors.E_ALREADY_QUEUED);
        if (Busy(p)) return Reply.Fail(V4Errors.E_ALREADY_SEATED);
        if (m.Mode is not ("solo" or "duo" or "squad")) return Reply.Fail(V4Errors.E_MODE);
        await CardAsync(p, refresh: true); // the league may have changed since the card was read (a match, the season)
        var party = new List<Person>();
        foreach (var id in (m.Party ?? []).Distinct())
            if (Guid.TryParse(id, out var g) && g != p.Id && await store.ExistsAsync(g) && !Busy(P(g))) party.Add(P(g));
        var size = m.Mode switch { "duo" => 1, "squad" => 3, _ => 0 };
        if (party.Count != size || (m.Party?.Count ?? 0) != size) return Reply.Fail(V4Errors.E_PARTY_SIZE);
        foreach (var member in party)
        {
            await CardAsync(member, refresh: true);
            // ±1 league (three divisions) between party members (mockServer rankedQueue).
            if (Math.Abs((member.Card?.RankIdx ?? 0) - (p.Card?.RankIdx ?? 0)) > 3) return Reply.Fail(V4Errors.E_PARTY_RANGE);
        }
        var members = new List<Guid> { p.Id };
        members.AddRange(party.Select(x => x.Id));
        foreach (var id in members) lobby.TryEnter(id, LobbyPlace.Queue);
        var q = new RankedEntry { Mode = m.Mode, Members = members, Since = now };
        foreach (var id in members) P(id).Queue = q;
        queue.Add(q);
        SearchEvent(q);
        q.Widen = Schedule(q.Since + TableTimings.RankedWiden, () => { if (queue.Contains(q) && q.Found is null) { SearchEvent(q); TryMatch(); } });
        TryMatch();
        return Reply.Ok;
    }

    /// <summary>Forms a table from waiting parties of one mode: the oldest first, leagues within its range.</summary>
    private void TryMatch()
    {
        foreach (var anchor in queue.Where(q => q.Found is null).OrderBy(q => q.Since).ToList())
        {
            if (anchor.Found is not null) continue;
            var range = now - anchor.Since >= TableTimings.RankedWiden ? 2 : 1;
            var group = new List<RankedEntry> { anchor };
            foreach (var q in queue.Where(q => q.Found is null && q != anchor && q.Mode == anchor.Mode).OrderBy(q => q.Since))
            {
                if (group.Sum(g => g.Members.Count) + q.Members.Count > 4) continue;
                var leagues = group.Append(q).SelectMany(g => g.Members).Select(League).ToList();
                if (leagues.Max() - leagues.Min() <= range) group.Add(q);
                if (group.Sum(g => g.Members.Count) == 4) break;
            }
            if (group.Sum(g => g.Members.Count) != 4) continue;
            var players = group.SelectMany(g => g.Members).ToList();
            var order = anchor.Mode == "duo" ? new[] { 0, 2, 1, 3 } : new[] { 0, 1, 2, 3 };
            var seated = new Guid[4];
            for (var i = 0; i < 4; i++) seated[order[i]] = players[i];
            var found = new FoundMatch { Id = "r" + (++foundSeq), Entries = group, Players = [.. seated], ExpiresAt = now + TableTimings.RankedAccept };
            foreach (var g in group) { g.Found = found; Cancel(g.Widen); }
            foreach (var g in group) ToEntry(g, V4Messages.RankedFound, new V4RankedFoundEvent { Found = FoundPub(found), At = now });
            found.Timer = Schedule(found.ExpiresAt, () => AcceptTimeout(found));
        }
    }

    private void AcceptTimeout(FoundMatch f)
    {
        if (f.Entries.All(g => g.Found != f) || f.Accepted.All(a => a)) return;
        foreach (var g in f.Entries)
        {
            g.Found = null;
            if (g.Members.All(id => f.Accepted[f.Players.IndexOf(id)])) { g.Since = now; SearchEvent(g); Rewiden(g); }
            else Unqueue(g, "timeout");
        }
        TryMatch();
    }

    private void Rewiden(RankedEntry q) =>
        q.Widen = Schedule(q.Since + TableTimings.RankedWiden, () => { if (queue.Contains(q) && q.Found is null) { SearchEvent(q); TryMatch(); } });

    private void Unqueue(RankedEntry q, string? reason)
    {
        queue.Remove(q);
        Cancel(q.Widen);
        foreach (var id in q.Members)
        {
            var p = P(id);
            if (p.Queue == q) p.Queue = null;
            lobby.Exit(id, LobbyPlace.Queue);
        }
        if (reason is not null) ToEntry(q, V4Messages.RankedCancelled, new V4RankedCancelled { Reason = reason, At = now });
    }

    /// <summary>The other parties of a found match that fell through go back to searching.</summary>
    private void Release(FoundMatch f, RankedEntry except)
    {
        Cancel(f.Timer);
        foreach (var g in f.Entries.Where(g => g != except && g.Found == f)) { g.Found = null; g.Since = now; SearchEvent(g); Rewiden(g); }
    }

    private async Task<Reply> RankedAcceptAsync(Person p, V4RankedAcceptRequest m)
    {
        if (p.Queue is not { Found: { } f } q || f.Id != m.MatchId) return Reply.Fail(V4Errors.E_MATCH_EXPIRED);
        if (!m.Accept)
        {
            Unqueue(q, "declined");
            q.Found = null;
            Release(f, q);
            TryMatch();
            return Reply.Ok;
        }
        var index = f.Players.IndexOf(p.Id);
        f.Accepted[index] = true;
        foreach (var g in f.Entries) ToEntry(g, V4Messages.RankedAccepted, new V4RankedAccepted { Index = index, Accepted = [.. f.Accepted], At = now });
        if (f.Accepted.All(a => a)) await RankedReadyAsync(f);
        return Reply.Ok;
    }

    private Reply RankedCancel(Person p)
    {
        if (p.Queue is not { } q) return Reply.Fail(V4Errors.E_NOT_QUEUED);
        var f = q.Found;
        Unqueue(q, "cancel");
        q.Found = null;
        if (f is not null) { Release(f, q); TryMatch(); }
        return Reply.Ok;
    }

    /// <summary>Everyone accepted: the ranked table (no stake, no katlama, 5 hands, 30 s: R-94), "Masa hazırlanıyor", then the match.</summary>
    private async Task RankedReadyAsync(FoundMatch f)
    {
        Cancel(f.Timer);
        foreach (var g in f.Entries) Unqueue(g, null);
        var mode = f.Entries[0].Mode == "duo" ? "pairs" : "solo";
        var t = NewTable(f.Players[0], new V4TableCreateRequest
        {
            Name = "Dereceli", Mode = mode, Bet = 0, Rules = new V4TableRules { Spectate = false, Chat = "quick", Gifts = false },
        }, ranked: true, hands: TableEconomy.RankedHands, turnSec: TableEconomy.RankedTurnSec);
        await RecordAsync(t);
        for (var k = 0; k < 4; k++)
        {
            var p = P(f.Players[k]);
            lobby.TryEnter(p.Id, LobbyPlace.Room);
            t.Seats[k] = new Occupant { Account = p.Id };
            p.Table = t; p.Seat = k;
        }
        TableEmit(t, V4Messages.RankedReady, new V4RankedReady { Table = TablePub(t), LoadingMs = TableTimings.RankedLoading, At = now });
        t.Timers["load"] = Schedule(now + TableTimings.RankedLoading, () => StartMatchAsync(t));
    }
}
