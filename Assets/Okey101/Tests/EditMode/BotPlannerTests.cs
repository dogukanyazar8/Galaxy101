using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Okey101.Bots;
using Okey101.Core.Model;
using Okey101.Core.Rules;
using Okey101.Core.Turns;
using Okey101.TestSupport;

namespace Okey101.Application.Tests
{
    public sealed class BotPlannerTests
    {
        [TestCase("Series", OpeningKind.Series)]
        [TestCase("Pairs", OpeningKind.Pairs)]
        public void SeriesAndPairOpeningFixtures(string name, OpeningKind expected)
        {
            var state = PracticeDeals.Create(name);
            var plan = new BotPlanner().PlanTurn(PlayerViewProjector.ForPlayer(state, 0), CancellationToken.None);
            var applied = TurnEvaluator.Evaluate(state, plan);
            Assert.That(applied.Success, Is.True, applied.Error.ToString());
            Assert.That(applied.Value.Players[0].Opening, Is.EqualTo(expected));
            Assert.That(applied.Value.Players[0].Hand, Is.Empty);
        }

        [Test]
        public void FinishesWithDiscardWhenPossible()
        {
            var state = PracticeDeals.Create("Finish");
            var plan = new BotPlanner().PlanTurn(PlayerViewProjector.ForPlayer(state, 0), CancellationToken.None);
            var result = GameEngine.Apply(state, new CommitTurnCommand(Meta(PlayerViewProjector.ForPlayer(state, 0)), plan), CancellationToken.None);
            Assert.That(result.Success, Is.True, result.Error.ToString());
            Assert.That(result.Value.Result, Is.Not.Null);
            Assert.That(result.Value.Result.FinishInfo.Winner, Is.EqualTo(0));
            Assert.That(result.Value.Players[0].Hand, Is.Empty);
            Assert.That(result.Value.Discards[0].Count, Is.EqualTo(1));
            Assert.That(StateInvariant.ValidateAuthority(result.Value).Success, Is.True);
        }

        [Test]
        public void UsesWitnessAndRespectsTwoTileBudget()
        {
            var picked = Id(TileColor.Red, 1);
            var waiting = Builder(new[] { Id(TileColor.Red, 2), Id(TileColor.Red, 3), Spare }, OpeningKind.Series)
                .WithTurn(0, RoundPhase.AwaitingDraw).WithDiscard(3, picked).Build();
            var decision = new BotPlanner().ChooseDraw(PlayerViewProjector.ForPlayer(waiting, 0), CancellationToken.None);
            Assert.That(decision.Source, Is.EqualTo(DrawSource.PreviousDiscard)); Assert.That(decision.Witness, Is.Not.Null);
            var drawn = DrawPrevious(waiting);
            var witness = TurnEvaluator.Evaluate(drawn, decision.Witness);
            Assert.That(witness.Success, Is.True, witness.Error.ToString());
            Assert.That(witness.Value.Table.SelectMany(m => m.Group.Tiles.Select(t => t.Id)), Has.Member(picked));
            var pairs = Builder(new[] { Id(TileColor.Red, 4), Id(TileColor.Red, 5), Id(TileColor.Red, 6), Spare }, OpeningKind.Pairs)
                .WithOpening(1, OpeningKind.Series).WithTable(new Meld(0, 1, Run(TileColor.Red, 1, 2, 3))).Build();
            var plan = new BotPlanner().PlanTurn(PlayerViewProjector.ForPlayer(pairs, 0), CancellationToken.None);
            var result = TurnEvaluator.Evaluate(pairs, plan);
            Assert.That(result.Success, Is.True, result.Error.ToString());
            Assert.That(result.Value.SeriesTilesUsed, Is.EqualTo(2));
        }

        [Test]
        public void HiddenStateCannotAffectDecision()
        {
            var state = PracticeDeals.Create("Series"); var players = state.Players.ToArray(); var stock = state.Stock.ToArray();
            var other = players[1].Hand.ToArray(); var swapped = stock[0]; stock[0] = other[0]; other[0] = swapped;
            players[1] = new PlayerState(other, players[1].Opening, players[1].Penalties);
            var different = new RoundState(state.RoundId, state.Version, state.Seed, state.StartingPlayer, state.ActivePlayer, state.TurnNumber,
                state.Phase, state.Indicator, stock, players, state.Table, state.Discards, state.PendingDraw, state.OpeningAtTurnStart,
                state.SeriesTilesUsed, state.IsInitialTurn, state.LastAcceptedSequences);
            Assert.That(StateInvariant.ValidateAuthority(different).Success, Is.True);
            var planner = new BotPlanner();
            var a = planner.PlanTurn(PlayerViewProjector.ForPlayer(state, 0), CancellationToken.None);
            var b = planner.PlanTurn(PlayerViewProjector.ForPlayer(different, 0), CancellationToken.None);
            Assert.That(Key(a), Is.EqualTo(Key(b)));
            Assert.That(a.Actions.Count, Is.GreaterThan(0));
        }

        [Test]
        public void SameObservationProducesSameDecision()
        {
            var view = PlayerViewProjector.ForPlayer(PracticeDeals.Create("Pairs"), 0); var planner = new BotPlanner();
            var first = planner.PlanTurn(view, CancellationToken.None);
            for (var i = 0; i < 3; i++) Assert.That(Key(planner.PlanTurn(view, CancellationToken.None)), Is.EqualTo(Key(first)));
            Assert.That(first.Actions.Count, Is.GreaterThan(0));
        }

        [Test]
        public void LegalButUnhelpfulPreviousDiscardIsNotPreferred()
        {
            var waiting = Builder(new[] { Id(TileColor.Red, 9), Spare }, OpeningKind.Series)
                .WithTurn(0, RoundPhase.AwaitingDraw).WithOpening(1, OpeningKind.Series)
                .WithDiscard(3, Id(TileColor.Yellow, 4)).WithTable(new Meld(0, 1, Run(TileColor.Yellow, 1, 2, 3))).Build();
            var view = PlayerViewProjector.ForPlayer(waiting, 0);
            Assert.That(DiscardTurnFinder.Find(view, CancellationToken.None).Success, Is.True);
            Assert.That(new BotPlanner().ChooseDraw(view, CancellationToken.None).Source, Is.EqualTo(DrawSource.Stock));
        }

        [Test]
        public void CancelledPlanningDoesNotReturnFallback()
        {
            using (var source = new CancellationTokenSource())
            {
                source.Cancel(); var planner = new BotPlanner();
                var view = PlayerViewProjector.ForPlayer(PracticeDeals.Create("Series"), 0);
                Assert.Throws<OperationCanceledException>(() => planner.PlanTurn(view, source.Token));
                Assert.Throws<OperationCanceledException>(() => planner.ChooseDraw(view, source.Token));
            }
        }

        [Test]
        public void NewPairDoesNotConsumeProcessingBudget()
        {
            var blue = Id(TileColor.Blue, 7);
            var state = Builder(new[] { Id(TileColor.Red, 4), Id(TileColor.Red, 5), blue, new TileId(blue.Value + 1), Spare }, OpeningKind.Pairs)
                .WithOpening(1, OpeningKind.Series).WithTable(new Meld(0, 1, Run(TileColor.Red, 1, 2, 3))).Build();
            var plan = new BotPlanner().PlanTurn(PlayerViewProjector.ForPlayer(state, 0), CancellationToken.None);
            var result = TurnEvaluator.Evaluate(state, plan);
            Assert.That(result.Success, Is.True, result.Error.ToString());
            Assert.That(result.Value.SeriesTilesUsed, Is.EqualTo(2));
            Assert.That(result.Value.Table.Count(m => m.Group.Kind == MeldKind.Pair), Is.EqualTo(1));
            Assert.That(result.Value.Players[0].Hand, Is.Empty);
        }

        [Test]
        public void JokerSwapWitnessUsesThePickedPhysicalTile()
        {
            var state = PracticeDeals.Create("Swap"); var picked = state.PendingDraw.Tile;
            var plan = new BotPlanner().PlanTurn(PlayerViewProjector.ForPlayer(state, 0), CancellationToken.None);
            var result = TurnEvaluator.Evaluate(state, plan);
            Assert.That(result.Success, Is.True, result.Error.ToString());
            Assert.That(plan.Actions.OfType<SwapJoker>().Any(), Is.True);
            Assert.That(result.Value.Table.SelectMany(m => m.Group.Tiles.Select(t => t.Id)), Has.Member(picked));
            Assert.That(result.Value.Players[0].Hand.Count, Is.LessThan(state.Players[0].Hand.Count));
        }

        internal static string Key(TurnPlan plan)
        {
            string Group(MeldProposal group) => group.Kind + ":" + string.Join(",", group.Tiles.Select(t => t.Id.Value + "/" + (int)t.RepresentedFace.Color + "/" + t.RepresentedFace.Number));
            return string.Join("|", plan.Actions.Select(a => a is OpenGroups open ? "O" + open.Opening + string.Join(";", open.Groups.Select(Group))
                : a is AddGroup add ? "A" + Group(add.Group) : a is ExtendMeld extend ? "E" + extend.MeldId + extend.Prepend + string.Join(",", extend.Tiles.Select(t => t.Id.Value + "/" + t.RepresentedFace))
                : a is SwapJoker swap ? "S" + swap.MeldId + "/" + swap.JokerId + "/" + swap.ReplacementId : "?")) + "D" + plan.Discard.Value + "/" + plan.AcceptPenalty;
        }
        private static CommandMeta Meta(PlayerView view) => new CommandMeta(view.RoundId, view.Actor, view.OwnLastSequence + 1, view.Version);
        private static readonly TileId Spare = Id(TileColor.Black, 12);
        private static TileId Id(TileColor color, int number) => new TileId(2 * (13 * (int)color + number - 1));
        private static MeldProposal Run(TileColor color, params int[] numbers) => new MeldProposal(MeldKind.Run, numbers.Select(n => new PlacedTile(Id(color, n), new TileFace(color, n))).ToArray());
        private static RoundTestBuilder Builder(TileId[] hand, OpeningKind opening) => new RoundTestBuilder().WithIndicator(Id(TileColor.Black, 13)).WithHand(0, hand).WithOpening(0, opening);
        private static RoundState DrawPrevious(RoundState state)
        {
            var players = state.Players.ToArray(); var piles = state.Discards.ToArray(); var picked = piles[3].Last();
            piles[3] = piles[3].Take(piles[3].Count - 1).ToArray(); players[0] = new PlayerState(players[0].Hand.Concat(new[] { picked }).ToArray(), players[0].Opening, 0);
            return new RoundState(state.RoundId, state.Version, state.Seed, 0, 0, 1, RoundPhase.PlanningTurn, state.Indicator,
                state.Stock, players, state.Table, piles, new DrawReceipt(DrawSource.PreviousDiscard, picked, false), players[0].Opening, 0, false, state.LastAcceptedSequences);
        }
    }
}
