using System;
using System.Collections.Generic;

namespace Galaxy101.Contracts
{
    public sealed class PlayerSummary
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Color { get; set; }
        public int Frame { get; set; }
        public string FriendCode { get; set; }
        public int Level { get; set; }
        public string League { get; set; }
    }

    /// <summary><c>presence</c>: "offline", "online" or "playing".</summary>
    public sealed class FriendResponse
    {
        public PlayerSummary Player { get; set; }
        public string Presence { get; set; }
        public string Status { get; set; }
        public DateTimeOffset? LastSeen { get; set; }
    }

    public sealed class FriendRequestResponse
    {
        public PlayerSummary Player { get; set; }
        public DateTimeOffset SentAt { get; set; }
    }

    public sealed class FriendsResponse
    {
        public List<FriendResponse> Friends { get; set; } = new List<FriendResponse>();
        public List<FriendRequestResponse> Incoming { get; set; } = new List<FriendRequestResponse>();
        public List<FriendRequestResponse> Outgoing { get; set; } = new List<FriendRequestResponse>();
    }

    public sealed class PlayerSearchResponse
    {
        public PlayerSummary Player { get; set; }
        public bool IsFriend { get; set; }
        public bool RequestSent { get; set; }
        public bool RequestReceived { get; set; }
    }

    public sealed class FriendCodeRequest
    {
        public string Code { get; set; }
    }

    public sealed class InviteRequest
    {
        public string RoomCode { get; set; }
    }

    public sealed class ShareLinkResponse
    {
        public string Url { get; set; }
        public string DeepLink { get; set; }
        public string FriendCode { get; set; }
    }

    public sealed class LeaderboardRow
    {
        public int Rank { get; set; }
        public PlayerSummary Player { get; set; }
        public long Points { get; set; }
        public bool Me { get; set; }
    }

    /// <summary><c>scope</c>: "weekly", "season" or "friends".</summary>
    public sealed class LeaderboardResponse
    {
        public string Scope { get; set; }
        public List<LeaderboardRow> Rows { get; set; } = new List<LeaderboardRow>();
        public LeaderboardRow Me { get; set; }
        public DateTimeOffset? EndsAt { get; set; }
    }
}
