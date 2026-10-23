using System;
using System.Collections.Generic;
using System.Linq;
using Okey101.Core.Model;
using Okey101.Core.Random;

namespace Okey101.Core.Turns
{
    public static class RoundFactory
    {
        public static RoundState Create(uint seed, Guid roundId, int? startingPlayer, RuleOptions options = null)
        {
            if (roundId == Guid.Empty) throw new ArgumentException("A round identity is required.", nameof(roundId));
            if (startingPlayer.HasValue && (startingPlayer.Value < 0 || startingPlayer.Value > 3))
                throw new ArgumentOutOfRangeException(nameof(startingPlayer));
            // spec/21: the dealer sits left of the starter; the deal ceremony decides every hand, the indicator and the stock.
            var plan = DealCeremony.Plan(seed, startingPlayer);
            var starter = plan.StartingPlayer;
            var indicator = plan.Indicator;
            var players = plan.Hands.Select(hand => new PlayerState(hand, OpeningKind.None, 0)).ToArray();
            var discards = Enumerable.Range(0, 4).Select(_ => (IReadOnlyList<TileId>)Array.Empty<TileId>()).ToArray();
            var state = new RoundState(roundId, 0, seed, starter, starter, 0, RoundPhase.PlanningTurn,
                indicator, plan.Stock.ToArray(), players, Array.Empty<Meld>(), discards, null,
                OpeningKind.None, 0, true, new long[4], options: options);
            if (!StateInvariant.Validate(state).Success) throw new InvalidOperationException("Round factory produced invalid tile ownership.");
            return state;
        }
    }
}
