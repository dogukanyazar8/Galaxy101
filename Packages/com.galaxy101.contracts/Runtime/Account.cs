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
        /// <summary>E-posta ile giriş (spec/18): no account for that e-mail.</summary>
        public const string AccountNotFound = "account_not_found";
        /// <summary>Wrong password; <see cref="LoginErrorResponse.RemainingAttempts"/> says how many tries are left.</summary>
        public const string WrongPassword = "wrong_password";
        /// <summary>Three wrong passwords: the e-mail is locked for a while (<see cref="LoginErrorResponse.RetryAfter"/> seconds).</summary>
        public const string AccountLocked = "account_locked";
        public const string EmailTaken = "email_taken";
        public const string TermsRequired = "terms_required";
        /// <summary>Şifremi unuttum: the code is wrong, used up or expired.</summary>
        public const string CodeInvalid = "code_invalid";
        /// <summary>The server cannot send e-mail (not configured or the relay failed).</summary>
        public const string EmailUnavailable = "email_unavailable";
        /// <summary>The text has words the game does not allow (names, status, table names).</summary>
        public const string Inappropriate = "inappropriate";
        /// <summary>Sign-in refused: a moderator's ban runs for <see cref="LoginErrorResponse.RetryAfter"/> more seconds.</summary>
        public const string Banned = "banned";
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
        /// <summary>Takma ad (spec/18 S05, at most 16 characters); empty = "Misafir####".</summary>
        public string Name { get; set; }
        /// <summary>One of the eight avatar colours; empty = random.</summary>
        public string Color { get; set; }
    }

    /// <summary>Kayıt ol (spec/18 S04): ad, e-posta, şifre (≥ 6), şartlar onayı. Starts with the registered balance.</summary>
    public sealed class RegisterRequest
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public bool AcceptTerms { get; set; }
        public string Color { get; set; }
        public string DeviceId { get; set; }
        public string Platform { get; set; }
    }

    /// <summary>Giriş yap (spec/18 S03).</summary>
    public sealed class PasswordLoginRequest
    {
        public string Email { get; set; }
        public string Password { get; set; }
    }

    /// <summary>Şifremi unuttum (spec/18 S03), step one: a six-digit code to the e-mail (the answer never says whether it has an account).</summary>
    public sealed class PasswordForgotRequest
    {
        public string Email { get; set; }
    }

    /// <summary>Step two: the code and the new password (≥ 6). Every session of the account ends.</summary>
    public sealed class PasswordResetRequest
    {
        public string Email { get; set; }
        public string Code { get; set; }
        public string Password { get; set; }
    }

    /// <summary>Error body of a failed password login.</summary>
    public sealed class LoginErrorResponse
    {
        public string Code { get; set; }
        public string Message { get; set; }
        public int RemainingAttempts { get; set; }
        public int RetryAfter { get; set; }
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
