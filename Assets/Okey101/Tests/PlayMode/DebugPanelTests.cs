using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Okey101.Application;
using Okey101.Core.Model;
using Okey101.Presentation;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Okey101.PlayMode.Tests
{
    public sealed class DebugPanelTests
    {
        [UnityTest] public IEnumerator AllSixFixtureButtonsImportStepAndReplay()
        {
            var names=new[]{"series-opening","pair-opening","joker-swap","finish-x8","stock-end","four-pairs"};
            var labels=new[]{"Seri açılışı","Çift açılışı","Okey değişimi","Bitiş ×8","Deste sonu","Dört çift iptali"};
            var fixtures=names.Select(name=>new TextAsset(File.ReadAllText("Assets/Okey101/Tests/Fixtures/Scenarios/"+name+".json")){name=name}).ToArray();
            var rootPath=Path.Combine(Path.GetTempPath(),"okey-six-panels-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(rootPath);
            var root=new GameObject("Six fixture diagnostic table");
            try
            {
                var game=root.AddComponent<GameBootstrap>();game.Initialize(71,0);
                var panel=root.AddComponent<DebugPanel>();panel.Configure(fixtures);panel.Initialize(game,rootPath);
                for(var i=0;i<names.Length;i++)
                {
                    root.GetComponentsInChildren<Button>(true).Single(button=>button.name==labels[i]).onClick.Invoke();
                    Assert.That(game.Session.GetView(0).RoundId.ToString("D"),Is.EqualTo("10100000-0000-0000-0000-"+(i+1).ToString("D12")));
                    yield return BotLifecycleTests.Await(panel.StepBotAsync());
                    var final=game.Session.CaptureReplay();Assert.That(final.Commands.Count,Is.EqualTo(1));
                    if(i==3) Assert.That(game.Session.GetView(0).Result.Scores[0].Total,Is.EqualTo(-808));
                    if(i==4) Assert.That(game.Session.GetView(0).Result.FinishInfo.Reason,Is.EqualTo(FinishReason.StockExhausted));
                    if(i==5) Assert.That(game.Session.GetView(0).Result.FinishInfo.Reason,Is.EqualTo(FinishReason.AllPairs));
                    var export=panel.ExportReplayAsync();yield return BotLifecycleTests.Await(export);
                    yield return BotLifecycleTests.Await(panel.ReplayAsync(export.Result));
                    Assert.That(game.Session.CaptureReplay().ExpectedFinalHash,Is.EqualTo(final.ExpectedFinalHash));
                    Assert.That(game.Session.CaptureReplay().Commands.Count,Is.EqualTo(1));
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root);foreach(var fixture in fixtures) UnityEngine.Object.DestroyImmediate(fixture);Directory.Delete(rootPath,true); }
        }
        [UnityTest] public IEnumerator LoadStepExportReplayAndInvalidLoadPreserveAuthority()
        {
            var rootPath = Path.Combine(Path.GetTempPath(), "okey-panel-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(rootPath);
            var root = new GameObject("Diagnostic table");
            try
            {
                var state = PracticeDeals.Create("Finish"); new ReplayCodec(rootPath).Write(new ReplayRecord(state, Array.Empty<GameCommand>(), ReplayCodec.Hash(state)), "fixture.json");
                var game = root.AddComponent<GameBootstrap>(); game.Initialize(71, 0);
                var panel = root.AddComponent<DebugPanel>(); panel.Initialize(game, rootPath); panel.LoadScenario("fixture.json");
                Assert.That(game.Session.GetView(0).RoundId, Is.EqualTo(state.RoundId));
                var step = panel.StepBotAsync(); yield return BotLifecycleTests.Await(step);
                Assert.That(game.Session.GetView(0).Result.FinishInfo.Winner, Is.Zero);
                var expected = game.Session.CaptureReplay().ExpectedFinalHash;
                var export = panel.ExportReplayAsync(); yield return BotLifecycleTests.Await(export);
                Assert.That(File.Exists(Path.Combine(rootPath, export.Result)), Is.True);
                panel.LoadScenario("missing.json"); Assert.That(game.Session.CaptureReplay().ExpectedFinalHash, Is.EqualTo(expected));
                panel.LoadScenario("fixture.json"); Assert.That(game.Session.GetView(0).Result, Is.Null);
                yield return BotLifecycleTests.Await(panel.ReplayAsync(export.Result));
                Assert.That(game.Session.CaptureReplay().ExpectedFinalHash, Is.EqualTo(expected));
                Assert.That(game.Presenter.VisibleOwnTileIds, Is.Empty);
                var replayed=game.Session.CaptureReplay();
                Assert.That(replayed.Commands.Count,Is.EqualTo(1),"Replay must preserve accepted-command provenance.");
                Assert.That(ReplayCodec.Hash(replayed.InitialState),Is.EqualTo(ReplayCodec.Hash(state)));
                var secondExport=panel.ExportReplayAsync();yield return BotLifecycleTests.Await(secondExport);
                Assert.That(new ReplayCodec(rootPath).Read(secondExport.Result).Value.Commands.Count,Is.EqualTo(1));
                game.InstallDiagnosticRound(replayed.InitialState);
                Assert.That(game.Session.GetView(0).Result,Is.Null);Assert.That(game.Session.GetView(0).OwnHand,Is.EqualTo(state.Players[0].Hand));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); Directory.Delete(rootPath, true); }
        }
    }
}
