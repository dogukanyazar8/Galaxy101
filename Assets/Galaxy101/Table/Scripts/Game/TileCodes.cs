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
    /// tiles are pairs. Joker bindings come from the engine's own candidate generator, so the groups can be committed.
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
                    MeldProposal best = null; int bestValue = -1;
                    foreach (var kind in new[] { MeldKind.Run, MeldKind.Set })
                    {
                        if (kind == MeldKind.Set && ids.Length > 4) continue;
                        foreach (var proposal in MeldCandidateGenerator.Generate(ids, indicator, kind))
                        {
                            if (proposal.Tiles.Count != ids.Length) continue;
                            var value = MeldValidator.Validate(proposal, indicator);
                            if (value.Success && value.Value > bestValue) { best = proposal; bestValue = value.Value; }
                        }
                    }
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
    }
}
