namespace Galaxy101.Server.Infrastructure;

public sealed class AuthOptions
{
    public const string Section = "Auth";
    /// <summary>Base64 HMAC-SHA256 key, at least 32 bytes. Set through the environment in production.</summary>
    public string SigningKey { get; set; } = "";
    public string Issuer { get; set; } = "galaxy101";
    public string Audience { get; set; } = "galaxy101-client";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 90;
    /// <summary>OAuth client ids whose Google id tokens are accepted (Android, iOS, desktop).</summary>
    public string[] GoogleClientIds { get; set; } = [];
    /// <summary>Bundle / service ids whose Sign in with Apple tokens are accepted.</summary>
    public string[] AppleClientIds { get; set; } = [];
}

public sealed class StoreOptions
{
    public const string Section = "Store";
    public string GooglePackageName { get; set; } = "";
    /// <summary>Google Play service account key file content (JSON) with the Android Publisher scope.</summary>
    public string GoogleServiceAccountJson { get; set; } = "";
    public string AppleBundleId { get; set; } = "";
    /// <summary>Base64 DER of Apple Root CA - G3, the anchor for StoreKit 2 signed transactions.</summary>
    public string AppleRootCertificate { get; set; } = "";
    /// <summary>Accept StoreKit sandbox transactions (TestFlight / review). Keep true until launch review is done.</summary>
    public bool AppleAllowSandbox { get; set; } = true;
}

public sealed class GameOptions
{
    public const string Section = "Game";
    public string PublicBaseUrl { get; set; } = "https://galaxy101.app";
    public string DeepLinkScheme { get; set; } = "galaxy101";
    public string TimeZone { get; set; } = "Europe/Istanbul";
    public string PrivacyPolicyVersion { get; set; } = "2026-10";
    public string PrivacyPolicyUrl { get; set; } = "https://galaxy101.app/gizlilik";
    public long StartingChips { get; set; } = 10_000;
    public long StartingGems { get; set; } = 20;
    public int MaxFriends { get; set; } = 200;
    public int Season { get; set; } = 1;

    public int HandsPerMatch { get; set; } = 4;
    public int TurnSeconds { get; set; } = 30;
    public int NextHandSeconds { get; set; } = 15;
    /// <summary>Consecutive timeouts after which the server keeps playing the seat until its owner acts again.</summary>
    public int TimeoutsBeforeAuto { get; set; } = 2;
    /// <summary>House share of the pot, in percent.</summary>
    public int RakePercent { get; set; } = 0;
    public Dictionary<string, long[]> Stakes { get; set; } = new()
    {
        ["solo"] = [1_000, 5_000, 25_000, 100_000],
        ["pairs"] = [1_000, 5_000, 25_000, 100_000],
        ["doubling"] = [10_000, 50_000, 250_000],
        ["ranked"] = [0],
        ["private"] = [0, 1_000, 5_000, 25_000, 100_000],
    };
    /// <summary>Share of the pot by place for solo tables (pairs: the winning team splits the whole pot).</summary>
    public double[] SoloPayout { get; set; } = [1, 0, 0, 0];
    public int[] RankedLp { get; set; } = [24, 8, -8, -16];
    public int[] WeeklyPoints { get; set; } = [30, 15, 5, 1];
    public int XpHandWin { get; set; } = 340;
    public int XpHandPlayed { get; set; } = 60;
}
