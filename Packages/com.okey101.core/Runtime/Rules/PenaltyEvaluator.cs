using System;
using System.Collections.Generic;
using Okey101.Core.Model;

namespace Okey101.Core.Rules
{
    public static class PenaltyEvaluator
    {
        public static int GetDiscardPenalty(RoundState afterActions, TileId discard, bool isWinningDiscard)
        {
            if (afterActions == null) throw new ArgumentNullException(nameof(afterActions));
            var player = afterActions.Players[afterActions.ActivePlayer];
            var owned = false;
            foreach (var id in player.Hand) if (id == discard) { owned = true; break; }
            if (!owned) throw new ArgumentException("Discard must be in the active player's hand.", nameof(discard));
            if (isWinningDiscard) return 0;

            var indicator = TileCatalog.Create()[afterActions.Indicator.Value].PrintedFace;
            if (!indicator.HasValue) throw new ArgumentException("Indicator must have a printed face.", nameof(afterActions));
            if (TileCatalog.IsRealJoker(discard, indicator.Value)) return afterActions.Options.OkeyDiscardPenalty ? 101 : 0;
            if (!afterActions.Options.PlayableDiscardPenalty) return 0;
            if (player.Opening == OpeningKind.None ||
                (player.Opening == OpeningKind.Pairs && afterActions.SeriesTilesUsed >= 2)) return 0;

            var face = TileCatalog.GetNonJokerFace(discard, indicator.Value);
            foreach (var meld in afterActions.Table)
            {
                if (meld.Group.Kind == MeldKind.Pair) continue;
                var tiles = new List<PlacedTile>(meld.Group.Tiles) { new PlacedTile(discard, face) };
                if (MeldValidator.Validate(new MeldProposal(meld.Group.Kind, tiles), indicator.Value).Success)
                    return 101;
            }
            return 0;
        }
    }
}
