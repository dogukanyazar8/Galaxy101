using System.Collections.Generic;
using Okey101.Core.Model;

namespace Okey101.Core.Rules
{
    public static class MeldValidator
    {
        /// <summary>Validates group membership and declared joker faces without changing tile order.</summary>
        public static RuleResult<int> Validate(MeldProposal group, TileFace indicator)
        {
            if (!indicator.IsValid) return RuleResult<int>.Fail(ErrorCode.InvalidState);
            if (group == null) return RuleResult<int>.Fail(ErrorCode.InvalidGroup);
            var count = group.Tiles.Count;
            if ((group.Kind == MeldKind.Pair && count != 2) ||
                (group.Kind == MeldKind.Run && (count < 3 || count > 13)) ||
                (group.Kind == MeldKind.Set && (count < 3 || count > 4)))
                return RuleResult<int>.Fail(ErrorCode.InvalidGroup);

            var ids = new HashSet<TileId>();
            var total = 0;
            foreach (var tile in group.Tiles)
            {
                if (!tile.RepresentedFace.IsValid || !ids.Add(tile.Id))
                    return RuleResult<int>.Fail(ErrorCode.InvalidTile);
                if (!TileCatalog.IsRealJoker(tile.Id, indicator) &&
                    TileCatalog.GetNonJokerFace(tile.Id, indicator) != tile.RepresentedFace)
                    return RuleResult<int>.Fail(ErrorCode.InvalidTile);
                total += tile.RepresentedFace.Number;
            }

            bool valid;
            switch (group.Kind)
            {
                case MeldKind.Run: valid = IsRun(group.Tiles); break;
                case MeldKind.Set: valid = IsSet(group.Tiles); break;
                case MeldKind.Pair:
                    valid = group.Tiles[0].RepresentedFace == group.Tiles[1].RepresentedFace;
                    break;
                default: valid = false; break;
            }
            return valid ? RuleResult<int>.Ok(total) : RuleResult<int>.Fail(ErrorCode.InvalidGroup);
        }

        private static bool IsRun(IReadOnlyList<PlacedTile> tiles)
        {
            var color = tiles[0].RepresentedFace.Color;
            var seen = new bool[14];
            var minimum = 13;
            var maximum = 1;
            foreach (var tile in tiles)
            {
                var face = tile.RepresentedFace;
                if (face.Color != color || seen[face.Number]) return false;
                seen[face.Number] = true;
                if (face.Number < minimum) minimum = face.Number;
                if (face.Number > maximum) maximum = face.Number;
            }
            return maximum - minimum + 1 == tiles.Count;
        }

        private static bool IsSet(IReadOnlyList<PlacedTile> tiles)
        {
            var number = tiles[0].RepresentedFace.Number;
            var seen = new bool[4];
            foreach (var tile in tiles)
            {
                var face = tile.RepresentedFace;
                if (face.Number != number || seen[(int)face.Color]) return false;
                seen[(int)face.Color] = true;
            }
            return true;
        }
    }
}
