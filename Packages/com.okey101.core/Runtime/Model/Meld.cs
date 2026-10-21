using System;
using System.Collections.Generic;

namespace Okey101.Core.Model
{
    public enum OpeningKind { None, Series, Pairs }
    public enum MeldKind { Run, Set, Pair }

    public readonly struct PlacedTile
    {
        public TileId Id { get; }
        public TileFace RepresentedFace { get; }
        public PlacedTile(TileId id, TileFace representedFace)
        {
            if (!representedFace.IsValid) throw new ArgumentException("Invalid represented face.", nameof(representedFace));
            Id = id;
            RepresentedFace = representedFace;
        }
    }

    public sealed class MeldProposal
    {
        public MeldKind Kind { get; }
        public IReadOnlyList<PlacedTile> Tiles { get; }
        public MeldProposal(MeldKind kind, IReadOnlyList<PlacedTile> tiles)
        {
            Immutable.EnumValue(kind, nameof(kind));
            Kind = kind;
            Tiles = Immutable.Copy(tiles, nameof(tiles));
        }
    }

    public sealed class Meld
    {
        public int Id { get; }
        public int Owner { get; }
        public MeldProposal Group { get; }
        public Meld(int id, int owner, MeldProposal group)
        {
            if (id < 0) throw new ArgumentOutOfRangeException(nameof(id));
            Immutable.Player(owner, nameof(owner));
            Id = id;
            Owner = owner;
            Group = group ?? throw new ArgumentNullException(nameof(group));
        }
    }
}
