namespace Galaxy101.Table
{
    /// <summary>
    /// Server durations (ms) of the current prototype's <c>logic/mockServer.js</c> T. Bot thinking times
    /// are the engine's (<c>Okey101.Core.V4.G101Bots.Timing</c>, D-20). One source for the server and the offline client.
    /// </summary>
    public static class TableTimings
    {
        /// <summary>preDealCountdown: matchStart → the first hand's dealStart (3-2-1).</summary>
        public const int PreDealCountdown = 3000;
        /// <summary>seatedToCountdown: a table whose owner is not a seated person, 4th seat filled → waiting-room countdown.</summary>
        public const int SeatedToCountdown = 900;
        /// <summary>waitRoomCountdown: tableStart → matchStart, ticking every <see cref="CountdownTick"/>.</summary>
        public const int WaitRoomCountdown = 3000;
        public const int CountdownTick = 1000;

        // Current prototype dealStart → first turn: 20 358 ms with a bot dealer.
        public const int DealBuild = 2572;        // 400 + 106 × 12 + 900
        public const int BotRollDelay = 700;      // dealStep:dice → first die (bot dealer)
        public const int HumanRollWait = 15000;   // D-23: per die, a human dealer's gRollDice or the server rolls
        public const int Roll1 = 1570;            // 1450 + 120
        public const int Roll2 = 1990;            // 420 + 1450 + 120
        public const int BetweenRolls = 650;
        public const int AfterDice = 1000;
        public const int Indicator = 2300;
        public const int Extra = 1300;
        public const int Distribute = 4140;       // 250 + 4 × 480 + 4 × 380 + 450
        public const int Center = 2000;
        public const int Fill = 2136;             // 80 + 22 × 48 + 1000
        public const int SkipFill = 1100;         // mockServer.js T.deal.skipFill; the server still owns the first turn

        /// <summary>dealStart → first turn with a bot dealer: 20 358 ms in the current prototype.</summary>
        public const int DealTotalBotDealer = DealBuild + BotRollDelay + Roll1 + BetweenRolls + Roll2 + AfterDice + Indicator + Extra + Distribute + Center + Fill;

        /// <summary>turnMs default (table option 15 / 20 / 30 / 45 s).</summary>
        public const int DefaultTurnMs = 30000;
        /// <summary>handEndWait: handEnd → next dealStart; at once when every person sent gReady (D-25).</summary>
        public const int HandEndWait = 15000;
        public const int InviteTtl = 30000;
        public const int RankedAccept = 10000;
        public const int RankedWiden = 30000;
        public const int RankedLoading = 1800;
        public const int LeaveToLobby = 1300;
        /// <summary>autoplayAfterTimeouts: a disconnected seat's consecutive timeouts before the server plays it (R-66, D-35).</summary>
        public const int AutoplayAfterTimeouts = 2;
        /// <summary>reconnectWaitRoom: a waiting-room disconnect leaves at once.</summary>
        public const int ReconnectWaitRoom = 0;
        public const int ClientRetries = 5;
        public const int ClientRetryMs = 3000;
        public const int LoginLock = 300000;
    }

    /// <summary>Table economy and options of the package (RULES.md §8, mockServer.js ECON, protocol.json tableCreate).</summary>
    public static class TableEconomy
    {
        /// <summary>R-91: no rake.</summary>
        public const int Rake = 0;
        /// <summary>R-93: hand XP (finisher / everyone else).</summary>
        public const int XpHandWin = 340, XpHand = 60;
        /// <summary>R-93: match-end place bonus.</summary>
        public static readonly int[] XpPlace = { 500, 200, 80, 0 };
        /// <summary>R-95: weekly leaderboard points by place.</summary>
        public static readonly int[] Weekly = { 30, 15, 5, 1 };
        public static readonly long[] Bets = { 1000, 5000, 10000, 50000, 100000 };
        public static readonly int[] Hands = { 3, 5, 7, 9, 11 };
        public static readonly int[] TurnSec = { 15, 20, 30, 40, 50, 60 };
        /// <summary>R-94: ranked is 5 hands, 30 s, no stake, no katlama.</summary>
        public const int RankedHands = 5, RankedTurnSec = 30;
        /// <summary>D-32: free chat text limit.</summary>
        public const int ChatMax = 120;
        /// <summary>Table strings use quick.0–quick.5; quick.6–quick.8 remain accepted for earlier protocol clients. Emoji keys are emoji.0–emoji.9.</summary>
        public static readonly string[] Quick = { "quick.0", "quick.1", "quick.2", "quick.3", "quick.4", "quick.5", "quick.6", "quick.7", "quick.8" };

        public static bool IsQuickKey(string key)
        {
            if (key == null) return false;
            foreach (var q in Quick) if (q == key) return true;
            return key.Length == 7 && key.StartsWith("emoji.") && key[6] >= '0' && key[6] <= '9';
        }

        /// <summary>JavaScript Math.round (halves toward +∞), as D-40 shares are computed in the package.</summary>
        public static int JsRound(double x) => (int)System.Math.Floor(x + 0.5);
    }
}
