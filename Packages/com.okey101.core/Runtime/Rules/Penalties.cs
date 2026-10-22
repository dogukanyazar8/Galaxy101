using Okey101.Core.Model;

namespace Okey101.Core.Rules
{
    /// <summary>Penalty amounts (spec/17 §3).</summary>
    public static class Penalties
    {
        /// <summary>Playable tile, okey discard, short opening, okey taken from your meld.</summary>
        public const int Standard = 101;
        public const int FedMultiplier = 10;

        /// <summary>Opening with the previous player's discard: tile value × 10; a real okey adds nothing (it cost +101 when thrown).</summary>
        public static int Fed(TileId tile, TileFace indicator)
            => TileCatalog.IsRealJoker(tile, indicator) ? 0 : TileCatalog.GetNonJokerFace(tile, indicator).Number * FedMultiplier;
    }
}
