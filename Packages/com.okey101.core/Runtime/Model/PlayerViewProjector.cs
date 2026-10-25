using System;
using System.Collections.Generic;
using System.Linq;

namespace Okey101.Core.Model
{
    public static class PlayerViewProjector
    {
        public static PlayerView ForPlayer(RoundState state, int actor)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            Immutable.Player(actor, nameof(actor));
            var players = new PublicPlayerInfo[4];
            for (var player = 0; player < players.Length; player++)
            {
                var source = state.Players[player];
                players[player] = new PublicPlayerInfo(source.Hand.Count, source.Opening, source.Penalties, source.OpeningValue);
            }
            var own = state.Players[actor];
            return new PlayerView(state.RoundId, state.Version, actor, state.ActivePlayer, state.Phase,
                own.Hand, own.Opening, own.Penalties, state.LastAcceptedSequences[actor], state.Indicator,
                state.Stock.Count, players, state.Table, state.Discards,
                actor == state.ActivePlayer ? state.PendingDraw : null, state.SeriesTilesUsed, state.Result, Turns.DealCeremony.Describe(state), state.Options,
                state.PreviousDiscardReturned);
        }

        public static RuleResult<RoundState> ForEvaluation(PlayerView snapshot)
        {
            if (snapshot == null || !TileCatalog.Create()[snapshot.Indicator.Value].PrintedFace.HasValue)
                return RuleResult<RoundState>.Fail(ErrorCode.InvalidState);
            if (snapshot.OwnHand.Count != snapshot.Players[snapshot.Actor].HandCount || snapshot.OwnOpening != snapshot.Players[snapshot.Actor].Opening
                || snapshot.OwnPenalties != snapshot.Players[snapshot.Actor].Penalties)
                return RuleResult<RoundState>.Fail(ErrorCode.InvalidState);
            var known = new HashSet<TileId>();
            if (!known.Add(snapshot.Indicator)) return RuleResult<RoundState>.Fail(ErrorCode.InvalidState);
            foreach (var id in snapshot.OwnHand.Concat(snapshot.Table.SelectMany(m => m.Group.Tiles.Select(t => t.Id))).Concat(snapshot.Discards.SelectMany(d => d)))
                if (!known.Add(id)) return RuleResult<RoundState>.Fail(ErrorCode.InvalidState);
            var unknown = TileCatalog.Create().Select(t => t.Id).Where(id => !known.Contains(id)).ToArray();
            var players = new PlayerState[4]; var cursor = 0;
            for (var p = 0; p < 4; p++)
            {
                var publicPlayer = snapshot.Players[p];
                var held = p == snapshot.Actor ? snapshot.OwnHand : unknown.Skip(cursor).Take(publicPlayer.HandCount).ToArray();
                if (p != snapshot.Actor) cursor += publicPlayer.HandCount;
                players[p] = new PlayerState(held, publicPlayer.Opening, publicPlayer.Penalties, publicPlayer.OpeningValue);
            }
            if (cursor + snapshot.StockCount != unknown.Length) return RuleResult<RoundState>.Fail(ErrorCode.InvalidState);
            // Canonical unknown allocation satisfies physical invariants only. It is never
            // exposed, used for scoring/decisions about opponents, or installed in LocalSession.
            var sequences = new long[4]; sequences[snapshot.Actor] = snapshot.OwnLastSequence;
            return RuleResult<RoundState>.Ok(new RoundState(snapshot.RoundId, snapshot.Version, 0, snapshot.Actor, snapshot.ActivePlayer, 0,
                snapshot.Phase, snapshot.Indicator, unknown.Skip(cursor).ToArray(), players, snapshot.Table, snapshot.Discards,
                snapshot.PendingDraw, snapshot.OwnOpening, snapshot.SeriesTilesUsed, snapshot.PendingDraw == null, sequences, options: snapshot.Options));
        }
    }
}
