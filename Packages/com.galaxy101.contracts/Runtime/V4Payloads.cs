using System;
using System.Collections.Generic;

namespace Galaxy101.Contracts
{
    // v4 payloads (protocol.json 0.4.0). Field names follow the package; JSON is camelCase on both sides.
    // Engine-shaped parts (GameView, EngineEvent, ranking units, history) stay untyped (object): their fields are the
    // rules engine's (logic/rules.js ev.push lines), see docs/v4/PROTOKOL_ESLEME.md.

    // ---- Client → server ----------------------------------------------------------------------------------------

    public sealed class V4HelloRequest
    {
        /// <summary>The socket is already signed in by its ticket; the token is accepted and ignored (package DEMO-11).</summary>
        public string Token { get; set; }
    }

    public sealed class V4TableRules
    {
        public bool Spectate { get; set; } = true;
        /// <summary>"open", "quick" or "off" (R-63).</summary>
        public string Chat { get; set; } = "open";
        public bool Gifts { get; set; } = true;
        public bool Isle { get; set; } = true;
        public bool Penalties { get; set; } = true;
    }

    public sealed class V4TableCreateRequest
    {
        public string Name { get; set; }
        /// <summary>"solo" or "pairs".</summary>
        public string Mode { get; set; } = "solo";
        public bool Private { get; set; }
        /// <summary>Four digits, or null.</summary>
        public string Pin { get; set; }
        public int Hands { get; set; }
        public long Bet { get; set; }
        public bool Katlama { get; set; }
        public int TurnSec { get; set; }
        public V4TableRules Rules { get; set; }
        /// <summary>Pairs + katlamalı only; default true.</summary>
        public bool? KatlamaPartner { get; set; }
    }

    public sealed class V4TableJoinRequest
    {
        public string TableId { get; set; }
        public string Pin { get; set; }
        public int? Seat { get; set; }
    }

    public sealed class V4TableJoinCodeRequest
    {
        public string Code { get; set; }
        public string Pin { get; set; }
    }

    public sealed class V4TableSpectateRequest
    {
        public string TableId { get; set; }
    }

    public sealed class V4SeatRequest
    {
        public int Seat { get; set; }
    }

    public sealed class V4InviteSendRequest
    {
        public string UserId { get; set; }
        public int? Seat { get; set; }
        /// <summary>The invitee is a recent co-player rather than a friend.</summary>
        public bool? Recent { get; set; }
    }

    public sealed class V4InviteRespondRequest
    {
        public string InviteId { get; set; }
        public bool Accept { get; set; }
    }

    public sealed class V4InviteCancelRequest
    {
        public string InviteId { get; set; }
    }

    public sealed class V4VsBotsStartRequest
    {
        public string Mode { get; set; } = "solo";
        public int? Hands { get; set; }
    }

    public sealed class V4RankedQueueRequest
    {
        /// <summary>"solo", "duo" (party of 1) or "squad" (party of 3).</summary>
        public string Mode { get; set; }
        public List<string> Party { get; set; } = new List<string>();
    }

    public sealed class V4RankedAcceptRequest
    {
        public string MatchId { get; set; }
        public bool Accept { get; set; }
    }

    public sealed class V4DrawRequest
    {
        /// <summary>"deck" or "side".</summary>
        public string Src { get; set; }
    }

    public sealed class V4Meld
    {
        /// <summary>"run", "group" or "pair".</summary>
        public string Kind { get; set; }
        public List<string> Tiles { get; set; } = new List<string>();
    }

    public sealed class V4MeldsRequest
    {
        public List<V4Meld> Melds { get; set; } = new List<V4Meld>();
    }

    public sealed class V4AddRequest
    {
        public string Tile { get; set; }
        public string Meld { get; set; }
        /// <summary>"L", "R" or null.</summary>
        public string Side { get; set; }
    }

    public sealed class V4SwapRequest
    {
        public string Tile { get; set; }
        public string Meld { get; set; }
        public int Index { get; set; }
    }

    public sealed class V4DiscardRequest
    {
        public string Tile { get; set; }
    }

    public sealed class V4RackOrderRequest
    {
        public List<string> Tiles { get; set; } = new List<string>();
    }

    public sealed class V4ChatSendRequest
    {
        /// <summary>Free text, at most 120 characters (D-32).</summary>
        public string Text { get; set; }
        /// <summary>A table string key (quick.0–quick.5) or emoji.0–emoji.9; earlier quick.6–quick.8 keys remain accepted without remapping.</summary>
        public string Quick { get; set; }
    }

    public sealed class V4GiftSendRequest
    {
        public string Gift { get; set; }
        /// <summary>A seat (int) or "all" (price × recipients, D-26).</summary>
        public object To { get; set; }
    }

    // ---- Server → client ----------------------------------------------------------------------------------------

    public class V4User
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Ini { get; set; }
        public string Color { get; set; }
        public int Level { get; set; }
        public int Frame { get; set; }
        public string Status { get; set; }
        public int RankIdx { get; set; }
        public bool Online { get; set; }
        public bool InGame { get; set; }
    }

    public sealed class V4Rank
    {
        /// <summary>Division 0 (Bronz III) … 14 (Usta I), 15 past Usta I.</summary>
        public int Idx { get; set; }
        /// <summary>LP into the division (0–99).</summary>
        public int Lp { get; set; }
        /// <summary>Protected matches left (D-30).</summary>
        public int Prot { get; set; }
        public int Peak { get; set; }
    }

    public sealed class V4Me : V4User
    {
        public long Xp { get; set; }
        public long XpNext { get; set; }
        public long Chips { get; set; }
        public long Gems { get; set; }
        public string Code { get; set; }
        public string Skin { get; set; }
        public List<string> Skins { get; set; } = new List<string>();
        public V4Rank Rank { get; set; }
    }

    public sealed class V4Seat
    {
        public V4User User { get; set; }
        public bool Bot { get; set; }
        public bool Connected { get; set; } = true;
    }

    public sealed class V4Table
    {
        public string Id { get; set; }
        /// <summary>GX-XXXX, shown for private tables (members share it).</summary>
        public string Code { get; set; }
        public string Name { get; set; }
        public string Mode { get; set; }
        public bool Private { get; set; }
        public bool Locked { get; set; }
        public int Hands { get; set; }
        public long Bet { get; set; }
        public bool Katlama { get; set; }
        /// <summary>Null unless the table is pairs + katlamalı.</summary>
        public bool? KatlamaPartner { get; set; }
        public int TurnSec { get; set; }
        public V4TableRules Rules { get; set; }
        public string Owner { get; set; }
        public bool Ranked { get; set; }
        public bool VsBots { get; set; }
        /// <summary>"waiting", "countdown", "playing" or "over".</summary>
        public string Status { get; set; }
        public int HandNo { get; set; }
        /// <summary>Four entries; null for an empty seat.</summary>
        public List<V4Seat> Seats { get; set; } = new List<V4Seat>();
        public int Spectators { get; set; }
        public int Countdown { get; set; }
    }

    public sealed class V4RankedFound
    {
        public string MatchId { get; set; }
        public List<V4RankedPlayer> Players { get; set; } = new List<V4RankedPlayer>();
        public long ExpiresAt { get; set; }
        public List<bool> Accepted { get; set; } = new List<bool>();
    }

    public sealed class V4RankedPlayer
    {
        public V4User User { get; set; }
        public bool Bot { get; set; }
    }

    public sealed class V4Queue
    {
        public string Mode { get; set; }
        public long Since { get; set; }
        /// <summary>1: ±1 league; 2: ±2 after rankedWiden (30 s).</summary>
        public int Range { get; set; }
        public int Filled { get; set; }
        public V4RankedFound Found { get; set; }
    }

    public sealed class V4Session
    {
        public V4Me Me { get; set; }
        public string ServerVersion { get; set; }
        public long ServerTime { get; set; }
        public long At { get; set; }
    }

    public sealed class V4Snapshot
    {
        public V4Me Me { get; set; }
        /// <summary>"menu", "waitroom", "table", "ranked.search", "ranked.found" or "ranked.loading".</summary>
        public string Screen { get; set; }
        public V4Table Table { get; set; }
        /// <summary>GameView: the rules state as this seat may see it (own rack only, counts for the others).</summary>
        public object Game { get; set; }
        public V4Queue Ranked { get; set; }
        public bool? Spectator { get; set; }
        public long At { get; set; }
    }

    public sealed class V4Balance
    {
        public long Chips { get; set; }
        public long Gems { get; set; }
        public long DChips { get; set; }
        public long DGems { get; set; }
        /// <summary>"bet", "betRefund", "win", "daily", "quest", "purchase", "shop" or "gift".</summary>
        public string Reason { get; set; }
        public string Ref { get; set; }
        public long At { get; set; }
    }

    public sealed class V4Xp
    {
        public long Gained { get; set; }
        public long Xp { get; set; }
        public long XpNext { get; set; }
        public int Level { get; set; }
        public int LevelUp { get; set; }
        /// <summary>"hand" or "match".</summary>
        public string Reason { get; set; }
        public long At { get; set; }
    }

    public sealed class V4LobbyEvent
    {
        public List<V4Table> Tables { get; set; } = new List<V4Table>();
        public long At { get; set; }
    }

    public sealed class V4TableEvent
    {
        public V4Table Table { get; set; }
        public long At { get; set; }
    }

    public sealed class V4IdEvent
    {
        public string Id { get; set; }
        public long At { get; set; }
    }

    public sealed class V4TableClosed
    {
        public string Id { get; set; }
        public string Reason { get; set; }
        public long At { get; set; }
    }

    public sealed class V4Kicked
    {
        public string TableId { get; set; }
        public long At { get; set; }
    }

    public sealed class V4Countdown
    {
        public int N { get; set; }
        public long StartsIn { get; set; }
        public bool? Cancelled { get; set; }
        public long At { get; set; }
    }

    public sealed class V4SeatConnection
    {
        public int Seat { get; set; }
        public bool Connected { get; set; }
        public bool? Autoplay { get; set; }
        public long At { get; set; }
    }

    public sealed class V4LeftTable
    {
        /// <summary>"leave" or "disconnect".</summary>
        public string Reason { get; set; }
        public long ToLobbyIn { get; set; }
        public long At { get; set; }
    }

    public sealed class V4InviteIn
    {
        public string InviteId { get; set; }
        public V4User From { get; set; }
        public V4Table Table { get; set; }
        public long ExpiresAt { get; set; }
        public long At { get; set; }
    }

    public sealed class V4InviteExpired
    {
        public string InviteId { get; set; }
        public long At { get; set; }
    }

    public sealed class V4InviteResult
    {
        public string InviteId { get; set; }
        public string UserId { get; set; }
        /// <summary>"accepted", "declined", "expired" or "failed".</summary>
        public string Result { get; set; }
        public int? Seat { get; set; }
        public string Error { get; set; }
        public long At { get; set; }
    }

    public sealed class V4RankedSearch
    {
        public string Mode { get; set; }
        public long Since { get; set; }
        public int Range { get; set; }
        public int Filled { get; set; }
        public V4RankedFound Found { get; set; }
        public long At { get; set; }
    }

    public sealed class V4RankedFoundEvent
    {
        public V4RankedFound Found { get; set; }
        public long At { get; set; }
    }

    public sealed class V4RankedAccepted
    {
        public int Index { get; set; }
        public List<bool> Accepted { get; set; } = new List<bool>();
        public long At { get; set; }
    }

    public sealed class V4RankedCancelled
    {
        /// <summary>"timeout", "declined" or "cancel".</summary>
        public string Reason { get; set; }
        public long At { get; set; }
    }

    public sealed class V4RankedReady
    {
        public V4Table Table { get; set; }
        public long LoadingMs { get; set; }
        public long At { get; set; }
    }

    public sealed class V4RankPoint
    {
        public int Idx { get; set; }
        public int Lp { get; set; }
    }

    public sealed class V4RankChange
    {
        public V4RankPoint Before { get; set; }
        public V4RankPoint After { get; set; }
        public int Delta { get; set; }
        public int Place { get; set; }
        public bool TierUp { get; set; }
        public bool LeagueUp { get; set; }
        public bool Down { get; set; }
        public int ProtectedLeft { get; set; }
        public long At { get; set; }
    }

    public sealed class V4MatchStart
    {
        public V4Table Table { get; set; }
        public long CountdownMs { get; set; }
        public long At { get; set; }
    }

    public sealed class V4DealStart
    {
        public int HandNo { get; set; }
        public int Dealer { get; set; }
        public int Stacks { get; set; } = 15;
        public int PerStack { get; set; } = 7;
        public long BuildMs { get; set; }
        public bool HumanDealer { get; set; }
        public long At { get; set; }
    }

    public sealed class V4OkeyFace
    {
        /// <summary>Colour: R, B, Y or K.</summary>
        public string C { get; set; }
        public int N { get; set; }
    }

    public sealed class V4DealPlan
    {
        public int Seat { get; set; }
        public List<int> Stacks { get; set; } = new List<int>();
    }

    /// <summary>A ceremony step; only the fields of its step are set (the indicator only in "indicator").</summary>
    public sealed class V4DealStep
    {
        /// <summary>"dice", "indicator", "extra", "distribute" or "center".</summary>
        public string Step { get; set; }
        public int? Dealer { get; set; }
        public long? RollWaitMs { get; set; }
        public int? Stack { get; set; }
        public int? Level { get; set; }
        public int? RolledLevel { get; set; }
        public bool? FakeSkip { get; set; }
        public string Indicator { get; set; }
        public V4OkeyFace Okey { get; set; }
        public List<V4DealPlan> Plan { get; set; }
        public List<int> Stacks { get; set; }
        public int? Deck { get; set; }
        public long At { get; set; }
    }

    public sealed class V4DealDie
    {
        public int Index { get; set; }
        public int Value { get; set; }
        public long RollMs { get; set; }
        public long At { get; set; }
    }

    /// <summary>Authoritative shortcut to fill: the already-determined dice and public indicator, never another seat's rack.</summary>
    public sealed class V4DealSkip
    {
        public List<int> Dice { get; set; } = new List<int>();
        public int Stack { get; set; }
        public int Level { get; set; }
        public bool FakeSkip { get; set; }
        public string Indicator { get; set; }
        public V4OkeyFace Okey { get; set; }
        public int ExtraStack { get; set; }
        public long At { get; set; }
    }

    public sealed class V4DealSkipVote
    {
        public int Seat { get; set; }
        public long At { get; set; }
    }

    public sealed class V4HandDealt
    {
        public int HandNo { get; set; }
        /// <summary>Your own rack only (null for a spectator).</summary>
        public List<string> Rack { get; set; }
        public List<int> Counts { get; set; } = new List<int>();
        public int Deck { get; set; }
        public string Indicator { get; set; }
        public V4OkeyFace Okey { get; set; }
        public int Starter { get; set; }
        public long At { get; set; }
    }

    public sealed class V4Turn
    {
        public int Seat { get; set; }
        /// <summary>"draw" or "play".</summary>
        public string Phase { get; set; }
        public int N { get; set; }
        public long TurnMs { get; set; }
        public long Deadline { get; set; }
        public long At { get; set; }
    }

    public sealed class V4Legal
    {
        /// <summary>{draw, side, discard, open, undo, returnSide, add} from the rules engine.</summary>
        public object Legal { get; set; }
        public long At { get; set; }
    }

    public sealed class V4GameEvent
    {
        /// <summary>A rules engine event (e: drew, sideReturned, openShort, opened, laid, added, okeySwapped, undone, discarded,
        /// finished, penalty, auto, handStart, turn, seatLeft, seatTaken, handEnd, matchEnd), filtered for the receiver.</summary>
        public object Ev { get; set; }
        public long At { get; set; }
    }

    public sealed class V4TableView
    {
        public object View { get; set; }
        public long At { get; set; }
    }

    public sealed class V4HandWait
    {
        public long NextHandIn { get; set; }
        public long NextAt { get; set; }
        public int HandNo { get; set; }
        public int Hands { get; set; }
        public long At { get; set; }
    }

    public sealed class V4ReadyState
    {
        public List<int> Ready { get; set; } = new List<int>();
        public long At { get; set; }
    }

    public sealed class V4MatchResult
    {
        /// <summary>Units from the rules engine: {seats, total, rank, left, ...}.</summary>
        public object Ranking { get; set; }
        public List<int> Totals { get; set; } = new List<int>();
        public object History { get; set; }
        /// <summary>The stakes paid in (R-91, D-34).</summary>
        public long Pool { get; set; }
        public int Rake { get; set; }
        public List<long> Payouts { get; set; } = new List<long>();
        public List<int> XpBonus { get; set; } = new List<int>();
        public List<int?> Lp { get; set; } = new List<int?>();
        public List<int> Weekly { get; set; } = new List<int>();
        public long At { get; set; }
    }

    public sealed class V4Chat
    {
        public int Seat { get; set; }
        public string User { get; set; }
        public string Text { get; set; }
        public string Quick { get; set; }
        public bool? Bot { get; set; }
        public bool? Spectator { get; set; }
        public long At { get; set; }
    }

    public sealed class V4Gift
    {
        public int From { get; set; }
        public List<int> To { get; set; } = new List<int>();
        public string Gift { get; set; }
        public long Cost { get; set; }
        public long At { get; set; }
    }
}
