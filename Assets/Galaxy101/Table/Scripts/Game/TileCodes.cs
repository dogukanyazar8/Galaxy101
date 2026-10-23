using System;
using System.Collections.Generic;
using System.Linq;
using Okey101.Core.Model;
using Okey101.Core.Rules;

namespace Galaxy101.Table
{
    /// <summary>Engine tile ids → face-atlas codes and rack heuristics.</summary>
    public static class TileCodes
    {
        // TileColor order is Red, Yellow, Blue, Black; the atlas and the prototype sort order are R, B, Y, K.
        private static readonly string[] Letters = { "R", "Y", "B", "K" };
        private static readonly int[] SortOrder = { 0, 2, 1, 3 };
        private static readonly IReadOnlyList<Tile> Catalog = TileCatalog.Create();

        public static TileFace? Printed(int id) => Catalog[id].PrintedFace;

        /// <summary>"R7", "K13" or "joker" for the two false okeys (printed with the okey symbol).</summary>
        public static string Code(int id)
        {
            var face = Printed(id);
            return face.HasValue ? Letters[(int)face.Value.Color] + face.Value.Number : "joker";
        }

        /// <summary>Rack heuristics see printed faces; a false okey stands in for the okey face, the real okey is wild (null).</summary>
        public static RackGrid.FaceOf FaceFor(TileFace indicator) => id =>
        {
            if (TileCatalog.IsRealJoker(new TileId(id), indicator)) return null;
            var face = TileCatalog.GetNonJokerFace(new TileId(id), indicator);
            return (SortOrder[(int)face.Color], face.Number);
        };

        public static TileFace IndicatorFace(TileId indicator) => Catalog[indicator.Value].PrintedFace.Value;
    }

    /// <summary>
    /// Opening meter (spec/10 §5): every gap-free rack segment that forms a valid run or set counts; segments of two equal
    /// tiles are pairs. A segment's group is built directly — every tile used, okeys on the missing faces — and checked by
    /// the engine's validator, so the groups can be committed.
    /// </summary>
    public sealed class HandEvaluation
    {
        public readonly List<MeldProposal> Groups = new List<MeldProposal>();
        public readonly List<MeldProposal> Pairs = new List<MeldProposal>();
        public readonly HashSet<int> ValidTiles = new HashSet<int>();
        public int Sum;

        public static HandEvaluation Of(RackGrid rack, TileFace indicator)
        {
            var result = new HandEvaluation();
            foreach (var segment in rack.Segments())
            {
                var ids = segment.Ids.Select(i => new TileId(i)).ToArray();
                if (ids.Length >= 3)
                {
                    var best = Whole(ids, indicator, out int bestValue);
                    if (best != null) { result.Groups.Add(best); result.Sum += bestValue; foreach (var id in segment.Ids) result.ValidTiles.Add(id); }
                }
                else if (ids.Length == 2)
                {
                    var pair = MeldCandidateGenerator.Generate(ids, indicator, MeldKind.Pair).FirstOrDefault(p => p.Tiles.Count == 2);
                    if (pair != null) result.Pairs.Add(pair);
                }
            }
            return result;
        }

        /// <summary>
        /// The highest-scoring run (else set) using every tile, as the engine's candidate order would pick it: the highest
        /// start, okeys in id order on the missing numbers / lowest missing colours. Null when the tiles form neither.
        /// </summary>
        private static MeldProposal Whole(TileId[] ids, TileFace indicator, out int value)
        {
            value = -1;
            var jokers = ids.Where(id => TileCatalog.IsRealJoker(id, indicator)).OrderBy(id => id.Value).ToArray();
            var naturals = ids.Where(id => !TileCatalog.IsRealJoker(id, indicator)).Select(id => new PlacedTile(id, TileCatalog.GetNonJokerFace(id, indicator))).ToArray();
            if (naturals.Length == 0) return null;
            MeldProposal best = null;
            var color = naturals[0].RepresentedFace.Color; int number = naturals[0].RepresentedFace.Number, length = ids.Length;
            if (length <= 13 && naturals.All(t => t.RepresentedFace.Color == color) && naturals.Select(t => t.RepresentedFace.Number).Distinct().Count() == naturals.Length)
            {
                int low = naturals.Min(t => t.RepresentedFace.Number), high = naturals.Max(t => t.RepresentedFace.Number), start = Math.Min(low, 14 - length);
                if (start >= 1 && start + length - 1 >= high)
                {
                    var tiles = new PlacedTile[length]; int next = 0;
                    for (int k = 0; k < length; k++)
                    {
                        var face = new TileFace(color, start + k);
                        int at = Array.FindIndex(naturals, t => t.RepresentedFace == face);
                        tiles[k] = at >= 0 ? naturals[at] : new PlacedTile(jokers[next++], face);
                    }
                    best = Checked(new MeldProposal(MeldKind.Run, tiles), indicator, ref value);
                }
            }
            if (length <= 4 && naturals.All(t => t.RepresentedFace.Number == number) && naturals.Select(t => t.RepresentedFace.Color).Distinct().Count() == naturals.Length)
            {
                var missing = Enumerable.Range(0, 4).Select(c => (TileColor)c).Where(c => naturals.All(t => t.RepresentedFace.Color != c)).Take(jokers.Length).ToArray();
                var tiles = naturals.Concat(missing.Select((c, k) => new PlacedTile(jokers[k], new TileFace(c, number)))).OrderBy(t => (int)t.RepresentedFace.Color).ToArray();
                int setValue = -1; var set = Checked(new MeldProposal(MeldKind.Set, tiles), indicator, ref setValue);
                if (set != null && setValue > value) { best = set; value = setValue; }
            }
            return best;
        }

        private static MeldProposal Checked(MeldProposal group, TileFace indicator, ref int value)
        {
            var result = MeldValidator.Validate(group, indicator);
            if (!result.Success) return null;
            value = result.Value; return group;
        }
    }
}
