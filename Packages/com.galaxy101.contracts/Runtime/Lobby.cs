using System;
using System.Collections.Generic;

namespace Galaxy101.Contracts
{
    /// <summary>Klasik lobby (spec/20): tables players open, list, join by seat / PIN / code, wait in, and start.</summary>
    public static class LobbyMessageTypes
    {
        // Client → server
        public const string Subscribe = "lobby.subscribe";
        public const string Unsubscribe = "lobby.unsubscribe";
        public const string Create = "table.create";
        public const string Join = "table.join";
        /// <summary>İzle: watch a playing table (TableJoinMessage: id or code, PIN when locked); match.leave stops.</summary>
        public const string Watch = "table.watch";
        public const string Leave = "table.leave";
        public const string AddBot = "table.addBot";
        public const string Kick = "table.kick";
        public const string Start = "table.start";
        // Server → client
        public const string Tables = "lobby.tables";
        public const string State = "table.state";
    }

    public static class TableErrors
    {
        public const string Full = "table_full";
        public const string Closed = "table_closed";
        public const string WrongPin = "wrong_pin";
        public const string Playing = "table_playing";
        public const string NotHost = "not_host";
        public const string NotReady = "not_ready";
    }

    /// <summary>Masa aç (S10). Values outside the lists are rejected.</summary>
    public sealed class TableOptions
    {
        /// <summary>"single" (Tekli) or "pairs" (Eşli: seats 0+2 vs 1+3).</summary>
        public string Type { get; set; } = "single";
        public string Name { get; set; }
        /// <summary>"public" (listed) or "private" (only by code).</summary>
        public string Visibility { get; set; } = "public";
        /// <summary>Four digits, or null for no PIN.</summary>
        public string Pin { get; set; }
        /// <summary>3, 5 or 7.</summary>
        public int Hands { get; set; } = 5;
        /// <summary>1.000, 5.000, 10.000, 50.000 or 100.000.</summary>
        public long Stake { get; set; } = 1000;
        /// <summary>Katlamalı: each later opener in a hand must beat the previous opener (series 101 → +1, pairs 5 → +1).</summary>
        public bool Doubling { get; set; }
        /// <summary>Katlamalı + Eşli: the partner's opening raises your bar too ("Ortağın açılışı eşiği yükseltsin"); default on.</summary>
        public bool PartnerRaises { get; set; } = true;
        /// <summary>15, 20, 30 or 45 seconds.</summary>
        public int TurnSeconds { get; set; } = 30;
        public bool Spectators { get; set; } = true;
        /// <summary>"on", "quick" (hazır cümleler) or "off".</summary>
        public string Chat { get; set; } = "on";
        public bool Gifts { get; set; } = true;
        /// <summary>İşleme (laying off on others' sets) allowed.</summary>
        public bool Processing { get; set; } = true;
        /// <summary>İşlek taş atma cezası: +101 for discarding a tile that fits a meld on the table.</summary>
        public bool Penalties { get; set; } = true;
        /// <summary>Okey atma cezası: +101 for discarding the okey.</summary>
        public bool OkeyPenalty { get; set; } = true;

        public static readonly int[] HandChoices = { 3, 5, 7 };
        public static readonly long[] StakeChoices = { 1000, 5000, 10000, 50000, 100000 };
        public static readonly int[] TurnChoices = { 15, 20, 30, 45 };
    }

    public sealed class TableSeat
    {
        public PlayerSummary Player { get; set; }
        public bool Bot { get; set; }
    }

    /// <summary>One table as the lobby list and the waiting room show it.</summary>
    public sealed class TableSummary
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public long Stake { get; set; }
        public int Hands { get; set; }
        public int TurnSeconds { get; set; }
        public bool Doubling { get; set; }
        /// <summary>Katlamalı + Eşli: the partner's opening raises the bar (see <see cref="TableOptions.PartnerRaises"/>).</summary>
        public bool PartnerRaises { get; set; } = true;
        public bool Locked { get; set; }
        public bool Private { get; set; }
        public bool Spectators { get; set; }
        public string Chat { get; set; }
        public bool Gifts { get; set; }
        public bool Processing { get; set; }
        public bool Penalties { get; set; }
        public bool OkeyPenalty { get; set; } = true;
        /// <summary>"waiting", "full" or "playing".</summary>
        public string Status { get; set; }
        /// <summary>Hand in play (1-based) while playing.</summary>
        public int Hand { get; set; }
        /// <summary>While playing: seats the server plays (a bot's, or one its player left) that a player may take ("Oyuna katıl").</summary>
        public int OpenSeats { get; set; }
        public Guid HostId { get; set; }
        /// <summary>Four entries; null for an empty seat.</summary>
        public List<TableSeat> Seats { get; set; } = new List<TableSeat>();
        public DateTimeOffset CreatedAt { get; set; }
    }

    public sealed class LobbyTablesMessage
    {
        public List<TableSummary> Tables { get; set; } = new List<TableSummary>();
    }

    public sealed class TableCreateMessage
    {
        public TableOptions Options { get; set; }
    }

    /// <summary>Sit at a listed table (<see cref="TableId"/>) or join by <see cref="Code"/> (GX-XXXX); PIN when locked.</summary>
    public sealed class TableJoinMessage
    {
        public Guid? TableId { get; set; }
        public string Code { get; set; }
        public string Pin { get; set; }
        /// <summary>Preferred seat (0–3); any free seat when null or taken.</summary>
        public int? Seat { get; set; }
    }

    public sealed class TableSeatMessage
    {
        public int Seat { get; set; }
    }

    /// <summary>The waiting room (S13), sent to everyone seated whenever it changes.</summary>
    public sealed class TableStateMessage
    {
        public TableSummary Table { get; set; }
        /// <summary>GX-XXXX, for sharing (members only).</summary>
        public string Code { get; set; }
        public int YourSeat { get; set; }
        /// <summary>Seconds left before the match starts; 0 = not counting.</summary>
        public int Countdown { get; set; }
        /// <summary>The table closed for you (left, kicked, closed by everyone leaving) or became a match.</summary>
        public bool Closed { get; set; }
        public Guid? MatchId { get; set; }
        public string Reason { get; set; }
    }
}
