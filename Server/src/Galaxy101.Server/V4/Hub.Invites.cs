using Galaxy101.Contracts;
using Galaxy101.Server.Realtime;
using Galaxy101.Table;

namespace Galaxy101.Server.V4;

public sealed partial class Hub
{
    private sealed class Invite
    {
        public required string Id { get; init; }
        public required Guid From { get; init; }
        public required Guid To { get; init; }
        public required Table Table { get; init; }
        public int Seat { get; init; }
        public long ExpiresAt { get; init; }
        public object? Timer { get; set; }
    }

    private readonly Dictionary<string, Invite> invites = [];
    private long inviteSeq;

    /// <summary>S14/S15 Davet: a friend (or a recent co-player) to the waiting room, valid inviteTtl (30 s).</summary>
    private async Task<Reply> InviteSendAsync(Person p, V4InviteSendRequest m)
    {
        if (p.Table is not { } t || p.Seat < 0) return Reply.Fail(V4Errors.E_NOT_SEATED);
        if (t.Status != "waiting") return Reply.Fail(V4Errors.E_STATUS);
        if (!Guid.TryParse(m.UserId, out var to) || to == p.Id || !await store.ExistsAsync(to)) return Reply.Fail(V4Errors.E_USER_UNKNOWN);
        if (!await store.FriendsAsync(p.Id, to) && !(m.Recent == true && await store.RecentAsync(p.Id, to))) return Reply.Fail(V4Errors.E_NOT_FRIENDS);
        if (!live.IsOnline(to)) return Reply.Fail(V4Errors.E_USER_OFFLINE);
        var v = P(to);
        if (Busy(v)) return Reply.Fail(V4Errors.E_USER_BUSY);
        var free = Array.IndexOf(t.Seats, null);
        if (free < 0) return Reply.Fail(V4Errors.E_TABLE_FULL);
        await CardAsync(v);
        var inv = new Invite
        {
            Id = "i" + (++inviteSeq), From = p.Id, To = to, Table = t, Seat = m.Seat is >= 0 and <= 3 && t.Seats[m.Seat.Value] is null ? m.Seat.Value : free,
            ExpiresAt = now + TableTimings.InviteTtl,
        };
        invites[inv.Id] = inv;
        inv.Timer = Schedule(inv.ExpiresAt, () =>
        {
            if (!invites.Remove(inv.Id)) return;
            Send(inv.From, V4Messages.InviteResult, new V4InviteResult { InviteId = inv.Id, UserId = to.ToString(), Result = "expired", At = now });
            Send(inv.To, V4Messages.InviteExpired, new V4InviteExpired { InviteId = inv.Id, At = now });
        });
        Send(to, V4Messages.InviteIn, new V4InviteIn { InviteId = inv.Id, From = Pub(p), Table = TablePub(t), ExpiresAt = inv.ExpiresAt, At = now });
        return Reply.With(("inviteId", inv.Id), ("expiresAt", inv.ExpiresAt));
    }

    private async Task<Reply> InviteRespondAsync(Person p, V4InviteRespondRequest m)
    {
        if (m.InviteId is null || !invites.TryGetValue(m.InviteId, out var inv) || inv.To != p.Id) return Reply.Fail(V4Errors.E_INVITE_EXPIRED);
        invites.Remove(inv.Id);
        Cancel(inv.Timer);
        if (!m.Accept)
        {
            Send(inv.From, V4Messages.InviteResult, new V4InviteResult { InviteId = inv.Id, UserId = p.Id.ToString(), Result = "declined", At = now });
            return Reply.Ok;
        }
        var t = inv.Table.Closed ? null : inv.Table;
        var refused = JoinCheck(t, p, t?.Pin, out var free);
        if (refused is null)
        {
            var seat = t!.Seats[inv.Seat] is null ? inv.Seat : free;
            refused = await SeatAsync(t, p, seat);
            if (refused is null)
            {
                Send(inv.From, V4Messages.InviteResult, new V4InviteResult { InviteId = inv.Id, UserId = p.Id.ToString(), Result = "accepted", Seat = seat, At = now });
                return Reply.Ok;
            }
        }
        Send(inv.From, V4Messages.InviteResult, new V4InviteResult { InviteId = inv.Id, UserId = p.Id.ToString(), Result = "failed", Error = refused.Error, At = now });
        return refused;
    }

    private Reply InviteCancel(Person p, V4InviteCancelRequest m)
    {
        if (m.InviteId is null || !invites.TryGetValue(m.InviteId, out var inv) || inv.From != p.Id) return Reply.Fail(V4Errors.E_INVITE_EXPIRED);
        invites.Remove(inv.Id);
        Cancel(inv.Timer);
        Send(inv.To, V4Messages.InviteExpired, new V4InviteExpired { InviteId = inv.Id, At = now });
        return Reply.Ok;
    }
}
