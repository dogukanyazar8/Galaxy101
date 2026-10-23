using System;
using System.Collections.Generic;

namespace Galaxy101.Contracts
{
    public sealed class ProfileResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Color { get; set; }
        public string Status { get; set; }
        public int Frame { get; set; }
        public string FriendCode { get; set; }
        public string Planet { get; set; }
        public ProgressResponse Progress { get; set; }
        public List<string> LinkedProviders { get; set; } = new List<string>();
        public string AcceptedPrivacyVersion { get; set; }
    }

    public sealed class ProgressResponse
    {
        public long Xp { get; set; }
        public int Level { get; set; }
        public long LevelXp { get; set; }
        public long NextLevelXp { get; set; }
        public int Lp { get; set; }
        public string League { get; set; }
        public int NextLeagueLp { get; set; }
    }

    public sealed class ProfileUpdateRequest
    {
        public string Name { get; set; }
        public string Color { get; set; }
        public string Status { get; set; }
        public int Frame { get; set; }
    }

    public sealed class StatsResponse
    {
        public int Hands { get; set; }
        public int Wins { get; set; }
        public int WinRate { get; set; }
        public int BestOpening { get; set; }
        public int OkeyFinishes { get; set; }
        public int BestStreak { get; set; }
        /// <summary>Win rate per online mode, indexed by <see cref="GameModes"/> order (solo, pairs, doubling, ranked).</summary>
        public List<int> ModeWinRates { get; set; } = new List<int>();
    }

    public sealed class BadgeResponse
    {
        public string Id { get; set; }
        public long Current { get; set; }
        public long Target { get; set; }
        public bool Unlocked { get; set; }
    }

    public sealed class HistoryEntryResponse
    {
        public Guid MatchId { get; set; }
        public string Mode { get; set; }
        public int Place { get; set; }
        public long ChipDelta { get; set; }
        public int? LpDelta { get; set; }
        public DateTimeOffset FinishedAt { get; set; }
    }

    public sealed class PrivacyConsentRequest
    {
        public string Version { get; set; }
    }

    /// <summary>Cloud-synced preferences. Resolution is device-local and deliberately absent.</summary>
    public sealed class SettingsDto
    {
        public bool AutoSort { get; set; } = true;
        public bool Highlight { get; set; } = true;
        public bool Dbl { get; set; }
        public bool Chat { get; set; } = true;
        public int Warn { get; set; } = 5;
        public string Tile { get; set; } = "Orta";
        public int Master { get; set; } = 80;
        public int Music { get; set; } = 60;
        public int Sfx { get; set; } = 90;
        public bool TileSnd { get; set; } = true;
        public bool NotifSnd { get; set; } = true;
        public bool Motion { get; set; } = true;
        public string Quality { get; set; } = "Yüksek";
        public string Lang { get; set; } = "tr";
        public bool FriendReq { get; set; } = true;
        public bool Invites { get; set; } = true;
    }

    public sealed class SettingsResponse
    {
        public SettingsDto Settings { get; set; }
        public string Planet { get; set; }
        /// <summary>Optimistic concurrency token; send it back on save.</summary>
        public long Revision { get; set; }
    }

    public sealed class SettingsUpdateRequest
    {
        public SettingsDto Settings { get; set; }
        public long Revision { get; set; }
    }

    public sealed class PlanetUpdateRequest
    {
        public string Planet { get; set; }
    }
}
