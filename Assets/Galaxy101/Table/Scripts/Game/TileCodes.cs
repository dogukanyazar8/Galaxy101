using System.Collections.Generic;
using Okey101.Core.V4;

namespace Galaxy101.Table
{
    /// <summary>The table's int tile ids (<see cref="TileIds"/>) → face-atlas codes and rack heuristics, on the v4 engine's faces.</summary>
    public static class TileCodes
    {
        /// <summary>The atlas and the prototype's sort order: R, B, Y, K.</summary>
        private const string SortOrder = "RBYK";

        /// <summary>"R7", "K13" or "joker" for the two fake okeys (printed with the okey symbol).</summary>
        public static string Code(int id) => TileIds.Code(id);

        /// <summary>Rack heuristics see printed faces; a fake okey stands in for the okey's face, the okey itself is wild (null).</summary>
        public static RackGrid.FaceOf FaceFor(TableClient model) => id =>
        {
            var face = model?.FaceOf(TileIds.ToV4(id));
            if (face == null) return null;
            return (SortOrder.IndexOf(face.Value.C, System.StringComparison.Ordinal), face.Value.N);
        };
    }

    /// <summary>The rack's proposals (<see cref="RackMelds"/>) on the rack grid's segments, with the int ids the board uses.</summary>
    public sealed class RackEval
    {
        public RackMelds Melds;
        public readonly HashSet<int> ValidTiles = new HashSet<int>();
        public int Sum => Melds.Sum;
        public List<MeldSpec> Groups => Melds.Groups;
        public List<MeldSpec> Pairs => Melds.Pairs;

        public static RackEval Of(RackGrid rack, TableClient model)
        {
            var segments = new List<IList<string>>();
            foreach (var seg in rack.Segments()) segments.Add(TileIds.ToV4s(seg.Ids));
            var okey = model != null && model.OkeyKnown ? new ColorNum(model.OkeyColor, model.OkeyNumber) : null;
            var e = new RackEval { Melds = RackMelds.Of(segments, okey) };
            foreach (var id in e.Melds.Valid) e.ValidTiles.Add(TileIds.ToInt(id));
            return e;
        }
    }
}
