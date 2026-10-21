using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Okey101.Core.Model;
using Okey101.Core.Turns;

namespace Okey101.Core.Tests
{
    public sealed class ModelIsolationTests
    {
        [Test]
        public void InputAndReturnedCollectionsCannotMutateState()
        {
            var original = RoundFactory.Create(1, Guid.NewGuid(), 0);
            var source = original.Stock.ToList();
            var sequences = new long[] { 0, 0, 0, 0 };
            var discardSource = new List<TileId>();
            var discards = new List<IReadOnlyList<TileId>>
            {
                discardSource, Array.Empty<TileId>(), Array.Empty<TileId>(), Array.Empty<TileId>()
            };
            var state = Copy(original, source, discards, sequences);
            var expected = source.ToArray();
            source.Clear();
            discardSource.Add(original.Stock[0]);
            sequences[0] = 99;
            Assert.That(state.Stock, Is.EqualTo(expected));
            Assert.That(state.Discards[0].Count, Is.EqualTo(0));
            Assert.That(state.LastAcceptedSequences[0], Is.EqualTo(0));
            Assert.Throws<NotSupportedException>(() => ((IList<TileId>)state.Stock).Clear());
        }

        [Test]
        public void HandAndMeldInputsAreCopied()
        {
            var hand = new List<TileId> { new TileId(0), new TileId(2), new TileId(4) };
            var player = new PlayerState(hand, OpeningKind.None, 0);
            var tiles = new List<PlacedTile>
            {
                new PlacedTile(new TileId(0), new TileFace(TileColor.Red, 1)),
                new PlacedTile(new TileId(2), new TileFace(TileColor.Red, 2)),
                new PlacedTile(new TileId(4), new TileFace(TileColor.Red, 3))
            };
            var group = new MeldProposal(MeldKind.Run, tiles);
            hand.Clear();
            tiles.Clear();
            Assert.That(player.Hand.Count, Is.EqualTo(3));
            Assert.That(group.Tiles.Count, Is.EqualTo(3));
        }

        [Test]
        public void DuplicatePlacementIsRejected()
        {
            var original = RoundFactory.Create(1, Guid.NewGuid(), 0);
            var stock = original.Stock.ToArray();
            stock[0] = original.Players[0].Hand[0];
            var duplicated = Copy(original, stock, original.Discards, original.LastAcceptedSequences);
            Assert.That(StateInvariant.Validate(duplicated).Success, Is.False);
            Assert.That(StateInvariant.Validate(original).Success, Is.True);
        }

        [Test]
        public void MissingPlacementIsRejected()
        {
            var original = RoundFactory.Create(1, Guid.NewGuid(), 0);
            var missing = Copy(original, original.Stock.Skip(1).ToArray(), original.Discards, original.LastAcceptedSequences);
            Assert.That(StateInvariant.Validate(missing).Success, Is.False);
        }

        private static RoundState Copy(RoundState state, IReadOnlyList<TileId> stock,
            IReadOnlyList<IReadOnlyList<TileId>> discards, IReadOnlyList<long> sequences)
        {
            return new RoundState(state.RoundId, state.Version, state.Seed, state.StartingPlayer,
                state.ActivePlayer, state.TurnNumber, state.Phase, state.Indicator, stock, state.Players,
                state.Table, discards, state.PendingDraw, state.OpeningAtTurnStart, state.SeriesTilesUsed,
                state.IsInitialTurn, sequences, state.Result);
        }
    }
}
