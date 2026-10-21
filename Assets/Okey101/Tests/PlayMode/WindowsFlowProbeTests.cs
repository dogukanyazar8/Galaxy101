using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using Okey101.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace Okey101.PlayMode.Tests
{
    public sealed class WindowsFlowProbeTests
    {
        private GameObject root;
        private string directory;
        [TestCase(0u)] [TestCase(72u)] [TestCase(uint.MaxValue)]
        public void DiagnosticSeedOverrideIsConsumedOnce(uint seed)
        {
            MainMenu.DiagnosticNextSeed=seed;
            var consume=typeof(MainMenu).GetMethod("NewSeed",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
            Assert.That((uint)consume.Invoke(null,null),Is.EqualTo(seed));
            Assert.That(MainMenu.DiagnosticNextSeed,Is.Null,"The override must not leak into a later round.");
            Assert.DoesNotThrow(()=>consume.Invoke(null,null));
            Assert.That(MainMenu.DiagnosticNextSeed,Is.Null);
        }
        [Test] public void DiagnosticStartupSeedHasHumanStarterAndUnfinishedAuthority()
        {
            using(var session=new Okey101.Application.LocalSession())
            {
                session.Start(72,0);
                Assert.That(session.CaptureReplay().InitialState.Seed,Is.EqualTo(72));
                Assert.That(session.CaptureReplay().InitialState.StartingPlayer,Is.Zero);
                Assert.That(session.GetView(0).Result,Is.Null);
                Assert.That(session.GetView(0).OwnHand.Count,Is.EqualTo(22));
            }
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void FailedFixtureLoadCannotPassByKeepingPreviousRound(int index)
        {
            root=new GameObject("Fixture load regression");var game=root.AddComponent<GameBootstrap>();game.Initialize(71,0);
            var method=typeof(WindowsFlowProbe).GetMethod("VerifyFixtureInitial",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
            Assert.Throws<System.Reflection.TargetInvocationException>(()=>method.Invoke(null,new object[]{game,index}));
        }
        [UnityTest] public IEnumerator RejectedReplayCannotPassWhenCurrentStateAlreadyMatchesExpected()
        {
            directory=Path.Combine(Path.GetTempPath(),"okey-flow-replay-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            root=new GameObject("Flow replay regression");var game=root.AddComponent<GameBootstrap>();game.Initialize(71,0);
            var panel=root.AddComponent<DebugPanel>();panel.Initialize(game,directory);
            var probe=root.AddComponent<WindowsFlowProbe>();
            var method=typeof(WindowsFlowProbe).GetMethod("VerifyReplayAsync",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            var task=(System.Threading.Tasks.Task)method.Invoke(probe,new object[]{game,panel,"does-not-exist.json",game.Session.CaptureReplay()});
            var deadline=Time.realtimeSinceStartup+10;while(!task.IsCompleted){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}
            Assert.That(task.IsFaulted,Is.True,"Rejected/no-op replay must fail acceptance even if the old hash matches.");
        }
        [UnityTest] public IEnumerator PackagedFlowHarnessWritesAllCompletedChecks()
        {
            directory=Path.Combine(Path.GetTempPath(),"okey-flow-probe-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            root=new GameObject("Flow probe test");var task=root.AddComponent<WindowsFlowProbe>().RunAsync(directory);
            var deadline=Time.realtimeSinceStartup+120;
            while(!task.IsCompleted){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}
            task.GetAwaiter().GetResult();
            var path=Path.Combine(directory,"flow.json");Assert.That(File.Exists(path),Is.True);
            var json=File.ReadAllText(path);
            foreach(var check in new[]{"settings-cancel-save-isolated","galaxy-native-menu-bot-entry","natural-round","series-input","pairs-input","joker-swap-input","penalty-cancel-accept","six-fixtures-replay","ten-menu-table-cycles"})
                Assert.That(json,Does.Contain(check));
            Assert.That(json,Does.Contain("\"completed\": true"));
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            MainMenu.DiagnosticNextSeed=null;
            if(root!=null)UnityEngine.Object.DestroyImmediate(root);
            foreach(var name in new[]{"Bootstrap","Table"})
            {
                var scene=SceneManager.GetSceneByName(name);if(!scene.IsValid()||!scene.isLoaded)continue;
                if(SceneManager.GetActiveScene()==scene)SceneManager.SetActiveScene(SceneManager.CreateScene("After packaged flow"));
                var unload=SceneManager.UnloadSceneAsync(scene);while(!unload.isDone)yield return null;
            }
            if(directory!=null)Directory.Delete(directory,true);
        }
    }
}
