using System;
using System.Collections.Generic;

namespace Okey101.Core.Model
{
    public readonly struct CommandMeta
    {
        public Guid RoundId { get; }
        public int Actor { get; }
        public long Sequence { get; }
        public long ExpectedVersion { get; }
        public CommandMeta(Guid roundId, int actor, long sequence, long expectedVersion)
        {
            if (roundId == Guid.Empty) throw new ArgumentException("A round identity is required.", nameof(roundId));
            Immutable.Player(actor, nameof(actor));
            if (sequence <= 0) throw new ArgumentOutOfRangeException(nameof(sequence));
            if (expectedVersion < 0) throw new ArgumentOutOfRangeException(nameof(expectedVersion));
            RoundId = roundId; Actor = actor; Sequence = sequence; ExpectedVersion = expectedVersion;
        }
    }

    public abstract class GameCommand
    {
        public CommandMeta Meta { get; }
        internal GameCommand(CommandMeta meta)
        {
            if (meta.RoundId == Guid.Empty || meta.Sequence <= 0) throw new ArgumentException("Invalid command metadata.", nameof(meta));
            Meta = meta;
        }
    }

    public sealed class DrawCommand : GameCommand
    {
        public DrawSource Source { get; }
        public DrawCommand(CommandMeta meta, DrawSource source) : base(meta)
        {
            Immutable.EnumValue(source, nameof(source));
            Source = source;
        }
    }

    /// <summary>
    /// Put the tile taken from the previous player's discard back on its pile (spec/17 §4). Only before any other
    /// change this turn; the player then draws from the stock and cannot take the same pile again this turn.
    /// </summary>
    public sealed class ReturnDiscardCommand : GameCommand
    {
        public ReturnDiscardCommand(CommandMeta meta) : base(meta) { }
    }

    /// <summary>
    /// A failed opening attempt (series below 101 or fewer than 5 pairs, spec/17 §3): the groups stay on the rack and the
    /// player takes +101. Only while the player has not opened.
    /// </summary>
    public sealed class ShortOpeningCommand : GameCommand
    {
        public OpeningKind Opening { get; }
        public ShortOpeningCommand(CommandMeta meta, OpeningKind opening) : base(meta)
        {
            Immutable.EnumValue(opening, nameof(opening));
            if (opening == OpeningKind.None) throw new ArgumentException("A short opening names its kind.", nameof(opening));
            Opening = opening;
        }
    }

    public sealed class CommitTurnCommand : GameCommand
    {
        public TurnPlan Plan { get; }
        public CommitTurnCommand(CommandMeta meta, TurnPlan plan) : base(meta)
        {
            Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        }
    }

    public sealed class TurnPlan
    {
        public IReadOnlyList<TableAction> Actions { get; }
        public TileId Discard { get; }
        public bool AcceptPenalty { get; }
        public TurnPlan(IReadOnlyList<TableAction> actions, TileId discard, bool acceptPenalty)
        {
            Actions = Immutable.Copy(actions, nameof(actions)); Discard = discard; AcceptPenalty = acceptPenalty;
        }
    }

    public abstract class TableAction { internal TableAction() { } }

    public sealed class OpenGroups : TableAction
    {
        public OpeningKind Opening { get; }
        public IReadOnlyList<MeldProposal> Groups { get; }
        public OpenGroups(OpeningKind opening, IReadOnlyList<MeldProposal> groups)
        {
            Immutable.EnumValue(opening, nameof(opening));
            Opening = opening; Groups = Immutable.Copy(groups, nameof(groups));
        }
    }

    public sealed class AddGroup : TableAction
    {
        public MeldProposal Group { get; }
        public AddGroup(MeldProposal group) { Group = group ?? throw new ArgumentNullException(nameof(group)); }
    }

    public sealed class ExtendMeld : TableAction
    {
        public int MeldId { get; }
        public bool Prepend { get; }
        public IReadOnlyList<PlacedTile> Tiles { get; }
        public ExtendMeld(int meldId, bool prepend, IReadOnlyList<PlacedTile> tiles)
        {
            if (meldId < 0) throw new ArgumentOutOfRangeException(nameof(meldId));
            MeldId = meldId; Prepend = prepend; Tiles = Immutable.Copy(tiles, nameof(tiles));
        }
    }

    public sealed class SwapJoker : TableAction
    {
        public int MeldId { get; }
        public TileId JokerId { get; }
        public TileId ReplacementId { get; }
        public SwapJoker(int meldId, TileId jokerId, TileId replacementId)
        {
            if (meldId < 0) throw new ArgumentOutOfRangeException(nameof(meldId));
            MeldId = meldId; JokerId = jokerId; ReplacementId = replacementId;
        }
    }
}
