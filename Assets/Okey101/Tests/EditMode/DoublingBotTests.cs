using System.Linq;
using System.Threading;
using NUnit.Framework;
using Okey101.Bots;
using Okey101.Core.Model;
using Okey101.Core.Turns;
using Okey101.TestSupport;

namespace Okey101.Application.Tests
{
    /// <summary>Katlamalı: bots open only above the raised bar (docs/rules/101-v1.md "Katlamalı").</summary>
    public sealed class DoublingBotTests
    {
        private static TileId Id(TileColor color, int number, int copy = 0) => new TileId(2 * (13 * (int)color + number - 1) + copy);

        // Red 11–13 (36) + Blue 9–11 (30) + Yellow 6–10 (40) = 106, and a loose Black 12.
        private static RoundState Planning(int previousOpening)
        {
            var hand = new[] { Id(TileColor.Red, 11), Id(TileColor.Red, 12), Id(TileColor.Red, 13), Id(TileColor.Blue, 9), Id(TileColor.Blue, 10),
                Id(TileColor.Blue, 11), Id(TileColor.Yellow, 6), Id(TileColor.Yellow, 7), Id(TileColor.Yellow, 8), Id(TileColor.Yellow, 9),
                Id(TileColor.Yellow, 10), Id(TileColor.Black, 12) };
            return new RoundTestBuilder().WithIndicator(Id(TileColor.Black, 13)).WithHand(0, hand)
                .WithHand(1, Id(TileColor.Blue, 4)).WithOpening(1, OpeningKind.Series, previousOpening)
                .WithHand(2, Id(TileColor.Yellow, 4)).WithHand(3, Id(TileColor.Red, 5))
                .WithRules(new RuleOptions(true, true, doubling: true)).Build();
        }

        [TestCase(105, true)]
        [TestCase(106, false)]
        public void BotsOpenOnlyAboveTheRaisedBar(int previousOpening, bool opens)
        {
            var state = Planning(previousOpening);
            var plan = new BotPlanner().PlanTurn(PlayerViewProjector.ForPlayer(state, 0), CancellationToken.None);
            var applied = TurnEvaluator.Evaluate(state, plan);
            Assert.That(applied.Success, Is.True, applied.Error.ToString());
            Assert.That(applied.Value.Players[0].Opening, Is.EqualTo(opens ? OpeningKind.Series : OpeningKind.None));
            if (opens) Assert.That(applied.Value.Players[0].OpeningValue, Is.EqualTo(106));
            Assert.That(plan.Actions.OfType<OpenGroups>().Any(), Is.EqualTo(opens));
        }

        [Test]
        public void AMidHandDoublingSnapshotKeepsItsBarThroughAReplayFile()
        {
            // Bots play a real katlamalı deal until someone has opened; that position is the replay's starting state.
            var state = RoundFactory.Create(7, System.Guid.NewGuid(), 0, new RuleOptions(true, true, doubling: true, partnerOpeningsRaise: false));
            var planner = new BotPlanner();
            CommandMeta Meta() => new CommandMeta(state.RoundId, state.ActivePlayer, state.LastAcceptedSequences[state.ActivePlayer] + 1, state.Version);
            for (var turn = 0; turn < 400 && state.Result == null && !state.Players.Any(p => p.OpeningValue > 0); turn++)
            {
                TurnPlan witness = null;
                if (state.Phase == RoundPhase.AwaitingDraw)
                {
                    var decision = planner.ChooseDraw(PlayerViewProjector.ForPlayer(state, state.ActivePlayer), CancellationToken.None);
                    witness = decision.Witness; state = GameEngine.Apply(state, new DrawCommand(Meta(), decision.Source), CancellationToken.None).Value;
                }
                state = GameEngine.Apply(state, new CommitTurnCommand(Meta(), planner.PlanTurn(PlayerViewProjector.ForPlayer(state, state.ActivePlayer), witness, CancellationToken.None)), CancellationToken.None).Value;
            }
            Assert.That(state.Result, Is.Null, "the deal ended before anyone opened; pick another seed");
            var opener = Enumerable.Range(0, 4).First(p => state.Players[p].OpeningValue > 0);
            var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "okey-doubling-" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(root);
            try
            {
                var codec = new ReplayCodec(root);
                codec.Write(new ReplayRecord(state, System.Array.Empty<GameCommand>(), ReplayCodec.Hash(state)), "mid.json");
                var restored = ReplayCodec.Restore(codec.Read("mid.json").Value, CancellationToken.None);
                Assert.That(restored.Success, Is.True, restored.Error.ToString());
                Assert.That(restored.Value.Options.Doubling, Is.True);
                Assert.That(restored.Value.Options.PartnerOpeningsRaise, Is.False);
                Assert.That(restored.Value.Players.Select(p => p.OpeningValue), Is.EqualTo(state.Players.Select(p => p.OpeningValue)));
                var next = (opener + 1) % 4;
                Assert.That(Okey101.Core.Rules.TableEvaluator.OpeningBar(restored.Value, next, state.Players[opener].Opening),
                    Is.EqualTo(state.Players[opener].OpeningValue + 1));
            }
            finally { System.IO.Directory.Delete(root, true); }
        }

        /// <summary>Bot-only katlamalı deals end legally, and every opening beats the bar of its moment (or finishes from the hand).</summary>
        [Test]
        public void BotDealsAtADoublingTableTerminateLegally([Range(0u, 199u, 1u)] uint seed)
        {
            var rules = new RuleOptions(true, true, doubling: true, partnerOpeningsRaise: seed % 2 == 0);
            var state = RoundFactory.Create(seed, System.Guid.NewGuid(), (int)(seed % 4), rules); var planner = new BotPlanner(); var accepted = 0;
            void Apply(GameCommand command)
            {
                Assert.That(++accepted, Is.LessThanOrEqualTo(2000));
                var before = state; var result = GameEngine.Apply(state, command, CancellationToken.None);
                Assert.That(result.Success, Is.True, $"seed={seed} version={state.Version} error={result.Error}");
                state = result.Value;
                Assert.That(StateInvariant.ValidateAuthority(state).Success, Is.True);
                for (var p = 0; p < 4; p++)
                {
                    var opened = state.Players[p];
                    if (before.Players[p].Opening != OpeningKind.None || opened.Opening == OpeningKind.None) continue;
                    var fromHand = state.Result?.FinishInfo?.Winner == p && state.Result.FinishInfo.WinnerOpenedThisTurn;
                    if (!fromHand) Assert.That(opened.OpeningValue, Is.GreaterThanOrEqualTo(Okey101.Core.Rules.TableEvaluator.OpeningBar(before, p, opened.Opening)), $"seed={seed} seat={p}");
                }
            }
            CommandMeta Meta() => new CommandMeta(state.RoundId, state.ActivePlayer, state.LastAcceptedSequences[state.ActivePlayer] + 1, state.Version);
            while (state.Result == null)
            {
                TurnPlan witness = null;
                if (state.Phase == RoundPhase.AwaitingDraw)
                {
                    var decision = planner.ChooseDraw(PlayerViewProjector.ForPlayer(state, state.ActivePlayer), CancellationToken.None);
                    witness = decision.Witness; Apply(new DrawCommand(Meta(), decision.Source));
                }
                Apply(new CommitTurnCommand(Meta(), planner.PlanTurn(PlayerViewProjector.ForPlayer(state, state.ActivePlayer), witness, CancellationToken.None)));
            }
        }
    }
}
