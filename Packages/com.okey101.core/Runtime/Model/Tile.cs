using System;
using System.Collections.Generic;

namespace Okey101.Core.Model
{
    public enum TileColor { Red, Yellow, Blue, Black }

    public readonly struct TileId : IEquatable<TileId>, IComparable<TileId>
    {
        public int Value { get; }
        public TileId(int value)
        {
            if (value < 0 || value > 105) throw new ArgumentOutOfRangeException(nameof(value));
            Value = value;
        }
        public bool Equals(TileId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is TileId other && Equals(other);
        public override int GetHashCode() => Value;
        public int CompareTo(TileId other) => Value.CompareTo(other.Value);
        public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        public static bool operator ==(TileId left, TileId right) => left.Equals(right);
        public static bool operator !=(TileId left, TileId right) => !left.Equals(right);
    }

    public readonly struct TileFace : IEquatable<TileFace>, IComparable<TileFace>
    {
        public TileColor Color { get; }
        public int Number { get; }
        public bool IsValid => Number >= 1 && Number <= 13 && (int)Color >= 0 && (int)Color <= 3;
        public TileFace(TileColor color, int number)
        {
            Immutable.EnumValue(color, nameof(color));
            if (number < 1 || number > 13) throw new ArgumentOutOfRangeException(nameof(number));
            Color = color;
            Number = number;
        }
        public bool Equals(TileFace other) => Color == other.Color && Number == other.Number;
        public override bool Equals(object obj) => obj is TileFace other && Equals(other);
        public override int GetHashCode() => ((int)Color * 13) + Number;
        public int CompareTo(TileFace other)
        {
            var color = Color.CompareTo(other.Color);
            return color != 0 ? color : Number.CompareTo(other.Number);
        }
        public override string ToString() => Color + "/" + Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        public static bool operator ==(TileFace left, TileFace right) => left.Equals(right);
        public static bool operator !=(TileFace left, TileFace right) => !left.Equals(right);
    }

    public sealed class Tile
    {
        public TileId Id { get; }
        public TileFace? PrintedFace { get; }
        internal Tile(TileId id, TileFace? face) { Id = id; PrintedFace = face; }
    }

    public static class TileCatalog
    {
        private static readonly IReadOnlyList<Tile> Tiles = Build();
        public static IReadOnlyList<Tile> Create() => Tiles;
        public static TileFace JokerFace(TileFace indicator)
        {
            if (!indicator.IsValid) throw new ArgumentException("Invalid indicator face.", nameof(indicator));
            return new TileFace(indicator.Color, indicator.Number == 13 ? 1 : indicator.Number + 1);
        }
        public static bool IsRealJoker(TileId id, TileFace indicator)
            => Tiles[id.Value].PrintedFace == JokerFace(indicator);

        public static TileFace GetNonJokerFace(TileId id, TileFace indicator)
        {
            var joker = JokerFace(indicator);
            var face = Tiles[id.Value].PrintedFace;
            if (face == joker) throw new InvalidOperationException("Real joker requires an explicit represented face.");
            return face ?? joker;
        }

        private static IReadOnlyList<Tile> Build()
        {
            var tiles = new Tile[106];
            for (var color = 0; color < 4; color++)
                for (var number = 1; number <= 13; number++)
                    for (var copy = 0; copy < 2; copy++)
                    {
                        var id = 2 * (13 * color + number - 1) + copy;
                        tiles[id] = new Tile(new TileId(id), new TileFace((TileColor)color, number));
                    }
            tiles[104] = new Tile(new TileId(104), null);
            tiles[105] = new Tile(new TileId(105), null);
            return Array.AsReadOnly(tiles);
        }
    }
}
