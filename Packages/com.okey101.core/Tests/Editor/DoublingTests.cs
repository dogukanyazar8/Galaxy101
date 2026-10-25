using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Okey101.Core.Model;
using Okey101.Core.Rules;
using Okey101.Core.Turns;
using static Okey101.Core.Tests.EngineTest;
using static Okey101.Core.Tests.TableTest;
using static Okey101.Core.Tests.TurnTest;

namespace Okey101.Core.Tests
{
    /// <summary>
    /// Katlamalı (docs/rules/101-v1.md "Katlamalı"): within a hand each later opener must beat the opener before them —
    /// series 101 → previous + 1, pairs 5 → previous + 1 (no cap). The two bars are independent; in pairs mode a
    /// partner's opening raises the bar unless the table turned that off.
    /// </summary>
    public sealed class DoublingTests
    {
        private static readonly RuleOptions Doubling = new RuleOptions(true, true, doubling: true);
        private static readonly RuleOptions PartnerOff = new RuleOptions(true, true, doubling: true, partnerOpeningsRaise: false);

        // 36 + 30 + 40 = 106.
        private static MeldProposal[] Series106() => new[]
        {
            Run(TileColor.Red, 11, 12, 13), Run(TileColor.Blue, 9, 10, 11), Run(TileColor.Yellow, 6, 7, 8, 9, 10)
        };
        private static MeldProposal[] PairsOf(int count) => Enumerable.Range(1, count).Select(n => Pair(TileColor.Red, n)).ToArray();

        private static RoundState Seated(MeldProposal[] groups, RuleOptions rules, params (int Seat, OpeningKind Kind, int Value)[] opened)
        {
            var builder = Builder(Ids(groups).Concat(new[] { Spare }).ToArray()).WithRules(rules);
            foreach (var o in opened) builder.WithOpening(o.Seat, o.Kind, o.Value);
            return builder.Build();
        }

        [Test]
        public void ClassicKeepsTheBarAt101AndFivePairs()
        {
            Accept(Seated(OpeningGroups(101), RuleOptions.Standard, (1, OpeningKind.Series, 140)), new OpenGroups(OpeningKind.Series, OpeningGroups(101)));
            Accept(Seated(PairsOf(5), RuleOptions.Standard, (1, OpeningKind.Pairs, 9)), new OpenGroups(OpeningKind.Pairs, PairsOf(5)));
        }

        [Test]
        public void TheFirstOpenerNeeds101OrFivePairs()
        {
            var state = Seated(OpeningGroups(101), Doubling);
            Assert.That(TableEvaluator.OpeningBar(state, 0, OpeningKind.Series), Is.EqualTo(101));
            Assert.That(TableEvaluator.OpeningBar(state, 0, OpeningKind.Pairs), Is.EqualTo(5));
            Accept(state, new OpenGroups(OpeningKind.Series, OpeningGroups(101)));
        }

        [TestCase(105, true)]
        [TestCase(106, false)]
        public void ALaterSeriesOpeningMustBeatThePreviousOne(int previous, bool valid)
        {
            var state = Seated(Series106(), Doubling, (1, OpeningKind.Series, previous));
            Assert.That(TableEvaluator.OpeningBar(state, 0, OpeningKind.Series), Is.EqualTo(previous + 1));
            var action = new OpenGroups(OpeningKind.Series, Series106());
            if (valid) Accept(state, action);
            else Reject(state, ErrorCode.InvalidOpening, action);
        }

        [Test]
        public void TheBarKeepsRisingWithEachOpener()
        {
            var state = Seated(Series106(), Doubling, (1, OpeningKind.Series, 101), (2, OpeningKind.Series, 102), (3, OpeningKind.Series, 106));
            Assert.That(TableEvaluator.OpeningBar(state, 0, OpeningKind.Series), Is.EqualTo(107));
            Reject(state, ErrorCode.InvalidOpening, new OpenGroups(OpeningKind.Series, Series106()));
        }

        [TestCase(5, 5, false)]
        [TestCase(5, 6, true)]
        [TestCase(6, 6, false)]
        [TestCase(6, 7, true)]
        public void ALaterPairsOpeningNeedsOneMorePair(int previous, int count, bool valid)
        {
            var state = Seated(PairsOf(count), Doubling, (3, OpeningKind.Pairs, previous));
            var action = new OpenGroups(OpeningKind.Pairs, PairsOf(count));
            if (valid) Accept(state, action);
            else Reject(state, ErrorCode.InvalidOpening, action);
        }

        [Test]
        public void ThePairsBarHasNoCeiling()
        {
            var state = Seated(PairsOf(5), Doubling, (1, OpeningKind.Pairs, 5), (2, OpeningKind.Pairs, 6), (3, OpeningKind.Pairs, 9));
            Assert.That(TableEvaluator.OpeningBar(state, 0, OpeningKind.Pairs), Is.EqualTo(10));
        }

        [Test]
        public void SeriesAndPairsBarsAreIndependent()
        {
            // Only series openings so far: the first pairs opener still needs five pairs, and the other way round.
            Accept(Seated(PairsOf(5), Doubling, (1, OpeningKind.Series, 140)), new OpenGroups(OpeningKind.Pairs, PairsOf(5)));
            Accept(Seated(OpeningGroups(101), Doubling, (1, OpeningKind.Pairs, 7)), new OpenGroups(OpeningKind.Series, OpeningGroups(101)));
            // A pairs opening after one pairs opener needs six even with series openers around.
            var state = Seated(PairsOf(5), Doubling, (1, OpeningKind.Series, 140), (2, OpeningKind.Pairs, 5));
            Reject(state, ErrorCode.InvalidOpening, new OpenGroups(OpeningKind.Pairs, PairsOf(5)));
        }

        [Test]
        public void APartnersOpeningRaisesTheBarByDefault()
        {
            var state = Seated(OpeningGroups(101), Doubling, (2, OpeningKind.Series, 101));
            Assert.That(TableEvaluator.OpeningBar(state, 0, OpeningKind.Series), Is.EqualTo(102));
            Reject(state, ErrorCode.InvalidOpening, new OpenGroups(OpeningKind.Series, OpeningGroups(101)));
        }

        [Test]
        public void WithThePartnerOptionOffOnlyTheOtherTeamRaisesTheBar()
        {
            var partner = Seated(OpeningGroups(101), PartnerOff, (2, OpeningKind.Series, 130));
            Assert.That(TableEvaluator.OpeningBar(partner, 0, OpeningKind.Series), Is.EqualTo(101));
            Accept(partner, new OpenGroups(OpeningKind.Series, OpeningGroups(101)));
            var opponent = Seated(OpeningGroups(101), PartnerOff, (2, OpeningKind.Series, 130), (1, OpeningKind.Series, 101));
            Assert.That(TableEvaluator.OpeningBar(opponent, 0, OpeningKind.Series), Is.EqualTo(102));
            Reject(opponent, ErrorCode.InvalidOpening, new OpenGroups(OpeningKind.Series, OpeningGroups(101)));
        }

        [Test]
        public void TheOpeningValueIsRecorded()
        {
            var series = Accept(Seated(Series106(), Doubling), new OpenGroups(OpeningKind.Series, Series106()));
            Assert.That(series.Players[0].OpeningValue, Is.EqualTo(106));
            var pairs = Accept(Seated(PairsOf(6), RuleOptions.Standard), new OpenGroups(OpeningKind.Pairs, PairsOf(6)));
            Assert.That(pairs.Players[0].OpeningValue, Is.EqualTo(6));
        }

        [Test]
        public void LaterGroupsDoNotChangeTheOpeningValue()
        {
            var opened = Accept(Seated(Series106().Concat(new[] { Run(TileColor.Black, 2, 3, 4) }).ToArray(), Doubling),
                new OpenGroups(OpeningKind.Series, Series106()), new AddGroup(Run(TileColor.Black, 2, 3, 4)));
            Assert.That(opened.Players[0].OpeningValue, Is.EqualTo(106));
        }

        [Test]
        public void FinishingFromTheHandStillIgnoresTheBar()
        {
            // Fifteen tiles in groups (sum 42) and one to throw: elden bitiş opens below any bar.
            var groups = new[]
            {
                Run(TileColor.Red, 1, 2, 3), Run(TileColor.Blue, 1, 2, 3), Run(TileColor.Yellow, 1, 2, 3),
                Run(TileColor.Black, 2, 3, 4), Run(TileColor.Red, 4, 5, 6)
            };
            Accept(Seated(groups, Doubling, (1, OpeningKind.Series, 140)), new OpenGroups(OpeningKind.Series, groups));
        }

        [Test]
        public void TheOpeningValueSurvivesDrawsDiscardsAndPenalties()
        {
            var okey = Id(TileColor.Black, 1);
            var state = new Okey101.TestSupport.RoundTestBuilder().WithIndicator(Id(TileColor.Black, 13))
                .WithHand(0, okey, Id(TileColor.Red, 4), Spare).WithOpening(0, OpeningKind.Series, 120)
                .WithHand(1, Id(TileColor.Blue, 4)).WithOpening(1, OpeningKind.Pairs, 6)
                .WithHand(2, Id(TileColor.Yellow, 4)).WithHand(3, Id(TileColor.Red, 5))
                .WithRules(Doubling).WithTurn(0, RoundPhase.AwaitingDraw).Build();
            var drawn = GameEngine.Apply(state, new DrawCommand(Meta(state), DrawSource.Stock), CancellationToken.None);
            Assert.That(drawn.Success, Is.True, drawn.Error.ToString());
            Assert.That(drawn.Value.Players[0].OpeningValue, Is.EqualTo(120));
            // Throwing the okey costs +101: the player is rebuilt with the penalty and must keep the opening value.
            var thrown = GameEngine.Apply(drawn.Value, new CommitTurnCommand(Meta(drawn.Value), Plan(okey, true)), CancellationToken.None);
            Assert.That(thrown.Success, Is.True, thrown.Error.ToString());
            Assert.That(thrown.Value.Players[0].Penalties, Is.EqualTo(101));
            Assert.That(thrown.Value.Players[0].OpeningValue, Is.EqualTo(120));
            Assert.That(thrown.Value.Players[1].OpeningValue, Is.EqualTo(6));
            Assert.That(thrown.Value.Options.Doubling, Is.True);
        }

        [Test]
        public void TheGiverKeepsTheirOpeningValueWhenFedTenfold()
        {
            var picked = Id(TileColor.Yellow, 10);
            var hand = Ids(Series106()).Where(id => id != picked).Concat(new[] { Spare }).ToArray();
            var before = Builder(hand).WithHand(3, Id(TileColor.Red, 5)).WithOpening(3, OpeningKind.Series, 105)
                .WithHand(1, Id(TileColor.Blue, 4)).WithHand(2, Id(TileColor.Yellow, 4))
                .WithDiscard(3, picked).WithRules(Doubling).WithTurn(0, RoundPhase.AwaitingDraw).Build();
            var drawn = GameEngine.Apply(before, new DrawCommand(Meta(before), DrawSource.PreviousDiscard), CancellationToken.None);
            Assert.That(drawn.Success, Is.True, drawn.Error.ToString());
            var plan = new TurnPlan(new TableAction[] { new OpenGroups(OpeningKind.Series, Series106()) }, Spare, false);
            var opened = GameEngine.Apply(drawn.Value, new CommitTurnCommand(Meta(drawn.Value), plan), CancellationToken.None);
            Assert.That(opened.Success, Is.True, opened.Error.ToString());
            Assert.That(opened.Value.Players[3].Penalties, Is.EqualTo(100), "Yellow 10 fed the opening: × 10");
            Assert.That(opened.Value.Players[3].OpeningValue, Is.EqualTo(105));
            Assert.That(opened.Value.Players[0].OpeningValue, Is.EqualTo(106));
        }

        [Test]
        public void ViewsCarryOpeningValuesBothWays()
        {
            var state = Seated(OpeningGroups(101), PartnerOff, (1, OpeningKind.Series, 117), (2, OpeningKind.Pairs, 6));
            var view = PlayerViewProjector.ForPlayer(state, 0);
            Assert.That(view.Players[1].OpeningValue, Is.EqualTo(117));
            Assert.That(view.Players[2].OpeningValue, Is.EqualTo(6));
            Assert.That(view.Options.Doubling, Is.True);
            Assert.That(view.Options.PartnerOpeningsRaise, Is.False);
            Assert.That(TableEvaluator.OpeningBar(view, OpeningKind.Series), Is.EqualTo(118), "the opponent's 117");
            Assert.That(TableEvaluator.OpeningBar(view, OpeningKind.Pairs), Is.EqualTo(5), "the partner's six pairs do not count");
            var rebuilt = PlayerViewProjector.ForEvaluation(view);
            Assert.That(rebuilt.Success, Is.True, rebuilt.Error.ToString());
            Assert.That(rebuilt.Value.Players[1].OpeningValue, Is.EqualTo(117));
            Assert.That(TableEvaluator.OpeningBar(rebuilt.Value, 0, OpeningKind.Series), Is.EqualTo(118));
        }

        [Test]
        public void TheDiscardFinderUsesTheRaisedBar()
        {
            var picked = Id(TileColor.Yellow, 10);
            var hand = Ids(Series106()).Where(id => id != picked).Concat(new[] { Spare }).ToArray();
            RoundState Before(int previous) => Builder(hand).WithHand(1, Id(TileColor.Blue, 4)).WithOpening(1, OpeningKind.Series, previous)
                .WithHand(2, Id(TileColor.Yellow, 4)).WithHand(3, Id(TileColor.Red, 5))
                .WithDiscard(3, picked).WithRules(Doubling).WithTurn(0, RoundPhase.AwaitingDraw).Build();
            Assert.That(DiscardTurnFinder.Find(PlayerViewProjector.ForPlayer(Before(105), 0), CancellationToken.None).Success, Is.True);
            Assert.That(DiscardTurnFinder.Find(PlayerViewProjector.ForPlayer(Before(106), 0), CancellationToken.None).Error, Is.EqualTo(ErrorCode.NoLegalUse));
        }

        [Test]
        public void DoublingIsNotTheStandardRuleSet()
        {
            Assert.That(Doubling.IsStandard, Is.False);
            Assert.That(RuleOptions.Standard.Doubling, Is.False);
            Assert.That(RuleOptions.Standard.PartnerOpeningsRaise, Is.True);
        }
    }
}
