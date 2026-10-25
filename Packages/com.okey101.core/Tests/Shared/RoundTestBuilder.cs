using System;
using System.Collections.Generic;
using System.Linq;
using Okey101.Core.Model;
using Okey101.Core.Turns;

namespace Okey101.TestSupport
{
    public sealed class RoundTestBuilder
    {
        private readonly List<TileId>[] hands = Enumerable.Range(0, 4).Select(_ => new List<TileId>()).ToArray();
        private readonly OpeningKind[] openings = new OpeningKind[4];
        private readonly int[] openingValues = new int[4];
        private readonly List<Meld> table = new List<Meld>();
        private readonly List<TileId>[] discards = Enumerable.Range(0, 4).Select(_ => new List<TileId>()).ToArray();
        private TileId indicator = new TileId(24);
        private int activePlayer;
        private RoundPhase phase = RoundPhase.PlanningTurn;

        public RoundTestBuilder WithIndicator(TileId tile) { indicator = tile; return this; }
        public RoundTestBuilder WithHand(int actor, params TileId[] tiles)
        {
            CheckPlayer(actor); hands[actor] = new List<TileId>(tiles); return this;
        }
        public RoundTestBuilder WithOpening(int actor, OpeningKind kind, int value = 0)
        {
            CheckPlayer(actor); openings[actor] = kind; openingValues[actor] = value; return this;
        }
        public RoundTestBuilder WithTable(params Meld[] groups) { table.Clear(); table.AddRange(groups); return this; }
        public RoundTestBuilder WithDiscard(int actor, params TileId[] tiles)
        {
            CheckPlayer(actor); discards[actor] = new List<TileId>(tiles); return this;
        }
        public RoundTestBuilder WithTurn(int actor, RoundPhase roundPhase)
        {
            CheckPlayer(actor); activePlayer = actor; phase = roundPhase; return this;
        }
        private RuleOptions options;
        public RoundTestBuilder WithRules(RuleOptions rules) { options = rules; return this; }

        public RoundState Build()
        {
            var occupied = new[] { indicator }.Concat(hands.SelectMany(h => h))
                .Concat(table.SelectMany(m => m.Group.Tiles.Select(t => t.Id)))
                .Concat(discards.SelectMany(d => d)).ToArray();
            if (occupied.Distinct().Count() != occupied.Length) throw new InvalidOperationException("Fixture places the same physical tile twice.");
            var used = new HashSet<TileId>(occupied);
            var stock = TileCatalog.Create().Select(t => t.Id).Where(id => !used.Contains(id)).ToArray();
            var players = Enumerable.Range(0, 4).Select(p => new PlayerState(hands[p], openings[p], 0, openingValues[p])).ToArray();
            var state = new RoundState(Guid.NewGuid(), 0, 1, activePlayer, activePlayer, 0, phase,
                indicator, stock, players, table, discards, null, openings[activePlayer], 0,
                phase == RoundPhase.PlanningTurn, new long[4], options: options);
            if (!StateInvariant.Validate(state).Success) throw new InvalidOperationException("Fixture violates physical tile ownership.");
            return state;
        }
        private static void CheckPlayer(int actor)
        {
            if (actor < 0 || actor > 3) throw new ArgumentOutOfRangeException(nameof(actor));
        }
    }
}
