using System;
using System.IO;
using System.Threading;
using NUnit.Framework;
using Okey101.Bots;
using Okey101.Core.Model;
using Okey101.Core.Turns;

namespace Okey101.Application.Tests
{
    public sealed class ScenarioFixtureTests
    {
        [TestCase("series-opening", FinishReason.DiscardWin)]
        [TestCase("pair-opening", FinishReason.DiscardWin)]
        [TestCase("joker-swap", null)]
        [TestCase("finish-x8", FinishReason.DiscardWin)]
        [TestCase("stock-end", FinishReason.StockExhausted)]
        [TestCase("four-pairs", FinishReason.AllPairs)]
        public void EachFixtureExercisesItsNamedBehavior(string name, FinishReason? reason)
        {
            var root = Path.GetFullPath("Assets/Okey101/Tests/Fixtures/Scenarios");
            var loaded = new ScenarioLoader(root).Load(name + ".json"); Assert.That(loaded.Success, Is.True, name);
            var initial = loaded.Value; var planner = new BotPlanner(); var view = PlayerViewProjector.ForPlayer(initial, initial.ActivePlayer);
            var plan = planner.PlanTurn(view, CancellationToken.None);
            var result = GameEngine.Apply(initial, new CommitTurnCommand(new CommandMeta(view.RoundId, view.Actor, view.OwnLastSequence + 1, view.Version), plan), CancellationToken.None);
            Assert.That(result.Success, Is.True, result.Error.ToString()); var final = result.Value;
            if (reason.HasValue) Assert.That(final.Result.FinishInfo.Reason, Is.EqualTo(reason));
            if (name == "series-opening") Assert.That(final.Players[0].Opening, Is.EqualTo(OpeningKind.Series));
            if (name == "pair-opening") Assert.That(final.Players[0].Opening, Is.EqualTo(OpeningKind.Pairs));
            if (name == "finish-x8") { Assert.That(final.Result.Scores[0].M, Is.EqualTo(8)); Assert.That(final.Result.Scores[0].Total, Is.EqualTo(-808)); }
            if (name == "four-pairs") foreach (var score in final.Result.Scores) Assert.That(score.Total, Is.Null);
            if (name == "joker-swap") Assert.That(System.Linq.Enumerable.Any(plan.Actions, a=>a is SwapJoker), Is.True);
        }
    }
}
