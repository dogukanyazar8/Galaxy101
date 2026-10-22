using System;
using System.Collections.Generic;

namespace Galaxy101.Contracts
{
    /// <summary>Stable machine-readable error codes; every non-2xx response carries one in <c>code</c>.</summary>
    public static class ErrorCodes
    {
        public const string Validation = "validation";
        public const string Unauthorized = "unauthorized";
        public const string NotFound = "not_found";
        public const string Conflict = "conflict";
        public const string RateLimited = "rate_limited";
        public const string InvalidToken = "invalid_token";
        public const string ProviderAlreadyLinked = "provider_already_linked";
        public const string InsufficientFunds = "insufficient_funds";
        public const string AlreadyOwned = "already_owned";
        public const string AlreadyClaimed = "already_claimed";
        public const string NotComplete = "not_complete";
        public const string ReceiptInvalid = "receipt_invalid";
        public const string ReceiptUsed = "receipt_used";
        public const string StoreUnavailable = "store_unavailable";
        public const string AlreadyFriends = "already_friends";
        public const string RequestsDisabled = "requests_disabled";
        public const string FriendLimit = "friend_limit";
        public const string NotOnline = "not_online";
        public const string InvitesDisabled = "invites_disabled";
    }

    public sealed class ErrorResponse
    {
        public string Code { get; set; }
        public string Message { get; set; }
    }

    public sealed class GuestLoginRequest
    {
        /// <summary>Optional client install id, kept for support and abuse limits only.</summary>
        public string DeviceId { get; set; }
        public string Platform { get; set; }
    }

    /// <summary><c>provider</c>: "google" or "apple"; the id token comes from the platform sign-in SDK.</summary>
    public sealed class ExternalLoginRequest
    {
        public string Provider { get; set; }
        public string IdToken { get; set; }
    }

    public sealed class RefreshRequest
    {
        public string RefreshToken { get; set; }
    }

    public sealed class TokenResponse
    {
        public Guid AccountId { get; set; }
        public string AccessToken { get; set; }
        public int ExpiresIn { get; set; }
        public string RefreshToken { get; set; }
        public bool IsNewAccount { get; set; }
        public List<string> LinkedProviders { get; set; } = new List<string>();
    }

    public sealed class RealtimeTicketResponse
    {
        public string Ticket { get; set; }
        public int ExpiresIn { get; set; }
    }

    public sealed class ServerInfoResponse
    {
        public string Version { get; set; }
        public string RuleVersion { get; set; }
        public DateTimeOffset ServerTime { get; set; }
        public string PrivacyPolicyVersion { get; set; }
        public string PrivacyPolicyUrl { get; set; }
    }
}
