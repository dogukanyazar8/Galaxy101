using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Okey101.Core.Model;
using Okey101.Core.Rules;
using static Okey101.Core.Tests.TableTest;
using static Okey101.Core.Tests.TurnTest;

namespace Okey101.Core.Tests
{
    public sealed class DiscardTurnFinderTests
    {
        [Test]
        public void UnopenedMustUseDiscardInOpening()
        {
            var picked = Id(TileColor.Red, 4);
            var before = Waiting(new[] { Spare }, picked)
                .WithTable(new Meld(0, 1, Run(TileColor.Red, 1, 2, 3))).Build();
            Assert.That(Find(before).Error, Is.EqualTo(ErrorCode.NoLegalUse));
            var groups = OpeningGroups(101);
            before = Waiting(Ids(groups).Concat(new[] { Spare }).ToArray(), picked)
                .WithTable(new Meld(0, 1, Run(TileColor.Red, 1, 2, 3))).Build();
            Assert.That(Find(before).Error, Is.EqualTo(ErrorCode.NoLegalUse));
        }

        [Test]
        public void SwapWitnessMayKeepRecoveredJoker()
        {
            var before = Waiting(new[] { Spare }, Id(TileColor.Red, 2), OpeningKind.Series)
                .WithTable(JokerRun()).Build();
            var plan = Witness(before);
            Assert.That(plan.Actions.Any(a => a is SwapJoker), Is.True);
            var next = Pass(Draw(before), plan);
            Assert.That(next.Players[0].Hand.Contains(Id(TileColor.Black, 1)), Is.True);
        }

        [Test]
        public void FindsMultiActionPathWithoutHeuristicCutoff()
        {
            var before = Waiting(new[] { Id(TileColor.Red, 2), Id(TileColor.Blue, 8), Spare },
                Id(TileColor.Blue, 6), OpeningKind.Series).WithTable(JokerRun()).Build();
            var plan = Witness(before);
            Assert.That(plan.Actions.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(plan.Actions[0], Is.TypeOf<SwapJoker>());
            Assert.That(plan.Actions.OfType<AddGroup>().Any(g => g.Group.Tiles.Any(t => t.Id == Id(TileColor.Blue, 6))), Is.True);
        }

        [Test]
        public void ExploresAlternativeJokerBindingsBeforeThreeStepWitness()
        {
            var before = Waiting(new[] { Id(TileColor.Red, 2), Spare }, Id(TileColor.Blue, 10), OpeningKind.Series)
                .WithTable(JokerRun(), new Meld(1, 1, Run(TileColor.Blue, 6, 7, 8))).Build();
            var plan = Witness(before);
            Assert.That(plan.Actions.Count, Is.EqualTo(3));
            Assert.That(plan.Actions[0], Is.TypeOf<SwapJoker>());
            var extensions = plan.Actions.OfType<ExtendMeld>().ToArray();
            Assert.That(extensions[0].Tiles[0].Id, Is.EqualTo(Id(TileColor.Black, 1)));
            Assert.That(extensions[0].Tiles[0].RepresentedFace, Is.EqualTo(new TileFace(TileColor.Blue, 9)));
            Assert.That(extensions[1].Tiles[0].Id, Is.EqualTo(Id(TileColor.Blue, 10)));
        }

        [Test]
        public void Dense22TileOpeningRetainsPhysicalAndJokerChoices()
        {
            var hand = Enumerable.Range(1, 10).SelectMany(n => new[] { Id(TileColor.Red, n), Id(TileColor.Red, n, 1) })
                .Concat(new[] { Id(TileColor.Black, 1) }).ToArray();
            var before = Waiting(hand, Id(TileColor.Black, 1, 1)).Build();
            var timer = System.Diagnostics.Stopwatch.StartNew();
            Witness(before);
            TestContext.WriteLine("Dense 22-tile exact search: " + timer.ElapsedMilliseconds + " ms");
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void PreviousSeatIsRelativeToActivePlayer(int actor)
        {
            var owner = (actor + 1) % 4;
            var before = new Okey101.TestSupport.RoundTestBuilder().WithIndicator(Id(TileColor.Black, 13))
                .WithHand(actor, Spare).WithOpening(actor, OpeningKind.Series).WithOpening(owner, OpeningKind.Series)
                .WithTurn(actor, RoundPhase.AwaitingDraw).WithDiscard((actor + 3) % 4, Id(TileColor.Red, 4))
                .WithTable(new Meld(5, owner, Run(TileColor.Red, 1, 2, 3))).Build();
            Witness(before);
        }

        [Test]
        public void TwoRealJokersCanSupplyFifthPairInOpening()
        {
            var pairs = new[] { Pair(TileColor.Red, 2), Pair(TileColor.Red, 4), Pair(TileColor.Red, 6), Pair(TileColor.Red, 8) };
            var before = Waiting(Ids(pairs).Concat(new[] { Id(TileColor.Black, 1), Spare }).ToArray(),
                Id(TileColor.Black, 1, 1)).Build();
            var plan = Witness(before);
            Assert.That(plan.Actions.OfType<OpenGroups>().First().Opening, Is.EqualTo(OpeningKind.Pairs));
        }

        [Test]
        public void FindsSeriesOpeningContainingExactDiscard()
        {
            var groups = OpeningGroups(101);
            var picked = Id(TileColor.Red, 11);
            var before = Waiting(Ids(groups).Where(id => id != picked).Concat(new[] { Spare }).ToArray(), picked).Build();
            var plan = Witness(before);
            var opening = plan.Actions.OfType<OpenGroups>().First();
            Assert.That(opening.Opening, Is.EqualTo(OpeningKind.Series));
            Assert.That(Ids(opening.Groups.ToArray()).Contains(picked), Is.True);
        }

        [TestCase(100, false)]
        [TestCase(101, true)]
        public void SearchOpeningThresholdIsExact(int total, bool legal)
        {
            var groups = OpeningGroups(total);
            var picked = groups[2].Tiles[0].Id;
            var before = Waiting(Ids(groups).Where(id => id != picked).Concat(new[] { Spare }).ToArray(), picked).Build();
            if (legal) Witness(before);
            else Assert.That(Find(before).Error, Is.EqualTo(ErrorCode.NoLegalUse));
        }

        [TestCase(1, false)]
        [TestCase(2, true)]
        public void OpeningRespectsRemainingGroupIdentitySpace(int slots, bool legal)
        {
            var groups = new[] { Run(TileColor.Red, 4, 5, 6, 7, 8, 9, 10, 11), Run(TileColor.Blue, 7, 8, 9, 10, 11) };
            var picked = groups[0].Tiles[0].Id;
            var before = Waiting(Ids(groups).Where(id => id != picked).Concat(new[] { Spare }).ToArray(), picked)
                .WithTable(new Meld(int.MaxValue - slots, 1, Run(TileColor.Black, 2, 3, 4))).Build();
            if (legal) Witness(before);
            else Assert.That(Find(before).Error, Is.EqualTo(ErrorCode.NoLegalUse));
        }

        [Test]
        public void PickedFakeJokerCanReplaceMatchingRealJoker()
        {
            var group = new MeldProposal(MeldKind.Run,
                new[] { Joker(0, TileColor.Black, 1), Normal(TileColor.Black, 2), Normal(TileColor.Black, 3) });
            var before = Waiting(new[] { Spare }, new TileId(104), OpeningKind.Series)
                .WithTable(new Meld(0, 1, group)).Build();
            var plan = Witness(before);
            Assert.That(plan.Actions.OfType<SwapJoker>().Single().ReplacementId, Is.EqualTo(new TileId(104)));
        }

        [Test]
        public void PickedRealJokerGetsExplicitBindingAndStaysOnTable()
        {
            var picked = Id(TileColor.Black, 1);
            var before = Waiting(new[] { Spare }, picked, OpeningKind.Series)
                .WithTable(new Meld(0, 1, Run(TileColor.Red, 1, 2, 3))).Build();
            var plan = Witness(before);
            Assert.That(plan.Actions.OfType<ExtendMeld>().Single().Tiles[0].RepresentedFace,
                Is.EqualTo(new TileFace(TileColor.Red, 4)));
            Assert.That(plan.Discard, Is.Not.EqualTo(picked));
        }

        [Test]
        public void FindsFivePairOpeningWithout101Points()
        {
            var groups = Enumerable.Range(1, 5).Select(n => Pair(TileColor.Red, n)).ToArray();
            var picked = Id(TileColor.Red, 5, 1);
            var before = Waiting(Ids(groups).Where(id => id != picked).Concat(new[] { Spare }).ToArray(), picked).Build();
            var plan = Witness(before);
            Assert.That(plan.Actions.OfType<OpenGroups>().First().Opening, Is.EqualTo(OpeningKind.Pairs));
        }

        [Test]
        public void SearchCannotSubstituteDiscardTwin()
        {
            var picked = Id(TileColor.Red, 4);
            var before = Waiting(new[] { Id(TileColor.Red, 4, 1), Spare }, picked, OpeningKind.Series)
                .WithTable(new Meld(0, 1, Run(TileColor.Red, 1, 2, 3))).Build();
            var next = Pass(Draw(before), Witness(before));
            Assert.That(next.Table.SelectMany(m => m.Group.Tiles).Any(t => t.Id == picked), Is.True);
        }

        [Test]
        public void PlanMustLeaveOneTileToDiscard()
        {
            var before = Waiting(new[] { Id(TileColor.Red, 1), Id(TileColor.Red, 2) },
                Id(TileColor.Red, 3), OpeningKind.Series).Build();
            Assert.That(Find(before).Error, Is.EqualTo(ErrorCode.NoLegalUse));
        }

        [TestCase(1, true)]
        [TestCase(2, false)]
        public void SearchRespectsRemainingPairBudget(int used, bool legal)
        {
            var before = TurnTest.Context(Waiting(new[] { Spare }, Id(TileColor.Red, 4), OpeningKind.Pairs)
                .WithTable(new Meld(0, 1, Run(TileColor.Red, 1, 2, 3))).Build(), null, used);
            if (legal) Witness(before);
            else Assert.That(Find(before).Error, Is.EqualTo(ErrorCode.NoLegalUse));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SearchRespectsPairAreaPermission(bool pairArea)
        {
            var before = Waiting(new[] { Id(TileColor.Red, 1), Spare }, Id(TileColor.Red, 1, 1), OpeningKind.Series)
                .WithOpening(1, pairArea ? OpeningKind.Pairs : OpeningKind.Series).Build();
            if (pairArea) Witness(before);
            else Assert.That(Find(before).Error, Is.EqualTo(ErrorCode.NoLegalUse));
        }

        [Test]
        public void CancelIsNotProofOfIllegality()
        {
            var before = Waiting(new[] { Spare }, Id(TileColor.Red, 4), OpeningKind.Series).Build();
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                var result = DiscardTurnFinder.Find(PlayerViewProjector.ForPlayer(before, 0), cancellation.Token);
                Assert.That(result.Error, Is.EqualTo(ErrorCode.Cancelled));
            }
        }

        [Test]
        public void CandidateGenerationHonorsCancellation()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Assert.Throws<OperationCanceledException>(() => MeldCandidateGenerator.Generate(
                    new[] { Spare }, new TileFace(TileColor.Black, 13), MeldKind.Run, cancellation.Token));
            }
        }

        [Test]
        public void CancellationAfterGenerationStartsIsObserved()
        {
            using (var cancellation = new CancellationTokenSource())
                Assert.Throws<OperationCanceledException>(() => MeldCandidateGenerator.Generate(
                    new CancellingHand(cancellation), new TileFace(TileColor.Black, 13), MeldKind.Run, cancellation.Token));
        }

        [Test]
        public void OnlyPreviousPlayersTopDiscardIsAvailable()
        {
            var before = Waiting(new[] { Spare }, Id(TileColor.Yellow, 13), OpeningKind.Series)
                .WithDiscard(3, Id(TileColor.Red, 4), Id(TileColor.Yellow, 13))
                .WithDiscard(1, Id(TileColor.Red, 4, 1))
                .WithTable(new Meld(0, 1, Run(TileColor.Red, 1, 2, 3))).Build();
            Assert.That(Find(before).Error, Is.EqualTo(ErrorCode.NoLegalUse));
            var missing = Waiting(new[] { Spare }, Id(TileColor.Red, 4)).WithDiscard(3).Build();
            Assert.That(Find(missing).Error, Is.EqualTo(ErrorCode.PreviousDiscardUnavailable));
        }

        [Test]
        public void PrivateAllocationDoesNotChangeWitness()
        {
            var baseBuilder = Waiting(new[] { Spare }, Id(TileColor.Red, 4), OpeningKind.Series)
                .WithTable(new Meld(0, 1, Run(TileColor.Red, 1, 2, 3)));
            var first = baseBuilder.WithHand(1, Id(TileColor.Yellow, 5)).Build();
            var second = baseBuilder.WithHand(1, Id(TileColor.Yellow, 6)).Build();
            var firstPlan = Witness(first);
            var secondPlan = Witness(second);
            Assert.That(firstPlan.Discard, Is.EqualTo(secondPlan.Discard));
            Assert.That(firstPlan.Actions.Count, Is.EqualTo(secondPlan.Actions.Count));
            Pass(Draw(first), secondPlan);
            Pass(Draw(second), firstPlan);
        }

        [Test]
        public void ProjectionExposesOnlyOwnPrivateDrawAndHand()
        {
            var before = TurnTest.Context(Waiting(new[] { Spare }, Id(TileColor.Red, 4), OpeningKind.Series)
                .WithHand(1, Id(TileColor.Yellow, 5)).Build(), null, 1, 202);
            var drawn = Draw(before, DrawSource.Stock);
            var own = PlayerViewProjector.ForPlayer(drawn, 0);
            var observer = PlayerViewProjector.ForPlayer(drawn, 1);
            Assert.That(own, Is.Not.Null);
            Assert.That(observer, Is.Not.Null);
            Assert.That(own.OwnHand, Is.EqualTo(drawn.Players[0].Hand));
            Assert.That(observer.OwnHand, Is.EqualTo(drawn.Players[1].Hand));
            Assert.That(observer.PendingDraw, Is.Null);
            Assert.That(own.PendingDraw, Is.SameAs(drawn.PendingDraw));
            Assert.That(own.StockCount, Is.EqualTo(drawn.Stock.Count));
            Assert.That(observer.Players[0].HandCount, Is.EqualTo(2));
            Assert.That(observer.Players[0].Opening, Is.EqualTo(OpeningKind.Series));
            Assert.That(own.OwnLastSequence, Is.EqualTo(drawn.LastAcceptedSequences[0]));
            Assert.That(own.OwnPenalties, Is.EqualTo(202));
            Assert.That(own.Version, Is.EqualTo(9));
            Assert.That(own.SeriesTilesUsed, Is.EqualTo(1));
            Assert.That(observer.OwnLastSequence, Is.EqualTo(3));
            Assert.That(observer.Players[0].Penalties, Is.EqualTo(202));
            Assert.Throws<NotSupportedException>(() => ((System.Collections.Generic.IList<TileId>)own.OwnHand).Clear());
        }

        [Test]
        public void MissingOrWrongActorViewAndPhaseAreRejected()
        {
            Assert.That(DiscardTurnFinder.Find(null, CancellationToken.None).Error, Is.EqualTo(ErrorCode.InvalidState));
            var before = Waiting(new[] { Spare }, Id(TileColor.Red, 4), OpeningKind.Series).Build();
            Assert.That(DiscardTurnFinder.Find(PlayerViewProjector.ForPlayer(before, 1), CancellationToken.None).Error,
                Is.EqualTo(ErrorCode.WrongPlayer));
            Assert.That(Find(Draw(before)).Error, Is.EqualTo(ErrorCode.WrongPhase));
            Assert.Throws<ArgumentNullException>(() => PlayerViewProjector.ForPlayer(null, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => PlayerViewProjector.ForPlayer(before, 4));
        }

        [Test]
        public void InconsistentPublicCountsAndRepeatedPhysicalIdsAreRejected()
        {
            var before = Waiting(new[] { Spare }, Id(TileColor.Red, 4), OpeningKind.Series).Build();
            var view = PlayerViewProjector.ForPlayer(before, 0);
            var wrongStock = Copy(view, view.OwnHand, view.Players, view.StockCount + 1);
            Assert.That(DiscardTurnFinder.Find(wrongStock, CancellationToken.None).Error, Is.EqualTo(ErrorCode.InvalidState));
            var players = view.Players.ToArray();
            players[0] = new PublicPlayerInfo(2, view.OwnOpening, view.OwnPenalties);
            var duplicates = Copy(view, new[] { Spare, Spare }, players, view.StockCount - 1);
            Assert.That(DiscardTurnFinder.Find(duplicates, CancellationToken.None).Error, Is.EqualTo(ErrorCode.InvalidState));
        }

        private static PlayerView Copy(PlayerView view, System.Collections.Generic.IReadOnlyList<TileId> hand,
            System.Collections.Generic.IReadOnlyList<PublicPlayerInfo> players, int stockCount)
            => new PlayerView(view.RoundId, view.Version, view.Actor, view.ActivePlayer, view.Phase, hand,
                view.OwnOpening, view.OwnPenalties, view.OwnLastSequence, view.Indicator, stockCount, players,
                view.Table, view.Discards, view.PendingDraw, view.SeriesTilesUsed, view.Result);

        private sealed class CancellingHand : System.Collections.Generic.IReadOnlyList<TileId>
        {
            private readonly CancellationTokenSource cancellation;
            private readonly TileId[] hand = { Id(TileColor.Red, 1), Id(TileColor.Red, 2), Id(TileColor.Red, 3) };
            public CancellingHand(CancellationTokenSource cancellation) { this.cancellation = cancellation; }
            public int Count { get { cancellation.Cancel(); return hand.Length; } }
            public TileId this[int index] => hand[index];
            public System.Collections.Generic.IEnumerator<TileId> GetEnumerator()
                => ((System.Collections.Generic.IEnumerable<TileId>)hand).GetEnumerator();
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private static Meld JokerRun() => new Meld(0, 1, new MeldProposal(MeldKind.Run,
            new[] { Normal(TileColor.Red, 1), Joker(0, TileColor.Red, 2), Normal(TileColor.Red, 3) }));

        private static RuleResult<TurnPlan> Find(RoundState before)
            => DiscardTurnFinder.Find(PlayerViewProjector.ForPlayer(before, before.ActivePlayer), CancellationToken.None);

        private static TurnPlan Witness(RoundState before)
        {
            var snapshot = Fingerprint(before);
            var result = Find(before);
            Assert.That(Fingerprint(before), Is.EqualTo(snapshot));
            Assert.That(result.Success, Is.True, result.Error.ToString());
            Pass(Draw(before), result.Value);
            return result.Value;
        }
    }
}
