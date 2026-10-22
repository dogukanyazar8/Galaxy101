using System;
using System.Collections.Generic;

namespace Galaxy101.Contracts
{
    /// <summary>
    /// A move in an online match. <c>kind</c>: "draw" (with <c>source</c> "stock" / "discard"), "return" (put the
    /// previous discard back), "shortOpening" (with <c>opening</c>) or "commit" (with <c>plan</c>).
    /// <c>expectedVersion</c> is the view version the move was made against; stale moves are rejected.
    /// </summary>
    public sealed class MatchCommandMessage
    {
        public Guid MatchId { get; set; }
        public long ExpectedVersion { get; set; }
        public string Kind { get; set; }
        public string Source { get; set; }
        public string Opening { get; set; }
        public TurnPlanDto Plan { get; set; }
    }

    public sealed class TurnPlanDto
    {
        public List<TableActionDto> Actions { get; set; } = new List<TableActionDto>();
        public int Discard { get; set; }
        public bool AcceptPenalty { get; set; }
    }

    /// <summary><c>type</c>: "open" (opening + groups), "add" (group), "extend" (meldId, prepend, tiles), "swap" (meldId, jokerId, replacementId).</summary>
    public sealed class TableActionDto
    {
        public string Type { get; set; }
        public string Opening { get; set; }
        public List<MeldDto> Groups { get; set; }
        public MeldDto Group { get; set; }
        public int MeldId { get; set; }
        public bool Prepend { get; set; }
        public List<PlacedTileDto> Tiles { get; set; }
        public int JokerId { get; set; }
        public int ReplacementId { get; set; }
    }

    /// <summary><c>kind</c>: "run", "set" or "pair". On the table <c>id</c> and <c>owner</c> are set.</summary>
    public sealed class MeldDto
    {
        public int Id { get; set; }
        public int Owner { get; set; }
        public string Kind { get; set; }
        public List<PlacedTileDto> Tiles { get; set; } = new List<PlacedTileDto>();
    }

    /// <summary><c>color</c>: "red", "yellow", "blue", "black"; the face the tile stands for (jokers included).</summary>
    public sealed class PlacedTileDto
    {
        public int Id { get; set; }
        public string Color { get; set; }
        public int Number { get; set; }
    }

    public sealed class SeatResponse
    {
        public int Seat { get; set; }
        public int Team { get; set; }
        public PlayerSummary Player { get; set; }
        /// <summary>"connected", "disconnected", "auto" (server plays for the seat) or "left".</summary>
        public string Status { get; set; }
    }

    public sealed class MatchStartMessage
    {
        public Guid MatchId { get; set; }
        public string Mode { get; set; }
        public long Stake { get; set; }
        public int Hands { get; set; }
        public int YourSeat { get; set; }
        public int TurnSeconds { get; set; }
        public List<SeatResponse> Seats { get; set; } = new List<SeatResponse>();
    }

    public sealed class PublicPlayerDto
    {
        public int HandCount { get; set; }
        public string Opening { get; set; }
        public int Penalties { get; set; }
    }

    public sealed class DrawReceiptDto
    {
        public string Source { get; set; }
        public int Tile { get; set; }
        public bool WasLastStock { get; set; }
    }

    public sealed class ScoreDto
    {
        public bool HasScore { get; set; }
        public int N { get; set; }
        public int J { get; set; }
        public int P { get; set; }
        public int B { get; set; }
        public int M { get; set; }
        public int? Total { get; set; }
    }

    public sealed class RoundResultDto
    {
        public string Reason { get; set; }
        public int? Winner { get; set; }
        public bool DiscardedRealJoker { get; set; }
        public List<ScoreDto> Scores { get; set; } = new List<ScoreDto>();
    }

    /// <summary>One seat's private projection of the authoritative round: only its own hand is revealed.</summary>
    public sealed class PlayerViewDto
    {
        public Guid RoundId { get; set; }
        public long Version { get; set; }
        public int Seat { get; set; }
        public int ActiveSeat { get; set; }
        /// <summary>"awaitingDraw", "planningTurn", "finished", "cancelled".</summary>
        public string Phase { get; set; }
        public List<int> Hand { get; set; } = new List<int>();
        public string Opening { get; set; }
        public int Penalties { get; set; }
        public int Indicator { get; set; }
        public int StockCount { get; set; }
        public List<PublicPlayerDto> Players { get; set; } = new List<PublicPlayerDto>();
        public List<MeldDto> Table { get; set; } = new List<MeldDto>();
        public List<List<int>> Discards { get; set; } = new List<List<int>>();
        public DrawReceiptDto PendingDraw { get; set; }
        public int SeriesTilesUsed { get; set; }
        public RoundResultDto Result { get; set; }
    }

    /// <summary><c>kind</c>: "draw", "return", "shortOpening", "commit", "timeout"; <c>tile</c> is public only for discards and discard-pile draws.</summary>
    public sealed class MatchEventDto
    {
        public string Kind { get; set; }
        public int Seat { get; set; }
        public int? Tile { get; set; }
    }

    public sealed class MatchStateMessage
    {
        public Guid MatchId { get; set; }
        public int HandIndex { get; set; }
        public PlayerViewDto View { get; set; }
        public MatchEventDto LastEvent { get; set; }
        public DateTimeOffset? TurnDeadline { get; set; }
        public List<SeatResponse> Seats { get; set; } = new List<SeatResponse>();
        public List<int> Totals { get; set; } = new List<int>();
    }

    public sealed class HandEndMessage
    {
        public Guid MatchId { get; set; }
        public int HandIndex { get; set; }
        public RoundResultDto Result { get; set; }
        public List<int> Totals { get; set; } = new List<int>();
        public List<long> Xp { get; set; } = new List<long>();
        public DateTimeOffset? NextHandAt { get; set; }
    }

    public sealed class PlacementDto
    {
        public int Seat { get; set; }
        public int Place { get; set; }
        public int Total { get; set; }
        public long ChipDelta { get; set; }
        public int? LpDelta { get; set; }
        public long Xp { get; set; }
        public bool Left { get; set; }
    }

    public sealed class MatchEndMessage
    {
        public Guid MatchId { get; set; }
        public List<PlacementDto> Placements { get; set; } = new List<PlacementDto>();
        public bool Aborted { get; set; }
    }
}
