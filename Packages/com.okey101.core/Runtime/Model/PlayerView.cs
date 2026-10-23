using System;
using System.Collections.Generic;

namespace Okey101.Core.Model
{
    public sealed class PublicPlayerInfo
    {
        public int HandCount { get; }
        public OpeningKind Opening { get; }
        public int Penalties { get; }
        public PublicPlayerInfo(int handCount, OpeningKind opening, int penalties)
        {
            if (handCount < 0 || handCount > 22) throw new ArgumentOutOfRangeException(nameof(handCount));
            if (penalties < 0) throw new ArgumentOutOfRangeException(nameof(penalties));
            Immutable.EnumValue(opening, nameof(opening));
            HandCount = handCount; Opening = opening; Penalties = penalties;
        }
    }

    public sealed class PlayerView
    {
        public Guid RoundId { get; }
        public long Version { get; }
        public int Actor { get; }
        public int ActivePlayer { get; }
        public RoundPhase Phase { get; }
        public IReadOnlyList<TileId> OwnHand { get; }
        public OpeningKind OwnOpening { get; }
        public int OwnPenalties { get; }
        public long OwnLastSequence { get; }
        public TileId Indicator { get; }
        public int StockCount { get; }
        public IReadOnlyList<PublicPlayerInfo> Players { get; }
        public IReadOnlyList<Meld> Table { get; }
        public IReadOnlyList<IReadOnlyList<TileId>> Discards { get; }
        public DrawReceipt PendingDraw { get; }
        public int SeriesTilesUsed { get; }
        public RoundResult Result { get; }
        /// <summary>Public deal facts (dealer, dice, stacks) for the deal ceremony; null for hand-made test states.</summary>
        public DealInfo Deal { get; }
        /// <summary>The room's switchable rules, public to every seat.</summary>
        public RuleOptions Options { get; }

        public PlayerView(Guid roundId, long version, int actor, int activePlayer, RoundPhase phase,
            IReadOnlyList<TileId> ownHand, OpeningKind ownOpening, int ownPenalties,
            long ownLastSequence, TileId indicator, int stockCount, IReadOnlyList<PublicPlayerInfo> players,
            IReadOnlyList<Meld> table, IReadOnlyList<IReadOnlyList<TileId>> discards,
            DrawReceipt pendingDraw, int seriesTilesUsed, RoundResult result = null, DealInfo deal = null, RuleOptions options = null)
        {
            Options = options ?? RuleOptions.Standard;
            if (roundId == Guid.Empty) throw new ArgumentException("A round identity is required.", nameof(roundId));
            if (version < 0 || ownLastSequence < 0) throw new ArgumentOutOfRangeException(nameof(version));
            if (ownPenalties < 0 || seriesTilesUsed < 0) throw new ArgumentOutOfRangeException(nameof(ownPenalties));
            if (stockCount < 0 || stockCount > 105) throw new ArgumentOutOfRangeException(nameof(stockCount));
            Immutable.Player(actor, nameof(actor)); Immutable.Player(activePlayer, nameof(activePlayer));
            Immutable.EnumValue(phase, nameof(phase)); Immutable.EnumValue(ownOpening, nameof(ownOpening));
            if (pendingDraw != null && actor != activePlayer) throw new ArgumentException("An observer cannot receive another player's private draw.", nameof(pendingDraw));
            RoundId = roundId; Version = version; Actor = actor; ActivePlayer = activePlayer; Phase = phase;
            OwnHand = Immutable.Copy(ownHand, nameof(ownHand)); OwnOpening = ownOpening;
            OwnPenalties = ownPenalties; OwnLastSequence = ownLastSequence; Indicator = indicator;
            StockCount = stockCount; Players = Immutable.Copy(players, nameof(players));
            Table = Immutable.Copy(table, nameof(table)); Discards = Immutable.CopyNested(discards, nameof(discards));
            if (Players.Count != 4 || Discards.Count != 4) throw new ArgumentException("Four public player entries and discard piles are required.");
            PendingDraw = pendingDraw; SeriesTilesUsed = seriesTilesUsed; Result = result; Deal = deal;
        }
    }
}
