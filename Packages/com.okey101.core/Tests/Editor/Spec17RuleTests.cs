using System;
using System.Linq;
using NUnit.Framework;
using Okey101.Core.Model;
using Okey101.Core.Rules;
using Okey101.Core.Turns;
using static Okey101.Core.Tests.TableTest;
using static Okey101.Core.Tests.TurnTest;
using static Okey101.Core.Tests.EngineTest;

namespace Okey101.Core.Tests
{
    /// <summary>Rules added by the table's extra states (Design/.../galaxy101_ek_durumlar/spec/17).</summary>
    public sealed class Spec17RuleTests
    {
        [Test]
        public void PreviousDiscardIsAlwaysTakeableAndCanBePutBack()
        {
            var picked = Id(TileColor.Red, 4);
            var waiting = Authority(Waiting(new[] { Spare }, picked).Build());
            var drawn = Apply(waiting, new DrawCommand(Meta(waiting), DrawSource.PreviousDiscard));
            Assert.That(drawn.Players[0].Hand.Contains(picked), Is.True, "no witness needed any more");
            // Unused: it cannot be thrown at the next player, nor kept.
            Reject(drawn, new CommitTurnCommand(Meta(drawn), Plan(picked)), ErrorCode.DiscardMustBeUsed);
            Reject(drawn, new CommitTurnCommand(Meta(drawn), Plan(Spare)), ErrorCode.DiscardMustBeUsed);

            var back = Apply(drawn, new ReturnDiscardCommand(Meta(drawn)));
            Assert.That(back.Phase, Is.EqualTo(RoundPhase.AwaitingDraw));
            Assert.That(back.PendingDraw, Is.Null);
            Assert.That(back.PreviousDiscardReturned, Is.True);
            Assert.That(back.Discards[3].Last(), Is.EqualTo(picked));
            Assert.That(back.Players[0].Hand.Contains(picked), Is.False);
            Reject(back, new DrawCommand(Meta(back), DrawSource.PreviousDiscard), ErrorCode.PreviousDiscardUnavailable);
            var stock = Apply(back, new DrawCommand(Meta(back), DrawSource.Stock));
            Assert.That(stock.PreviousDiscardReturned, Is.False);
            Assert.That(stock.PendingDraw.Source, Is.EqualTo(DrawSource.Stock));
        }

        [Test]
        public void OnlyAPreviousDiscardDrawCanBePutBack()
        {
            var waiting = Authority(Waiting(new[] { Spare }, Id(TileColor.Red, 4)).Build());
            var drawn = Apply(waiting, new DrawCommand(Meta(waiting), DrawSource.Stock));
            Reject(drawn, new ReturnDiscardCommand(Meta(drawn)), ErrorCode.InvalidState);
            Reject(waiting, new ReturnDiscardCommand(Meta(waiting)), ErrorCode.WrongPhase);
        }

        [Test]
        public void ShortOpeningCosts101AndKeepsTheTurn()
        {
            var state = RoundFactory.Create(31, Guid.NewGuid(), 0);
            var shortOpening = Apply(state, new ShortOpeningCommand(Meta(state), OpeningKind.Series));
            Assert.That(shortOpening.Players[0].Penalties, Is.EqualTo(Penalties.Standard));
            Assert.That(shortOpening.Phase, Is.EqualTo(RoundPhase.PlanningTurn));
            Assert.That(shortOpening.Players[0].Hand, Is.EqualTo(state.Players[0].Hand), "the groups go back to the rack");
            var twice = Apply(shortOpening, new ShortOpeningCommand(Meta(shortOpening), OpeningKind.Pairs));
            Assert.That(twice.Players[0].Penalties, Is.EqualTo(2 * Penalties.Standard));
            // The turn continues normally.
            var next = Apply(twice, new CommitTurnCommand(Meta(twice), Plan(twice.Players[0].Hand[0], true)));
            Assert.That(next.Players[0].Penalties, Is.GreaterThanOrEqualTo(2 * Penalties.Standard));
        }

        [Test]
        public void ShortOpeningIsOnlyForPlayersWhoHaveNotOpened()
        {
            var opened = Authority(Builder(new[] { Spare, Id(TileColor.Red, 5) }, OpeningKind.Series)
                .WithOpening(1, OpeningKind.Series).WithTurn(0, RoundPhase.PlanningTurn).Build());
            Reject(opened, new ShortOpeningCommand(Meta(opened), OpeningKind.Series), ErrorCode.InvalidOpening);
        }

        [Test]
        public void OpeningWithThePreviousDiscardCostsItsOwnerTenTimesItsValue()
        {
            var groups = OpeningGroups(101); // Red 11-13, Blue 9-11, Yellow 5-9
            var fed = Id(TileColor.Blue, 9);
            var hand = Ids(groups).Where(id => id != fed).Concat(new[] { Spare }).ToArray();
            var waiting = Authority(Waiting(hand, fed).Build());
            var drawn = Apply(waiting, new DrawCommand(Meta(waiting), DrawSource.PreviousDiscard));
            var opened = Apply(drawn, new CommitTurnCommand(Meta(drawn), new TurnPlan(new TableAction[] { new OpenGroups(OpeningKind.Series, groups) }, Spare, false)));
            Assert.That(opened.Players[3].Penalties, Is.EqualTo(90), "9 × 10 to the player who threw it");
            Assert.That(opened.Players[0].Penalties, Is.Zero);
        }

        [Test]
        public void TakingAnotherPlayersOkeyCostsItsOwner101()
        {
            var replacement = Id(TileColor.Red, 4);
            var meld = new MeldProposal(MeldKind.Run, new[] { Normal(TileColor.Red, 3), Joker(0, TileColor.Red, 4), Normal(TileColor.Red, 5) });
            var state = Builder(new[] { replacement, Spare }, OpeningKind.Series)
                .WithOpening(1, OpeningKind.Series).WithTable(new Meld(0, 1, meld)).Build();
            var next = TableEvaluator.Evaluate(state, new TableAction[] { new SwapJoker(0, Id(TileColor.Black, 1), replacement) });
            Assert.That(next.Success, Is.True, next.Error.ToString());
            Assert.That(next.Value.Players[1].Penalties, Is.EqualTo(Penalties.Standard));
            Assert.That(next.Value.Players[0].Penalties, Is.Zero);

            // Taking back your own okey costs nothing.
            var own = Builder(new[] { replacement, Spare }, OpeningKind.Series).WithTable(new Meld(0, 0, meld)).Build();
            var mine = TableEvaluator.Evaluate(own, new TableAction[] { new SwapJoker(0, Id(TileColor.Black, 1), replacement) });
            Assert.That(mine.Success, Is.True, mine.Error.ToString());
            Assert.That(mine.Value.Players.Sum(p => p.Penalties), Is.Zero);
        }

        [Test]
        public void FedPenaltyIgnoresTheOkeyItself()
        {
            var indicator = new TileFace(TileColor.Black, 13);
            Assert.That(Penalties.Fed(Id(TileColor.Black, 1), indicator), Is.Zero);
            Assert.That(Penalties.Fed(Id(TileColor.Red, 13), indicator), Is.EqualTo(130));
        }
    }
}
