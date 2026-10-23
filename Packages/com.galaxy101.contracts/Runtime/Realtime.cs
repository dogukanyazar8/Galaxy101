using System;
using System.Collections.Generic;

namespace Galaxy101.Contracts
{
    /// <summary>
    /// WebSocket frames are UTF-8 JSON objects <c>{ "type": "...", "id": "...", "data": { ... } }</c>.
    /// <c>id</c> is an optional client correlation id echoed back in <see cref="AckMessage"/>.
    /// </summary>
    public static class MessageTypes
    {
        // Client → server
        public const string Ping = "ping";
        public const string QueueJoin = "queue.join";
        public const string QueueLeave = "queue.leave";
        public const string RoomCreate = "room.create";
        public const string RoomJoin = "room.join";
        public const string RoomLeave = "room.leave";
        public const string MatchCommand = "match.command";
        public const string MatchSync = "match.sync";
        public const string MatchLeave = "match.leave";
        public const string MatchEmote = "match.emote";
        public const string MatchGift = "match.gift";

        // Server → client
        public const string Pong = "pong";
        public const string Ack = "ack";
        public const string Presence = "presence";
        public const string FriendRequest = "friend.request";
        public const string FriendAccepted = "friend.accepted";
        public const string Invite = "invite";
        public const string Wallet = "wallet";
        public const string QueueStatus = "queue.status";
        public const string RoomState = "room.state";
        public const string MatchStart = "match.start";
        public const string MatchState = "match.state";
        public const string MatchHandEnd = "match.handEnd";
        public const string MatchEnd = "match.end";
        public const string Replaced = "session.replaced";
    }

    /// <summary>Online modes in menu order. "bots" is offline and never reaches the server.</summary>
    public static class GameModes
    {
        public const string Solo = "solo";
        public const string Pairs = "pairs";
        public const string Doubling = "doubling";
        public const string Ranked = "ranked";
        public const string Private = "private";
        public static readonly string[] Rated = { Solo, Pairs, Doubling, Ranked };
    }

    public sealed class AckMessage
    {
        public string Id { get; set; }
        public bool Ok { get; set; }
        public string Code { get; set; }
    }

    public sealed class PresenceMessage
    {
        public Guid AccountId { get; set; }
        public string Presence { get; set; }
    }

    public sealed class InviteMessage
    {
        public PlayerSummary From { get; set; }
        public string RoomCode { get; set; }
        public long Stake { get; set; }
    }

    public sealed class QueueJoinMessage
    {
        public string Mode { get; set; }
        public long Stake { get; set; }
    }

    public sealed class QueueStatusMessage
    {
        public string Mode { get; set; }
        public long Stake { get; set; }
        public bool Queued { get; set; }
        public int Waiting { get; set; }
    }

    public sealed class RoomCreateMessage
    {
        public long Stake { get; set; }
        public string Password { get; set; }
    }

    public sealed class RoomJoinMessage
    {
        public string Code { get; set; }
        public string Password { get; set; }
    }

    public sealed class RoomStateMessage
    {
        public string Code { get; set; }
        public long Stake { get; set; }
        public Guid HostId { get; set; }
        public bool HasPassword { get; set; }
        /// <summary>Four entries; null for an empty seat.</summary>
        public List<PlayerSummary> Seats { get; set; } = new List<PlayerSummary>();
        public bool Closed { get; set; }
    }

    public sealed class MatchRefMessage
    {
        public Guid MatchId { get; set; }
    }

    public sealed class EmoteMessage
    {
        public Guid MatchId { get; set; }
        public int Seat { get; set; }
        public string Emote { get; set; }
    }

    public sealed class GiftMessage
    {
        public Guid MatchId { get; set; }
        public int Seat { get; set; }
        /// <summary>Recipient seat; null treats the whole table.</summary>
        public int? TargetSeat { get; set; }
        public string Gift { get; set; }
    }
}
