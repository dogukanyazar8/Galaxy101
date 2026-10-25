using System;
using System.Collections.Generic;

namespace Okey101.Core.Model
{
    public enum RoundPhase { AwaitingDraw, PlanningTurn, Finished, Cancelled }
    public enum DrawSource { Stock, PreviousDiscard }

    public sealed class PlayerState
    {
        public IReadOnlyList<TileId> Hand { get; }
        public OpeningKind Opening { get; }
        public int Penalties { get; }
        /// <summary>The opening's series total or pair count, fixed for the hand (katlamalı bar); 0 before opening.</summary>
        public int OpeningValue { get; }
        public PlayerState(IReadOnlyList<TileId> hand, OpeningKind opening, int penalties, int openingValue = 0)
        {
            Immutable.EnumValue(opening, nameof(opening));
            if (penalties < 0) throw new ArgumentOutOfRangeException(nameof(penalties));
            if (openingValue < 0 || (opening == OpeningKind.None && openingValue != 0)) throw new ArgumentOutOfRangeException(nameof(openingValue));
            Hand = Immutable.Copy(hand, nameof(hand)); Opening = opening; Penalties = penalties; OpeningValue = openingValue;
        }
    }

    public sealed class DrawReceipt
    {
        public DrawSource Source { get; }
        public TileId Tile { get; }
        public bool WasLastStock { get; }
        public DrawReceipt(DrawSource source, TileId tile, bool wasLastStock)
        {
            Immutable.EnumValue(source, nameof(source));
            if (source != DrawSource.Stock && wasLastStock) throw new ArgumentException("Only stock draws can exhaust the stock.");
            Source = source; Tile = tile; WasLastStock = wasLastStock;
        }
    }

    public sealed class RoundState
    {
        public Guid RoundId { get; }
        public long Version { get; }
        public uint Seed { get; }
        public int StartingPlayer { get; }
        public int ActivePlayer { get; }
        public int TurnNumber { get; }
        public RoundPhase Phase { get; }
        public TileId Indicator { get; }
        public IReadOnlyList<TileId> Stock { get; }
        public IReadOnlyList<PlayerState> Players { get; }
        public IReadOnlyList<Meld> Table { get; }
        public IReadOnlyList<IReadOnlyList<TileId>> Discards { get; }
        public DrawReceipt PendingDraw { get; }
        public OpeningKind OpeningAtTurnStart { get; }
        public int SeriesTilesUsed { get; }
        public bool IsInitialTurn { get; }
        public IReadOnlyList<long> LastAcceptedSequences { get; }
        public RoundResult Result { get; }
        /// <summary>The previous discard was taken and put back this turn: only the stock may be drawn now.</summary>
        public bool PreviousDiscardReturned { get; }
        /// <summary>The room's switchable rules (penalties); <see cref="RuleOptions.Standard"/> unless the room changed them.</summary>
        public RuleOptions Options { get; }
        /// <summary>The deal ceremony that produced this round (spec/21); null for hand-built states.</summary>
        public DealInfo Deal { get; }

        public RoundState(Guid roundId, long version, uint seed, int startingPlayer, int activePlayer,
            int turnNumber, RoundPhase phase, TileId indicator, IReadOnlyList<TileId> stock,
            IReadOnlyList<PlayerState> players, IReadOnlyList<Meld> table,
            IReadOnlyList<IReadOnlyList<TileId>> discards, DrawReceipt pendingDraw,
            OpeningKind openingAtTurnStart, int seriesTilesUsed, bool isInitialTurn,
            IReadOnlyList<long> lastAcceptedSequences, RoundResult result = null, bool previousDiscardReturned = false, RuleOptions options = null, DealInfo deal = null)
        {
            Options = options ?? RuleOptions.Standard; Deal = deal;
            if (roundId == Guid.Empty) throw new ArgumentException("A round identity is required.", nameof(roundId));
            if (version < 0) throw new ArgumentOutOfRangeException(nameof(version));
            if (turnNumber < 0) throw new ArgumentOutOfRangeException(nameof(turnNumber));
            if (seriesTilesUsed < 0) throw new ArgumentOutOfRangeException(nameof(seriesTilesUsed));
            Immutable.Player(startingPlayer, nameof(startingPlayer));
            Immutable.Player(activePlayer, nameof(activePlayer));
            Immutable.EnumValue(phase, nameof(phase));
            Immutable.EnumValue(openingAtTurnStart, nameof(openingAtTurnStart));
            RoundId = roundId; Version = version; Seed = seed; StartingPlayer = startingPlayer;
            ActivePlayer = activePlayer; TurnNumber = turnNumber; Phase = phase; Indicator = indicator;
            Stock = Immutable.Copy(stock, nameof(stock)); Players = Immutable.Copy(players, nameof(players));
            Table = Immutable.Copy(table, nameof(table)); Discards = Immutable.CopyNested(discards, nameof(discards));
            LastAcceptedSequences = Immutable.Copy(lastAcceptedSequences, nameof(lastAcceptedSequences));
            if (Players.Count != 4 || Discards.Count != 4 || LastAcceptedSequences.Count != 4)
                throw new ArgumentException("Exactly four player, discard and sequence entries are required.");
            foreach (var sequence in LastAcceptedSequences)
                if (sequence < 0) throw new ArgumentOutOfRangeException(nameof(lastAcceptedSequences));
            PendingDraw = pendingDraw; OpeningAtTurnStart = openingAtTurnStart;
            SeriesTilesUsed = seriesTilesUsed; IsInitialTurn = isInitialTurn; Result = result;
            PreviousDiscardReturned = previousDiscardReturned;
        }
    }
}
