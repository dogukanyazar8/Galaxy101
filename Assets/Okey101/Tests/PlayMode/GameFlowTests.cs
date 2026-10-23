using System;
using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Okey101.Application;
using Okey101.Bots;
using Okey101.Core.Model;
using Okey101.Core.Turns;
using Okey101.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Galaxy101;
using ToolkitButton = UnityEngine.UIElements.Button;

namespace Okey101.PlayMode.Tests
{
    public sealed class GameFlowTests
    {
        private GameObject root;
        private string menuStatePath;
        [SetUp] public void Setup()
        {
            root = new GameObject("Game flow test");
            menuStatePath=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"galaxy-flow-"+Guid.NewGuid().ToString("N")+".json");
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(root!=null) UnityEngine.Object.DestroyImmediate(root);
            foreach(var name in new[]{"Table","Bootstrap"})
            {
                var scene=SceneManager.GetSceneByName(name); if(!scene.IsValid()||!scene.isLoaded)continue;
                if(SceneManager.GetActiveScene()==scene) SceneManager.SetActiveScene(SceneManager.CreateScene("After flow test"));
                var unload=SceneManager.UnloadSceneAsync(scene); while(!unload.isDone)yield return null;
            }
            if(System.IO.File.Exists(menuStatePath))System.IO.File.Delete(menuStatePath);
        }
        private static IEnumerator Await(Task task)
        {
            var deadline=Time.realtimeSinceStartup+15;
            while(!task.IsCompleted){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}
            task.GetAwaiter().GetResult();
        }
        [TestCase(0u,0)] [TestCase(1u,1)] [TestCase(2u,2)] [TestCase(3u,3)]
        public void NewGameUsesAllFourStartingSeats(uint seed,int expected)
        {
            var game=root.AddComponent<GameBootstrap>();
            game.InitializeNewGame(seed,false);
            var initial=game.Session.CaptureReplay().InitialState;
            Assert.That(initial.StartingPlayer,Is.EqualTo(expected));
            Assert.That(initial.Players[expected].Hand.Count,Is.EqualTo(22));
        }
        [UnityTest] public IEnumerator MenuStartsOneHumanThreeBots()
        {
            var menu=root.AddComponent<MainMenuController>();menu.Initialize(menuStatePath);yield return null;
            yield return Submit(menu.View.Root.Q<ToolkitButton>("mode-4"));yield return null;
            Assert.That(menu.Model.SelectedMode,Is.EqualTo(4));
            var first=menu.StartLocalAsync(MainMenuController.LegacyTableScene);var repeated=menu.StartLocalAsync(MainMenuController.LegacyTableScene);
            yield return Await(first); yield return Await(repeated); yield return null;
            var games=UnityEngine.Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None).Where(g=>g.Session!=null).ToArray();
            Assert.That(games,Has.Length.EqualTo(1)); root=games[0].gameObject;
            Assert.That(games[0].BotsEnabled,Is.True); Assert.That(games[0].Session.GetView(0).Actor,Is.Zero);
            Assert.That(games[0].Session.GetView(0).Players.Count,Is.EqualTo(4));
        }
        [UnityTest] public IEnumerator PauseStopsBotAndResumeDoesNotDoubleSubmit()
        {
            var game=root.AddComponent<GameBootstrap>(); game.Initialize(71,1,true); game.HandleFocus(false);
            var boundary=game.Session.GetView(0); for(var i=0;i<8;i++)yield return null;
            Assert.That(game.Session.IsPaused,Is.True); Assert.That(game.Session.GetView(0).Version,Is.EqualTo(boundary.Version));
            game.TogglePause(); var deadline=Time.realtimeSinceStartup+15;
            while(game.BotThinking||game.Session.GetView(0).ActivePlayer!=0)
            {Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}
            Assert.That(game.Session.GetView(0).Version,Is.EqualTo(5));
        }
        [UnityTest] public IEnumerator FocusLossPausesLocalSession()
        {
            var game=root.AddComponent<GameBootstrap>(); game.Initialize(71,0); yield return null;
            game.SelectTile(game.Session.GetView(0).OwnHand[0]); var task=game.DiscardSelectedAsync(true);
            while(!game.Presenter.Motion.IsPlaying&&!task.IsCompleted)yield return null;
            game.HandleFocus(false); var version=game.Session.GetView(0).Version;
            var moving=root.GetComponentsInChildren<TileView>().Single(t=>t.Role==TileRole.Discard); var position=moving.transform.position;
            for(var i=0;i<8;i++)yield return null;
            Assert.That(game.Session.IsPaused,Is.True); Assert.That(moving.transform.position,Is.EqualTo(position));
            Assert.That(game.Session.GetView(0).Version,Is.EqualTo(version)); game.HandleFocus(true);
            Assert.That(game.Session.IsPaused,Is.True,"Focus gain must not auto-resume."); game.TogglePause(); yield return Await(task);
        }
        [UnityTest] public IEnumerator CancelLeavePreservesExactRound()
        {
            var game=root.AddComponent<GameBootstrap>(); game.Initialize(71,0); yield return null;
            game.SelectTile(game.Session.GetView(0).OwnHand[0]); game.MoveSelectedTo(23);
            var before=game.Session.CaptureReplay().ExpectedFinalHash; var session=game.Session; var id=game.SelectedTiles[0];
            var leave=game.RequestLeaveAsync(); var dialog=root.GetComponentInChildren<LeaveRoundDialog>(true);
            Assert.That(dialog,Is.Not.Null); Assert.That(dialog.IsOpen,Is.True); dialog.Resolve(false); yield return Await(leave);
            Assert.That(leave.Result,Is.False); Assert.That(game.Session,Is.SameAs(session)); Assert.That(session.IsDisposed,Is.False);
            Assert.That(game.Session.CaptureReplay().ExpectedFinalHash,Is.EqualTo(before)); Assert.That(game.Presenter.Rack.IndexOf(id),Is.EqualTo(23));
        }
        [UnityTest] public IEnumerator ConfirmLeaveCancelsOldWork()
        {
            var game=root.AddComponent<GameBootstrap>(); game.Initialize(71,1,true); yield return null; var old=game.Session;
            var leave=game.RequestLeaveAsync(); var dialog=root.GetComponentInChildren<LeaveRoundDialog>(true);
            Assert.That(dialog,Is.Not.Null); dialog.Resolve(true); yield return Await(leave); yield return null;
            Assert.That(old.IsDisposed,Is.True); Assert.That(UnityEngine.Object.FindObjectsByType<TileMotion>(FindObjectsSortMode.None),Is.Empty);
            yield return NativeMenuReady();
            Assert.That(UnityEngine.Object.FindAnyObjectByType<MainMenuController>(),Is.Not.Null);
            Assert.That(UnityEngine.Object.FindAnyObjectByType<MainMenu>(),Is.Null,"Returning from a table must restore the authored Galaxy menu.");
        }
        private static RoundState Finished(string scenario)
        {
            var state=new ScenarioLoader(System.IO.Path.GetFullPath("Assets/Okey101/Tests/Fixtures/Scenarios")).Load(scenario+".json").Value;
            var view=PlayerViewProjector.ForPlayer(state,state.ActivePlayer); var plan=new BotPlanner().PlanTurn(view,CancellationToken.None);
            return GameEngine.Apply(state,new CommitTurnCommand(new CommandMeta(view.RoundId,view.Actor,view.OwnLastSequence+1,view.Version),plan),CancellationToken.None).Value;
        }
        [UnityTest] public IEnumerator ReplayNextStarterAndCancelledSameStarter()
        {
            foreach(var scenario in new[]{"finish-x8","stock-end","four-pairs"})
            {
                var finished=Finished(scenario); using(var session=new LocalSession())
                {
                    session.Start(finished); session.GetView(0,out var oldEpoch); session.StartNextRound(72);
                    var next=session.CaptureReplay().InitialState;
                    Assert.That(next.RoundId,Is.Not.EqualTo(finished.RoundId)); Assert.That(oldEpoch.IsCancellationRequested,Is.True);
                    Assert.That(next.StartingPlayer,Is.EqualTo((finished.StartingPlayer+(scenario=="four-pairs"?0:1))%4));
                    Assert.That(next.Players[next.StartingPlayer].Hand.Count,Is.EqualTo(22)); Assert.That(next.Version,Is.Zero);
                }
            }
            yield return null;
        }
        [UnityTest] public IEnumerator ResultShowsBreakdownAndCancellation()
        {
            var panel=root.AddComponent<ResultPanel>(); panel.Show(Finished("finish-x8").Result);
            foreach(var field in new[]{"N=","J=","P=","B=","M=","-808"})Assert.That(panel.Summary,Does.Contain(field));
            panel.Show(Finished("four-pairs").Result);
            Assert.That(panel.Summary,Does.Contain("Puan yazılmadı")); Assert.That(panel.Summary,Does.Not.Contain("Toplam: 0")); yield return null;
        }
        [UnityTest] public IEnumerator TenMenuTableCyclesDisposeEveryOwnerAndAudioProducer()
        {
            var load=SceneManager.LoadSceneAsync("Bootstrap",LoadSceneMode.Single);while(!load.isDone)yield return null;yield return null;
            for(var i=0;i<10;i++)
            {
                yield return NativeMenuReady();
                var menu=UnityEngine.Object.FindAnyObjectByType<MainMenuController>();Assert.That(menu,Is.Not.Null);
                // These authored-scene cycles must not write the user's saved menu preferences.
                menu.Model.SelectedMode=4;menu.View.Refresh();yield return null;
                // "Botlara karşı" opens the 3D Galaxy table; these cycles cover the classic table entry.
                menu.StartLocalAsync(MainMenuController.LegacyTableScene);
                var openingDeadline=Time.realtimeSinceStartup+15;
                while(UnityEngine.Object.FindAnyObjectByType<GameBootstrap>()?.Session==null)
                {Assert.That(Time.realtimeSinceStartup,Is.LessThan(openingDeadline));yield return null;}
                yield return null;
                var games=UnityEngine.Object.FindObjectsByType<GameBootstrap>(FindObjectsSortMode.None).Where(g=>g.Session!=null).ToArray();
                Assert.That(games,Has.Length.EqualTo(1));var game=games[0];game.SetPaused(false);
                var deadline=Time.realtimeSinceStartup+30; // three bot turns; the starter depends on the deal
                while(game.BotThinking||game.Session.GetView(0).ActivePlayer!=0)
                {Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}
                Assert.That(game.GetComponentsInChildren<Camera>().Length,Is.EqualTo(1));
                Assert.That(game.GetComponentsInChildren<AudioListener>().Length,Is.EqualTo(1));
                Assert.That(game.GetComponentsInChildren<TileMotion>().Length,Is.EqualTo(1));
                game.SelectTile(game.Session.GetView(0).OwnHand[0]);yield return Await(game.DiscardSelectedAsync(true));
                Assert.That(game.GetComponentsInChildren<AudioSource>().Length,Is.EqualTo(1));var old=game.Session;
                var leave=game.RequestLeaveAsync();game.GetComponentInChildren<LeaveRoundDialog>(true).Resolve(true);
                yield return Await(leave);yield return null;
                Assert.That(old.IsDisposed,Is.True);
                Assert.That(UnityEngine.Object.FindObjectsByType<TileMotion>(FindObjectsSortMode.None),Is.Empty);
                Assert.That(UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None),Is.Empty);
            }
        }
        [UnityTest] public IEnumerator CancelSettingsPreservesAppliedValues()
        {
            var game=root.AddComponent<GameBootstrap>();game.Initialize(71,0);yield return null;game.SetPaused(true);
            var settingsButton=root.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b=>b.name=="Ayarlar");
            settingsButton.onClick.Invoke();
            var panel=root.GetComponentInChildren<SettingsPanel>();Assert.That(panel,Is.Not.Null);
            panel.GetComponentInChildren<UnityEngine.UI.Slider>().value=.15f;
            foreach(var toggle in panel.GetComponentsInChildren<UnityEngine.UI.Toggle>())toggle.isOn=true;
            panel.Close();Assert.That(game.Settings.MasterVolume,Is.EqualTo(1));
            Assert.That(game.Settings.Fullscreen||game.Settings.ReducedMotion,Is.False);
        }
        [UnityTest] public IEnumerator LeaveModalBlocksUnderlyingResumeAndRestoresFocus()
        {
            var game=root.AddComponent<GameBootstrap>();game.Initialize(71,1,true);game.SetPaused(true);yield return null;
            var resume=root.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b=>b.name=="Devam et");
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(resume.gameObject);
            var leave=game.RequestLeaveAsync();var dialog=root.GetComponentInChildren<LeaveRoundDialog>(true);
            var version=game.Session.GetView(0).Version;
            Assert.That(resume.IsInteractable(),Is.False,"Modal must remove background buttons from navigation.");
            Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject.transform.IsChildOf(dialog.transform),Is.True);
            Assert.That(dialog.GetComponentsInChildren<UnityEngine.UI.Button>().All(b=>b.IsInteractable()),Is.True);
            resume.onClick.Invoke();game.SetPaused(false);
            for(var i=0;i<8;i++)yield return null;
            Assert.That(game.Session.IsPaused,Is.True);Assert.That(game.Session.GetView(0).Version,Is.EqualTo(version));
            dialog.Resolve(false);yield return Await(leave);
            Assert.That(resume.IsInteractable(),Is.True);
            Assert.That(UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject,Is.EqualTo(resume.gameObject));
        }
        [UnityTest] public IEnumerator SettingsModalBlocksPlayAndPreservesDraftOnRepeatedShow()
        {
            var menu=root.AddComponent<MainMenuController>();menu.Initialize(menuStatePath);yield return null;
            yield return Submit(menu.View.Root.Q<ToolkitButton>("settings-open"));yield return null;
            menu.View.SetTab("settings",1);yield return null;
            var slider=menu.View.Root.Q<GalaxySlider>("setting-master");Assert.That(slider,Is.Not.Null);slider.Value=15;
            menu.View.OpenPanel("settings");yield return null;
            slider=menu.View.Root.Q<GalaxySlider>("setting-master");
            Assert.That(slider.Value,Is.EqualTo(15));
            Assert.That(menu.View.Root.Q<ToolkitButton>("play-now").enabledInHierarchy,Is.False);
            yield return Await(menu.StartLocalAsync(MainMenuController.LegacyTableScene));Assert.That(menu!=null,Is.True);
            Assert.That(UnityEngine.Object.FindAnyObjectByType<GameBootstrap>(),Is.Null);
            menu.View.CloseTop();yield return null;
            Assert.That(menu.View.Root.Q<ToolkitButton>("play-now").enabledInHierarchy,Is.True);
            Assert.That(System.IO.File.Exists(menuStatePath),Is.False,"Cancel must not persist the settings draft.");
            menu.View.OpenPanel("settings",1);yield return null;
            Assert.That(menu.View.Root.Q<GalaxySlider>("setting-master").Value,Is.EqualTo(80));
        }

        private static IEnumerator NativeMenuReady()
        {
            var deadline=Time.realtimeSinceStartup+15;
            while(UnityEngine.Object.FindAnyObjectByType<MainMenuController>()?.View==null)
            {Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}
        }
        private static IEnumerator Submit(ToolkitButton button)
        {
            Assert.That(button,Is.Not.Null);Assert.That(button.enabledInHierarchy,Is.True);button.Focus();yield return null;
            using(var evt=NavigationSubmitEvent.GetPooled()){evt.target=button;button.SendEvent(evt);}
        }
    }
}

