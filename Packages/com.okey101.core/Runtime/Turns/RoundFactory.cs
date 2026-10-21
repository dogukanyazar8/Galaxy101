using System;
using System.Collections.Generic;
using System.Linq;
using Okey101.Core.Model;
using Okey101.Core.Random;

namespace Okey101.Core.Turns
{
    public static class RoundFactory
    {
        public static RoundState Create(uint seed, Guid roundId, int? startingPlayer)
        {
            if (roundId == Guid.Empty) throw new ArgumentException("A round identity is required.", nameof(roundId));
            if (startingPlayer.HasValue && (startingPlayer.Value < 0 || startingPlayer.Value > 3))
                throw new ArgumentOutOfRangeException(nameof(startingPlayer));
            var random = new SeededRandom(seed);
            var indicator = new TileId(random.NextInt(104));
            var deck = TileCatalog.Create().Where(t => t.Id != indicator).Select(t => t.Id).ToArray();
            for (var i = deck.Length - 1; i > 0; i--)
            {
                var index = random.NextInt(i + 1);
                var value = deck[i]; deck[i] = deck[index]; deck[index] = value;
            }
            var starter = startingPlayer ?? random.NextInt(4);
            var hands = Enumerable.Range(0, 4).Select(_ => new List<TileId>()).ToArray();
            var cursor = 0;
            for (var round = 0; round < 21; round++)
                for (var offset = 0; offset < 4; offset++)
                    hands[(starter + offset) % 4].Add(deck[cursor++]);
            hands[starter].Add(deck[cursor++]);
            var players = hands.Select(hand => new PlayerState(hand, OpeningKind.None, 0)).ToArray();
            var discards = Enumerable.Range(0, 4).Select(_ => (IReadOnlyList<TileId>)Array.Empty<TileId>()).ToArray();
            var state = new RoundState(roundId, 0, seed, starter, starter, 0, RoundPhase.PlanningTurn,
                indicator, deck.Skip(cursor).ToArray(), players, Array.Empty<Meld>(), discards, null,
                OpeningKind.None, 0, true, new long[4]);
            if (!StateInvariant.Validate(state).Success) throw new InvalidOperationException("Round factory produced invalid tile ownership.");
            return state;
        }
    }
}
