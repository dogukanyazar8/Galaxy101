using System;
using System.Collections.Generic;
using System.Linq;
using Okey101.Core.Model;
using Okey101.Core.Random;

namespace Okey101.Core.Turns
{
    /// <summary>
    /// The real 101 deal (spec/21): the shuffled 106 tiles are laid in front of the dealer as 15 stacks of 7 (stack 1 at
    /// the dealer's left, bottom tile first) plus one extra tile. The dealer rolls twice: die A picks the stack, die B the
    /// tile from its bottom; that tile turns face up as the indicator on top of the same stack (a fake okey there makes the
    /// tile above it the indicator). The extra tile goes on stack A+1. Stacks are handed out from S(k) = (A − 1 + k) mod 15:
    /// right, opposite, left, dealer take S(1 + j) and S(5 + j), then S(9 + j). S(13) goes on S(14) in the middle (drawn
    /// first, from the top), the indicator stack beside it (drawn last). The right-hand player gets the extra tile — 22
    /// tiles — and starts by discarding. The next hand's dealer is the right-hand player.
    /// </summary>
    public static class DealCeremony
    {
        public const int Stacks = 15, StackHeight = 7;

        /// <summary>0-based stack index of S(k).</summary>
        public static int StackOf(int dieA, int k) => ((dieA - 1 + k) % Stacks + Stacks) % Stacks;

        /// <summary>The seat that receives the j-th share (0 right of the dealer, 1 opposite, 2 left, 3 the dealer).</summary>
        public static int Receiver(int dealer, int j) => (dealer + 1 + j) % 4;

        public static DealPlan Plan(uint seed, int? startingPlayer)
        {
            if (startingPlayer.HasValue && (startingPlayer.Value < 0 || startingPlayer.Value > 3)) throw new ArgumentOutOfRangeException(nameof(startingPlayer));
            var random = new SeededRandom(seed);
            var deck = TileCatalog.Create().Select(t => t.Id).ToArray();
            for (var i = deck.Length - 1; i > 0; i--)
            {
                var index = random.NextInt(i + 1);
                var value = deck[i]; deck[i] = deck[index]; deck[index] = value;
            }
            int dealer = startingPlayer.HasValue ? (startingPlayer.Value + 3) % 4 : random.NextInt(4);
            int a = 1 + random.NextInt(6), b = 1 + random.NextInt(6);
            var stacks = new List<TileId>[Stacks];
            for (int s = 0; s < Stacks; s++) stacks[s] = deck.Skip(s * StackHeight).Take(StackHeight).ToList();
            var extra = deck[Stacks * StackHeight];

            int indicatorStack = a - 1, position = b - 1;
            bool fakeShown = !TileCatalog.Create()[stacks[indicatorStack][position].Value].PrintedFace.HasValue;
            if (fakeShown) position += 1; // b ≤ 6, so a tile above always exists
            var indicator = stacks[indicatorStack][position];
            stacks[indicatorStack].RemoveAt(position);
            int extraStack = StackOf(a, 1);
            stacks[extraStack].Add(extra);

            var hands = Enumerable.Range(0, 4).Select(_ => new List<TileId>()).ToArray();
            for (int j = 0; j < 4; j++)
            {
                var seat = Receiver(dealer, j);
                hands[seat].AddRange(stacks[StackOf(a, 1 + j)]);
                hands[seat].AddRange(stacks[StackOf(a, 5 + j)]);
                hands[seat].AddRange(stacks[StackOf(a, 9 + j)]);
            }
            // Draw order: S(13) top → bottom, S(14) top → bottom, then the indicator stack top → bottom.
            var stock = new List<TileId>();
            stock.AddRange(Enumerable.Reverse(stacks[StackOf(a, 13)]));
            stock.AddRange(Enumerable.Reverse(stacks[StackOf(a, 14)]));
            stock.AddRange(Enumerable.Reverse(stacks[indicatorStack]));
            var info = new DealInfo(dealer, a, b, indicatorStack, position, fakeShown, extraStack);
            return new DealPlan(info, indicator, hands.Select(h => (IReadOnlyList<TileId>)h.ToArray()).ToArray(), stock.ToArray());
        }

        /// <summary>The public part of a round's deal (dealer, dice, stacks) for the ceremony on every client.</summary>
        public static DealInfo Describe(RoundState state) => Plan(state.Seed, state.StartingPlayer).Info;
    }

    public sealed class DealPlan
    {
        public DealInfo Info { get; }
        public TileId Indicator { get; }
        public IReadOnlyList<IReadOnlyList<TileId>> Hands { get; }
        public IReadOnlyList<TileId> Stock { get; }
        public int StartingPlayer => (Info.Dealer + 1) % 4;

        public DealPlan(DealInfo info, TileId indicator, IReadOnlyList<IReadOnlyList<TileId>> hands, IReadOnlyList<TileId> stock)
        { Info = info; Indicator = indicator; Hands = hands; Stock = stock; }
    }
}
