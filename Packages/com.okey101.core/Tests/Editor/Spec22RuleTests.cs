using System;
using System.Linq;
using NUnit.Framework;
using Okey101.Core.Model;
using Okey101.Core.Rules;
using Okey101.Core.Turns;
using static Okey101.Core.Tests.TableTest;
using static Okey101.Core.Tests.TurnTest;

namespace Okey101.Core.Tests
{
    /// <summary>v3 rules: the deal ceremony (spec/21) and finishing from the hand (elden bitiş, spec/22).</summary>
    public sealed class Spec22RuleTests
    {
        [Test]
        public void EveryDealUsesAll106TilesOnceAndTheRightHandPlayerHas22([Range(1u, 400u, 37u)] uint seed)
        {
            var plan = DealCeremony.Plan(seed, null);
            var all = plan.Hands.SelectMany(h => h).Concat(plan.Stock).Concat(new[] { plan.Indicator }).ToList();
            Assert.That(all.Count, Is.EqualTo(106)); Assert.That(all.Distinct().Count(), Is.EqualTo(106));
            int dealer = plan.Info.Dealer, right = (dealer + 1) % 4;
            for (int k = 0; k < 4; k++) Assert.That(plan.Hands[k].Count, Is.EqualTo(k == right ? 22 : 21), "seat " + k);
            Assert.That(plan.Stock.Count, Is.EqualTo(20));
            Assert.That(plan.StartingPlayer, Is.EqualTo(right), "the right-hand player starts");
            Assert.That(TileCatalog.Create()[plan.Indicator.Value].PrintedFace.HasValue, Is.True, "a fake okey never stays the indicator");
            Assert.That(plan.Info.DieA, Is.InRange(1, 6)); Assert.That(plan.Info.DieB, Is.InRange(1, 6));
            Assert.That(plan.Info.IndicatorStack, Is.EqualTo(plan.Info.DieA - 1));
            Assert.That(plan.Info.ExtraStack, Is.EqualTo(plan.Info.DieA % 15), "the extra tile goes on stack A + 1");
        }

        [Test]
        public void TheIndicatorIsTheBthTileFromTheBottomOfStackAOrTheOneAboveAFakeOkey()
        {
            for (uint seed = 1; seed < 3000; seed++)
            {
                var plan = DealCeremony.Plan(seed, 0);
                var deck = ShuffledDeck(seed);
                var stack = deck.Skip((plan.Info.DieA - 1) * 7).Take(7).ToArray();
                int b = plan.Info.DieB - 1;
                bool fake = !TileCatalog.Create()[stack[b].Value].PrintedFace.HasValue;
                Assert.That(plan.Info.FakeOkeyShown, Is.EqualTo(fake));
                Assert.That(plan.Indicator, Is.EqualTo(fake ? stack[b + 1] : stack[b]), "seed " + seed);
                if (fake) return; // the fake case was covered at least once
            }
            Assert.Fail("No seed below 3000 showed a fake okey first.");
        }

        [TestCase(1320u)]
        [TestCase(2666u)]
        [TestCase(3271u)]
        public void TwoFakeOkeysInARowNeverBecomeTheIndicator(uint seed)
        {
            var plan = DealCeremony.Plan(seed, 0);
            var stack = ShuffledDeck(seed).Skip((plan.Info.DieA - 1) * 7).Take(7).ToArray();
            Assert.That(plan.Info.FakeOkeyShown, Is.True);
            Assert.That(TileCatalog.Create()[plan.Indicator.Value].PrintedFace.HasValue, Is.True);
            Assert.That(plan.Indicator, Is.EqualTo(stack[plan.Info.IndicatorPosition]));
            Assert.DoesNotThrow(() => RoundFactory.Create(seed, Guid.NewGuid(), 0));
        }

        [Test]
        public void ARandomStartDescribesTheDiceItWasDealtWith()
        {
            for (uint seed = 1; seed < 200; seed++)
            {
                var plan = DealCeremony.Plan(seed, null);
                var described = DealCeremony.Describe(RoundFactory.Create(seed, Guid.NewGuid(), null));
                Assert.That((described.Dealer, described.DieA, described.DieB, described.IndicatorPosition),
                    Is.EqualTo((plan.Info.Dealer, plan.Info.DieA, plan.Info.DieB, plan.Info.IndicatorPosition)), "seed " + seed);
            }
        }

        [Test]
        public void StacksAreHandedOutFromDieAAndTheStockIsTheTowerThenTheIndicatorStack()
        {
            var plan = DealCeremony.Plan(77, 2); // starter 2 → dealer 1
            Assert.That(plan.Info.Dealer, Is.EqualTo(1));
            var deck = ShuffledDeck(77); int a = plan.Info.DieA;
            TileId[] Stack(int k) => deck.Skip(DealCeremony.StackOf(a, k) * 7).Take(7).ToArray();
            // Opposite of the dealer (j = 1) takes S(2), S(6), S(10).
            var opposite = DealCeremony.Receiver(1, 1);
            Assert.That(plan.Hands[opposite], Is.EquivalentTo(Stack(2).Concat(Stack(6)).Concat(Stack(10))));
            // First draws come from the top of S(13).
            Assert.That(plan.Stock.Take(7), Is.EqualTo(Stack(13).Reverse()));
            Assert.That(plan.Stock.Skip(7).Take(7), Is.EqualTo(Stack(14).Reverse()));
        }

        [Test]
        public void RoundsAndViewsCarryTheCeremony()
        {
            var state = RoundFactory.Create(91, Guid.NewGuid(), 3);
            var plan = DealCeremony.Plan(91, 3);
            Assert.That(state.StartingPlayer, Is.EqualTo(3)); Assert.That(state.Indicator, Is.EqualTo(plan.Indicator));
            Assert.That(state.Stock, Is.EqualTo(plan.Stock));
            for (int k = 0; k < 4; k++)
            {
                var view = PlayerViewProjector.ForPlayer(state, k);
                Assert.That(view.Deal.Dealer, Is.EqualTo(2)); Assert.That(view.Deal.DieA, Is.EqualTo(plan.Info.DieA));
                Assert.That(view.OwnHand, Is.EquivalentTo(plan.Hands[k]));
            }
        }

        [Test]
        public void ANotOpenedPlayerFinishesFromTheHandBelow101AndIsPaidDouble()
        {
            // 14 tiles in valid sets worth 3+4+5 + 1+2+3 + 6+6+6 + 2+3+4+5+6 = 56 < 101, plus one tile to discard.
            var groups = new[]
            {
                Run(TileColor.Red, 3, 4, 5), Run(TileColor.Blue, 1, 2, 3),
                new MeldProposal(MeldKind.Set, new[] { Normal(TileColor.Red, 6, 1), Normal(TileColor.Blue, 6), Normal(TileColor.Yellow, 6) }),
                Run(TileColor.Yellow, 9, 10, 11, 12, 13),
            };
            var hand = Ids(groups).Concat(new[] { Spare }).ToArray();
            var state = Context(Builder(hand).Build(), new DrawReceipt(DrawSource.Stock, hand[0], false));
            var finished = Pass(state, new TurnPlan(new TableAction[] { new OpenGroups(OpeningKind.Series, groups) }, Spare, false));
            Assert.That(finished.Players[0].Hand, Is.Empty, "everything laid, the last tile thrown");

            // Without finishing, the same sets are a short opening.
            var notAll = Context(Builder(hand.Concat(new[] { Id(TileColor.Black, 2) }).ToArray()).Build(), new DrawReceipt(DrawSource.Stock, hand[0], false));
            Fail(notAll, new TurnPlan(new TableAction[] { new OpenGroups(OpeningKind.Series, groups) }, Spare, false), ErrorCode.InvalidOpening);

            // Scoring: opened and finished this turn → ×2: the finisher −202, an opponent who never opened 202 × 2.
            var finish = new FinishInfo(FinishReason.DiscardWin, 0, OpeningKind.Series, true, false);
            Assert.That(Scoring.ScoreCalculator.Calculate(OpeningKind.Series, true, 0, 0, 0, finish).Total, Is.EqualTo(-202));
            Assert.That(Scoring.ScoreCalculator.Calculate(OpeningKind.None, false, 40, 0, 101, finish).Total, Is.EqualTo(404 + 101), "penalties are not doubled");
        }

        private static TileId[] ShuffledDeck(uint seed)
        {
            var random = new Okey101.Core.Random.SeededRandom(seed);
            var deck = TileCatalog.Create().Select(t => t.Id).ToArray();
            for (var i = deck.Length - 1; i > 0; i--) { var j = random.NextInt(i + 1); var v = deck[i]; deck[i] = deck[j]; deck[j] = v; }
            return deck;
        }
    }
}
