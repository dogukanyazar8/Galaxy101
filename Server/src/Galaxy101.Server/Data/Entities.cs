namespace Galaxy101.Server.Data;

public sealed class Account
{
    public Guid Id { get; set; }
    public required string FriendCode { get; set; }
    public required string Name { get; set; }
    public required string Color { get; set; }
    public string Status { get; set; } = "";
    public int Frame { get; set; }
    public string Planet { get; set; } = "Satürn";
    /// <summary>Bit per planet theme the player has selected at least once (badge progress).</summary>
    public int PlanetsTried { get; set; }
    public string SettingsJson { get; set; } = "{}";
    public long SettingsRevision { get; set; }
    public string? AcceptedPrivacyVersion { get; set; }
    public string? Platform { get; set; }
    public string? DeviceId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
}

/// <summary>E-mail + password sign-in (spec/18). The e-mail is stored trimmed and lower-cased.</summary>
public sealed class PasswordLogin
{
    public required string Email { get; set; }
    public Guid AccountId { get; set; }
    /// <summary>PBKDF2-SHA256: "iterations.salt.hash" (base64).</summary>
    public required string Hash { get; set; }
    public int FailedAttempts { get; set; }
    public DateTimeOffset? LockedUntil { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class ExternalLogin
{
    public required string Provider { get; set; }
    public required string Subject { get; set; }
    public Guid AccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>Opaque rotating refresh token; only its SHA-256 is stored. Reuse of a rotated token revokes the family.</summary>
public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid FamilyId { get; set; }
    public required byte[] TokenHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RotatedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}

public sealed class Wallet
{
    public Guid AccountId { get; set; }
    public long Chips { get; set; }
    public long Gems { get; set; }
}

/// <summary>Append-only record of every balance change. (account, idempotency key) is unique.</summary>
public sealed class LedgerEntry
{
    public long Id { get; set; }
    public Guid AccountId { get; set; }
    public required string Currency { get; set; }
    public long Delta { get; set; }
    public long BalanceAfter { get; set; }
    public required string Reason { get; set; }
    public required string IdempotencyKey { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class OwnedItem
{
    public Guid AccountId { get; set; }
    public required string ItemId { get; set; }
    public DateTimeOffset AcquiredAt { get; set; }
}

public sealed class StorePurchase
{
    public required string Platform { get; set; }
    public required string TransactionId { get; set; }
    /// <summary>Null once the buyer deleted the account; the row stays as the anti-replay and accounting record.</summary>
    public Guid? AccountId { get; set; }
    public required string ProductId { get; set; }
    public required string OfferId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class DailyState
{
    public Guid AccountId { get; set; }
    /// <summary>Days claimed in the current streak (0–7, wraps after the seventh).</summary>
    public int StreakCount { get; set; }
    public DateOnly? LastClaimDay { get; set; }
    public DateOnly QuestDay { get; set; }
    public int[] QuestProgress { get; set; } = [];
    public int QuestClaimedMask { get; set; }
}

/// <summary>An undirected pair stored once with LowId &lt; HighId.</summary>
public sealed class Friendship
{
    public Guid LowId { get; set; }
    public Guid HighId { get; set; }
    public Guid RequesterId { get; set; }
    public bool Accepted { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

public sealed class PlayerStats
{
    public Guid AccountId { get; set; }
    public long Xp { get; set; }
    public int Lp { get; set; }
    public int Hands { get; set; }
    public int Wins { get; set; }
    public int OkeyFinishes { get; set; }
    public int PairsOpenings { get; set; }
    public int BestOpening { get; set; }
    public int CurrentStreak { get; set; }
    public int BestStreak { get; set; }
    public int[] ModeHands { get; set; } = new int[4];
    public int[] ModeWins { get; set; } = new int[4];
}

/// <summary>Board keys: "w:2026-10-19" (week starting that Monday) or "s:1" (season).</summary>
public sealed class LeaderboardScore
{
    public required string Board { get; set; }
    public Guid AccountId { get; set; }
    public long Points { get; set; }
}

public enum MatchStatus { Active, Completed, Aborted }

public sealed class MatchRecord
{
    public Guid Id { get; set; }
    public required string Mode { get; set; }
    public long Stake { get; set; }
    public MatchStatus Status { get; set; }
    public Guid[] Seats { get; set; } = [];
    public int Hands { get; set; }
    public int FirstStarter { get; set; }
    public int LeftMask { get; set; }
    /// <summary>Seats a bot holds from the start (Klasik table "Bot ekle"): no stake, no payout, the server plays them.</summary>
    public int BotMask { get; set; }
    /// <summary>Per-table turn time (spec/20: 15/20/30/45 s); 0 = the server default.</summary>
    public int TurnSeconds { get; set; }
    /// <summary>Table rules the session enforces: "chat" (on/quick/off), "gifts" (bool). JSON object.</summary>
    public string OptionsJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
}

public sealed class MatchHand
{
    public Guid MatchId { get; set; }
    public int HandIndex { get; set; }
    public long Seed { get; set; }
    public Guid RoundId { get; set; }
    /// <summary>Per-seat hand totals once the hand ends; empty while it is in play.</summary>
    public int[] Scores { get; set; } = [];
}

/// <summary>Accepted moves in order; replaying them over the seeded deal rebuilds the round after a restart.</summary>
public sealed class MatchMove
{
    public Guid MatchId { get; set; }
    public int HandIndex { get; set; }
    public long Version { get; set; }
    public int Seat { get; set; }
    public required string Payload { get; set; }
}

public sealed class MatchParticipant
{
    public Guid MatchId { get; set; }
    public int Seat { get; set; }
    public Guid? AccountId { get; set; }
    public required string Mode { get; set; }
    public int Place { get; set; }
    public int Total { get; set; }
    public long ChipDelta { get; set; }
    public int? LpDelta { get; set; }
    public long Xp { get; set; }
    public bool Left { get; set; }
    public DateTimeOffset FinishedAt { get; set; }
}
