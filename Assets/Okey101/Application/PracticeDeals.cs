using System;
using System.Collections.Generic;
using System.Linq;
using Okey101.Core.Model;
using Okey101.Core.Turns;

namespace Okey101.Application
{
#if UNITY_EDITOR || OKEY_DIAGNOSTICS
    // Small, explicit A1 acceptance deals. No file loader, hidden-state UI or bot logic.
    public static class PracticeDeals
    {
        public static RoundState Create(string name)
        {
            var spare = Id(TileColor.Black, 12);
            var opening = OpeningKind.None;
            var otherOpening = OpeningKind.None;
            var table = Array.Empty<Meld>();
            TileId[] hand;
            var source = DrawSource.Stock;
            switch (name)
            {
                case "Series":
                case "Finish":
                    hand = new[] { Run(TileColor.Red, 11, 12, 13), Run(TileColor.Blue, 9, 10, 11), Run(TileColor.Yellow, 5, 6, 7, 8, 9) }
                        .SelectMany(g => g.Tiles.Select(t => t.Id)).Concat(new[] { name == "Finish" ? Id(TileColor.Black, 1) : spare }).ToArray();
                    break;
                case "Pairs":
                    hand = Enumerable.Range(1, 5).SelectMany(n => new[] { Id(TileColor.Red, n), Id(TileColor.Red, n, 1) }).Concat(new[] { spare }).ToArray();
                    break;
                case "Swap":
                    hand = new[] { Id(TileColor.Red, 2), spare, Id(TileColor.Blue, 8) };
                    opening = OpeningKind.Series; otherOpening = OpeningKind.Series; source = DrawSource.PreviousDiscard;
                    table = new[] { new Meld(0, 1, new MeldProposal(MeldKind.Run, new[] { Normal(TileColor.Red, 1),
                        new PlacedTile(Id(TileColor.Black, 1), new TileFace(TileColor.Red, 2)), Normal(TileColor.Red, 3) })) };
                    break;
                case "Penalty":
                    hand = new[] { Id(TileColor.Red, 4), spare };
                    opening = OpeningKind.Series; otherOpening = OpeningKind.Series;
                    table = new[] { new Meld(0, 1, Run(TileColor.Red, 1, 2, 3)) };
                    break;
                default: throw new ArgumentException("Unknown A1 practice deal. Use Series, Pairs, Swap, Finish or Penalty.", nameof(name));
            }
            var indicator = Id(TileColor.Black, 13);
            var used = new HashSet<TileId>(hand.Concat(new[] { indicator }).Concat(table.SelectMany(g => g.Group.Tiles.Select(t => t.Id))));
            var remaining = TileCatalog.Create().Select(t => t.Id).Where(id => !used.Contains(id)).ToArray();
            var players = new PlayerState[4]; players[0] = new PlayerState(hand, opening, 0);
            for (var p = 1; p < 4; p++) players[p] = new PlayerState(remaining.Skip((p - 1) * 21).Take(21).ToArray(), p == 1 ? otherOpening : OpeningKind.None, 0);
            var state = new RoundState(Guid.NewGuid(), 1, 71, 0, 0, 1, RoundPhase.PlanningTurn, indicator, remaining.Skip(63).ToArray(), players, table,
                Enumerable.Range(0, 4).Select(_ => (IReadOnlyList<TileId>)Array.Empty<TileId>()).ToArray(),
                new DrawReceipt(source, hand[0], false), opening, 0, false, new long[4]);
            if (!StateInvariant.ValidateAuthority(state).Success) throw new InvalidOperationException("A1 practice deal violates authority invariants.");
            return state;
        }
        private static TileId Id(TileColor color, int number, int copy = 0) => new TileId(2 * (13 * (int)color + number - 1) + copy);
        private static PlacedTile Normal(TileColor color, int number) => new PlacedTile(Id(color, number), new TileFace(color, number));
        private static MeldProposal Run(TileColor color, params int[] numbers) => new MeldProposal(MeldKind.Run, numbers.Select(n => Normal(color, n)).ToArray());
    }
#endif
}
