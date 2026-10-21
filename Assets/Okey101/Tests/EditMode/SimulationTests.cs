using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using Okey101.Bots;
using Okey101.Core.Model;
using Okey101.Core.Turns;

namespace Okey101.Application.Tests
{
    [Category("Simulation")]
    public sealed class SimulationTests
    {
        private int wins, exhausted, cancelled, series, pairs, commands;
        private readonly Stopwatch elapsed = new Stopwatch();
        public static IEnumerable<uint> Seeds => Enumerable.Range(0, 1000).Select(n => (uint)n);
        [OneTimeSetUp] public void Begin() { elapsed.Start(); }
        [OneTimeTearDown] public void Report() => TestContext.Out.WriteLine($"SIMULATION seeds=0..999 wins={wins} stockEnd={exhausted} allPairs={cancelled} seriesOpenings={series} pairOpenings={pairs} commands={commands} seconds={elapsed.Elapsed.TotalSeconds:F3}");
        [TestCaseSource(nameof(Seeds))]
        public void Seeds0To999TerminateLegally(uint seed)
        {
            UnityEngine.Debug.LogFormat(UnityEngine.LogType.Log, UnityEngine.LogOption.NoStacktrace, null, "SIMULATION_START seed={0}", seed);
            var dealTime = Stopwatch.StartNew();
            var state = RoundFactory.Create(seed, Guid.NewGuid(), (int)(seed % 4)); var planner = new BotPlanner(); var accepted = 0;
            void Apply(GameCommand command)
            {
                Assert.That(++accepted, Is.LessThanOrEqualTo(2000), "A deal cannot be force-finished at the audit limit.");
                var before = state; var result = GameEngine.Apply(state, command, CancellationToken.None);
                Assert.That(result.Success, Is.True, $"seed={seed} version={state.Version} error={result.Error}");
                state = result.Value; commands++;
                Assert.That(StateInvariant.ValidateAuthority(state).Success, Is.True);
                Assert.That(state.Version, Is.EqualTo(before.Version + 1));
                for (var p = 0; p < 4; p++)
                    if (before.Players[p].Opening == OpeningKind.None && state.Players[p].Opening != OpeningKind.None)
                    { if (state.Players[p].Opening == OpeningKind.Series) series++; else pairs++; }
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
                var plan = planner.PlanTurn(PlayerViewProjector.ForPlayer(state, state.ActivePlayer), witness, CancellationToken.None);
                Apply(new CommitTurnCommand(Meta(), plan));
            }
            var finish = state.Result.FinishInfo;
            if (finish.Reason == FinishReason.DiscardWin) wins++; else if (finish.Reason == FinishReason.StockExhausted) exhausted++; else cancelled++;
            var indicator = TileCatalog.Create()[state.Indicator.Value].PrintedFace.Value;
            var multiplier = (finish.DiscardedRealJoker ? 2 : 1) * (finish.WinnerOpening == OpeningKind.Pairs ? 2 : 1) * (finish.WinnerOpenedThisTurn ? 2 : 1);
            for (var p = 0; p < 4; p++)
            {
                var player = state.Players[p]; var actual = state.Result.Scores[p];
                var j = player.Hand.Count(id => TileCatalog.IsRealJoker(id, indicator));
                var n = player.Hand.Where(id => !TileCatalog.IsRealJoker(id, indicator)).Sum(id => TileCatalog.GetNonJokerFace(id, indicator).Number);
                Assert.That(actual.N, Is.EqualTo(n)); Assert.That(actual.J, Is.EqualTo(j)); Assert.That(actual.P, Is.EqualTo(player.Penalties));
                if (finish.Reason == FinishReason.AllPairs) { Assert.That(actual.HasScore, Is.False); Assert.That(actual.Total, Is.Null); continue; }
                var b = finish.Winner == p ? -101 : player.Opening == OpeningKind.None ? 202 : (n + 101 * j) * (player.Opening == OpeningKind.Pairs ? 2 : 1);
                Assert.That(actual.B, Is.EqualTo(b)); Assert.That(actual.M, Is.EqualTo(multiplier));
                Assert.That(actual.Total, Is.EqualTo((b + player.Penalties) * multiplier));
            }
            UnityEngine.Debug.LogFormat(UnityEngine.LogType.Log, UnityEngine.LogOption.NoStacktrace, null,
                "SIMULATION_END seed={0} commands={1} reason={2} milliseconds={3}",seed,accepted,finish.Reason,dealTime.ElapsedMilliseconds);
        }
    }
}
