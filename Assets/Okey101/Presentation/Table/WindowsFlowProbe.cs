#if UNITY_EDITOR || OKEY_DIAGNOSTICS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Okey101.Application;
using Okey101.Core.Model;
using Okey101.Core.Turns;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Galaxy101;
using UnityEngine.UIElements;
using Button=UnityEngine.UI.Button;
using Text=UnityEngine.UI.Text;
using ToolkitButton=UnityEngine.UIElements.Button;
namespace Okey101.Presentation
{
    public sealed class WindowsFlowProbe : MonoBehaviour
    {
        [Serializable] private sealed class Evidence
        {public bool completed;public string mode="programmatic player UI/controller actions; no desktop input",failure="",completedUtc;public string[] checks;}
        private readonly CancellationTokenSource lifetime=new CancellationTokenSource();
        private readonly List<string> checks=new List<string>();
        private string directory;
        private bool running;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if(!BackgroundAcceptance.Requested||BackgroundAcceptance.Argument("-okeyA4FlowOutput")==null)return;
            new GameObject("A4 background flow owner").AddComponent<WindowsFlowProbe>().RunFromArguments();
        }
        private async void RunFromArguments()
        {
            try{await RunAsync(BackgroundAcceptance.Output);UnityEngine.Application.Quit(0);}
            catch(Exception exception){Debug.LogException(exception);UnityEngine.Application.Quit(1);}
        }
        public async Task RunAsync(string output)
        {
            if(running||BackgroundAcceptance.FlowOutput!=null)throw new InvalidOperationException("Flow probe already started.");
            directory=Path.GetFullPath(output);
            if(!Directory.Exists(directory)||Directory.EnumerateFileSystemEntries(directory).Any())throw new IOException("Flow evidence requires an existing empty directory.");
            running=true;BackgroundAcceptance.FlowOutput=directory;DontDestroyOnLoad(gameObject);var evidence=new Evidence();
            try
            {
                // Exercise the real native settings controller against an isolated store, including in editor runs.
                var settingsPath=Path.Combine(directory,"galaxy-menu.json");
                var settingsOwner=new GameObject("Isolated Galaxy settings flow");
                try
                {
                    var menu=settingsOwner.AddComponent<MainMenuController>();menu.Initialize(settingsPath);
                    await ClickNative(menu,"settings-open");menu.View.SetTab("settings",1);
                    await Wait(()=>menu.View.Root.Q<GalaxySlider>("setting-master")!=null);
                    menu.View.Root.Q<GalaxySlider>("setting-master").Value=25;
                    await ClickNative(menu,"settings-close");
                    Require(!File.Exists(settingsPath),"Cancel wrote native settings.");
                    Require((int)menu.Model.Settings["master"]==80,"Cancel changed the applied native volume.");
                    await ClickNative(menu,"settings-open");
                    await Wait(()=>menu.View.Root.Q<GalaxySlider>("setting-master")!=null);
                    Require(menu.View.Root.Q<GalaxySlider>("setting-master").Value==80,"Cancel retained an unsaved volume draft.");
                    menu.View.Root.Q<GalaxySlider>("setting-master").Value=25;
                    await ClickNative(menu,"settings-save");
                    Require((int)MenuModel.LoadResources(settingsPath).Settings["master"]==25,"Saved native settings did not load.");
                }
                finally{Destroy(settingsOwner);}
                Record("settings-cancel-save-isolated");
                var load=SceneManager.LoadSceneAsync("Bootstrap",LoadSceneMode.Single);await Wait(()=>load.isDone);
                await Wait(()=>FindAnyObjectByType<MainMenuController>()?.View!=null);
                Require(FindAnyObjectByType<MainMenu>()==null,"Bootstrap still contains the retired uGUI menu.");
                var game=await OpenTable();Record("galaxy-native-menu-bot-entry");
                game.InstallDiagnosticRound(RoundFactory.Create(71,Guid.NewGuid(),0));game.SetBotsEnabled(true);
                var turns=0;
                while(game.Session.GetView(0).Result==null)
                {
                    await Wait(()=>game.Session.GetView(0).Result!=null||(game.Session.GetView(0).ActivePlayer==0&&game.CanControl));
                    if(game.Session.GetView(0).Result!=null)break;
                    if(game.Session.GetView(0).Phase==RoundPhase.AwaitingDraw)
                    {var version=game.Session.GetView(0).Version;Click(game,"Kapalıdan çek");await Wait(()=>game.Session.GetView(0).Version>version&&game.CanControl);}
                    game.SelectTile(game.Session.GetView(0).OwnHand[0]);await game.RecomputePreviewAsync();await Commit(game);turns++;
                }
                await Wait(()=>!game.BotThinking&&!game.Presenter.Motion.IsPlaying);
                Require(turns>0,"Human input policy never played.");
                SaveReplay(game,"natural-round");await CaptureAsync(game,"natural-result");Record("natural-round");
                var starter=game.Session.CaptureReplay().InitialState.StartingPlayer;var oldId=game.Session.GetView(0).RoundId;
                Click(game.GetComponentInChildren<ResultPanel>(),"Sonraki el");
                await Wait(()=>game.Session.GetView(0).RoundId!=oldId);
                Require(game.Session.CaptureReplay().InitialState.StartingPlayer==(starter+1)%4,"Normal next starter incorrect.");Record("result-next-starter");
                await Leave(game,false);
                foreach(var name in new[]{"Series","Pairs","Finish"})
                {
                    game=await OpenTable();game.InstallDiagnosticRound(PracticeDeals.Create(name));
                    Click(game,"Çoklu seç");
                    var groups=name=="Pairs"?Enumerable.Range(1,5).Select(n=>new[]{Id(TileColor.Red,n),Id(TileColor.Red,n,1)}).ToArray()
                        :new[]{new[]{11,12,13}.Select(n=>Id(TileColor.Red,n)).ToArray(),new[]{9,10,11}.Select(n=>Id(TileColor.Blue,n)).ToArray(),new[]{5,6,7,8,9}.Select(n=>Id(TileColor.Yellow,n)).ToArray()};
                    foreach(var group in groups)
                    {foreach(var id in group)game.ClickTile(id);Click(game,name=="Pairs"?"Çift":"Sıralı per");await game.RecomputePreviewAsync();}
                    Click(game,"Çoklu seç");game.SelectTile(name=="Finish"?Id(TileColor.Black,1):Id(TileColor.Black,12));await game.RecomputePreviewAsync();
                    Require(name=="Pairs"?game.Preview.PairCount==5:game.Preview.OpeningTotal==101,"Opening preview incorrect.");
                    await Commit(game);await Wait(()=>game.Session.GetView(0).Result!=null&&!game.Presenter.Motion.IsPlaying);
                    var expected=name=="Series"?-202:-404;Require(game.Session.GetView(0).Result.Scores[0].Total==expected,"Opening finish score incorrect.");
                    await CaptureAsync(game,"opening-"+name);SaveReplay(game,"opening-"+name);Record(name=="Pairs"?"pairs-input":name=="Series"?"series-input":"real-joker-finish-input");
                    await Leave(game,false);
                }
                game=await OpenTable();game.InstallDiagnosticRound(PracticeDeals.Create("Swap"));
                var before=game.Session.CaptureReplay().ExpectedFinalHash;
                game.SelectTile(Id(TileColor.Red,2));game.Presenter.Melds.Select(0,Id(TileColor.Black,1));Click(game,"Okey değiştir");await game.RecomputePreviewAsync();
                Require(game.Preview.ProjectedOwnHand.Contains(Id(TileColor.Black,1)),"Swap preview lost joker.");
                Require(game.Session.CaptureReplay().ExpectedFinalHash==before,"Swap committed before discard.");
                game.SelectTile(Id(TileColor.Black,12));await game.RecomputePreviewAsync();await Commit(game);
                Require(game.Session.GetView(0).OwnHand.Contains(Id(TileColor.Black,1)),"Recovered joker missing.");await CaptureAsync(game,"joker-swap");Record("joker-swap-input");
                await Leave(game,false);
                game=await OpenTable();game.InstallDiagnosticRound(PracticeDeals.Create("Penalty"));
                before=game.Session.CaptureReplay().ExpectedFinalHash;game.SelectTile(Id(TileColor.Red,4));await game.RecomputePreviewAsync();
                Click(game,"At ve turu onayla");var penalty=game.GetComponent<PenaltyDialog>();await Wait(()=>penalty.IsOpen);
                Click(game,"Vazgeç");await Wait(()=>!penalty.IsOpen&&game.CanControl);
                Require(game.Session.CaptureReplay().ExpectedFinalHash==before,"Penalty cancel changed authority.");await Commit(game);
                Require(game.Session.GetView(0).OwnPenalties==101,"Penalty was not applied exactly once.");await CaptureAsync(game,"penalty");Record("penalty-cancel-accept");
                await Leave(game,false);
                game=await OpenTable();Click(game,"Test paneli");var panel=game.GetComponent<DebugPanel>();
                var labels=new[]{"Seri açılışı","Çift açılışı","Okey değişimi","Bitiş ×8","Deste sonu","Dört çift iptali"};
                for(var i=0;i<labels.Length;i++)
                {
                    await Wait(()=>game.CanControl&&!game.BotThinking);await Task.Yield();Click(panel,labels[i]);VerifyFixtureInitial(game,i);await panel.StepBotAsync();
                    var expected=game.Session.CaptureReplay();var exported=await panel.ExportReplayAsync();await VerifyReplayAsync(game,panel,exported,expected);
                    Require(expected.Commands.Count==1,"Fixture did not accept exactly one turn.");
                    if(i==0)Require(game.Session.GetView(0).OwnOpening==OpeningKind.Series&&game.Session.GetView(0).Result?.Scores[0].Total==-202,"Series fixture outcome incorrect.");
                    if(i==1)Require(game.Session.GetView(0).OwnOpening==OpeningKind.Pairs&&game.Session.GetView(0).Result?.Scores[0].Total==-404,"Pairs fixture outcome incorrect.");
                    if(i==2)
                    {var restored=ReplayCodec.Restore(expected,CancellationToken.None);Require(restored.Success&&restored.Value.Table[0].Group.Tiles.Any(t=>t.Id==Id(TileColor.Red,2)),"Swap fixture did not place the replacement tile.");}
                    if(i==3)Require(game.Session.GetView(0).Result.Scores[0].Total==-808,"x8 finish incorrect.");
                    if(i==4)Require(game.Session.GetView(0).Result.FinishInfo.Reason==FinishReason.StockExhausted,"Stock end incorrect.");
                    if(i==5)Require(game.Session.GetView(0).Result.FinishInfo.Reason==FinishReason.AllPairs,"AllPairs incorrect.");
                    SaveReplay(game,"fixture-"+i);
                }
                Click(panel,"Kapat");await CaptureAsync(game,"all-pairs-result");
                starter=game.Session.CaptureReplay().InitialState.StartingPlayer;oldId=game.Session.GetView(0).RoundId;
                Click(game.GetComponentInChildren<ResultPanel>(),"Yeniden dağıt");await Wait(()=>game.Session.GetView(0).RoundId!=oldId);
                Require(game.Session.CaptureReplay().InitialState.StartingPlayer==starter,"AllPairs changed starting seat.");Record("six-fixtures-replay");await Leave(game,false);
                for(var i=0;i<10;i++)
                {
                    game=await OpenTable();Require(FindObjectsByType<GameBootstrap>().Count(g=>g.Session!=null)==1,"Duplicate session.");
                    await Leave(game,i==0);
                    Require(FindObjectsByType<TileMotion>().Length==0&&FindObjectsByType<AudioSource>().Length==0,"Menu retained motion/audio.");
                }
                Record("ten-menu-table-cycles");evidence.completed=true;
            }
            catch(Exception exception){evidence.failure=exception.ToString();throw;}
            finally{BackgroundAcceptance.FlowOutput=null;evidence.checks=checks.ToArray();evidence.completedUtc=DateTime.UtcNow.ToString("O");File.WriteAllText(Path.Combine(directory,"flow.json"),JsonUtility.ToJson(evidence,true));}
        }
        private async Task<GameBootstrap> OpenTable()
        {
            await Wait(()=>FindAnyObjectByType<MainMenuController>()?.View!=null);
            var menu=FindAnyObjectByType<MainMenuController>();Require(menu!=null,"Expected native Galaxy main menu.");
            // Programmatic selection avoids changing user preferences in editor acceptance runs.
            menu.Model.SelectedMode=4;menu.View.Refresh();
            await NextFrame();MainMenu.DiagnosticNextSeed=72;
            try
            {
                // The menu's play button opens the 3D Galaxy table; the packaged A4 flow probes the classic table.
                await menu.StartLocalAsync(MainMenuController.LegacyTableScene);
                await Wait(()=>FindAnyObjectByType<GameBootstrap>()?.Session!=null);
                var game=FindAnyObjectByType<GameBootstrap>();
                var initial=game.Session.CaptureReplay().InitialState;
                Require(initial.Seed==72&&initial.StartingPlayer==0,"Diagnostic startup seed was not consumed by the native table entry.");
                Require(game.BotsEnabled&&game.Session.GetView(0).Actor==0&&game.Session.GetView(0).Players.Count==4,"Native bot mode did not create one human and three bots.");
                game.EnableBackgroundAcceptance();game.SetPaused(false);
                await Wait(()=>game.Session.GetView(0).Result!=null||(game.CanControl&&game.Session.GetView(0).ActivePlayer==0));
                var result=game.Session.GetView(0).Result;
                Require(result==null,"Unexpected startup finish: seed="+initial.Seed+", starter="+initial.StartingPlayer+", reason="+result?.FinishInfo.Reason+".");return game;
            }
            finally{MainMenu.DiagnosticNextSeed=null;}
        }
        private async Task Commit(GameBootstrap game)
        {
            var version=game.Session.GetView(0).Version;Click(game,"At ve turu onayla");var penalty=game.GetComponent<PenaltyDialog>();
            await Wait(()=>penalty.IsOpen||game.Session.GetView(0).Version>version);
            if(penalty.IsOpen)Click(game,"Kabul et ve turu onayla");
            await Wait(()=>game.Session.GetView(0).Version>version&&game.CanControl);
        }
        private async Task Leave(GameBootstrap game,bool cancelFirst)
        {
            await Wait(()=>game.CanControl&&!game.BotThinking);var session=game.Session;
            if(game.Session.GetView(0).Result!=null)Click(game.GetComponentInChildren<ResultPanel>(),"Menüye dön");
            else
            {
                game.SetBotsEnabled(false);game.SelectTile(game.Session.GetView(0).OwnHand[0]);game.MoveSelectedTo(23);
                var hash=game.Session.CaptureReplay().ExpectedFinalHash;var selected=game.SelectedTiles[0];
                Click(game,"Duraklat");var pause=game.GetComponentInChildren<PauseMenu>();Click(pause,"Menüye dön");
                var dialog=game.GetComponentInChildren<LeaveRoundDialog>();await Wait(()=>dialog.IsOpen);
                if(cancelFirst)
                {
                    Click(dialog,"Vazgeç");await Wait(()=>!dialog.IsOpen);
                    Require(!session.IsDisposed&&game.Session.CaptureReplay().ExpectedFinalHash==hash&&game.Presenter.Rack.IndexOf(selected)==23,"Leave cancel lost round/rack.");
                    Click(pause,"Devam et");Require(!session.IsPaused,"Resume failed.");Click(game,"Duraklat");Click(pause,"Menüye dön");await Wait(()=>dialog.IsOpen);
                    Record("pause-resume-leave-cancel");
                }
                Click(dialog,"Menüye dön");
            }
            await Wait(()=>FindAnyObjectByType<MainMenuController>()?.View!=null);Require(session.IsDisposed,"Old session not disposed.");
            Require(FindAnyObjectByType<MainMenu>()==null,"Table exit restored the retired uGUI menu.");await Task.Yield();
        }
        private void SaveReplay(GameBootstrap game,string name)
        {var codec=new ReplayCodec(Path.Combine(directory,"records"));codec.Write(game.Session.CaptureReplay(),name+".json");Require(codec.Read(name+".json",CancellationToken.None).Success,"Written replay did not validate.");}
        private async Task VerifyReplayAsync(GameBootstrap game,DebugPanel panel,string exported,ReplayRecord expected)
        {
            game.InstallDiagnosticRound(RoundFactory.Create(1731,Guid.NewGuid(),0));
            Require(game.Session.CaptureReplay().ExpectedFinalHash!=expected.ExpectedFinalHash,"Replay test did not change authority first.");
            await panel.ReplayAsync(exported);var actual=game.Session.CaptureReplay();
            Require(actual.ExpectedFinalHash==expected.ExpectedFinalHash&&actual.InitialState.RoundId==expected.InitialState.RoundId
                &&actual.Commands.Count==expected.Commands.Count,"Fixture replay was rejected or did not restore expected authority.");
        }
        private static void VerifyFixtureInitial(GameBootstrap game,int index)
        {
            var hashes=new[]{"61c386634ff40cb41328db2c2d5224985cf49aa17d3507e41e5a0f94fa1fe5f8","f71038f682d6f88a8402e2cd1cf26f02149f84035e229574629a22cd70b987fa",
                "4f3c2e3661dca5d940f784b75219e80955a6f3c5df14888d4b030c79d8f7d4ac","b82ac067f82a44ea26af6d5fd4bf70eea7d57131f24177e4839923b936895ae3",
                "da9fe6798174a19a394ecddef8e223d23a939ef0e8b2e196c4d25f5076999bdc","1aa56f6a7dbf48d914be797b5648f077668ceb1f1eb1cf7ae25c752d9826ebd5"};
            Require(index>=0&&index<hashes.Length&&game.Session.CaptureReplay().ExpectedFinalHash==hashes[index],"Fixture button did not install its expected initial authority.");
        }
        private async Task CaptureAsync(GameBootstrap game,string name)
        {
            Require(game.Session.GetView(0).Result==null||game.GetComponentInChildren<ResultPanel>(true).isActiveAndEnabled,"Capture reached terminal authority before result UI settled.");
            var target=game.Presenter.TableCamera.targetTexture;if(target==null)return;
            var frame=Time.frameCount;await Wait(()=>Time.frameCount>=frame+2);
            var previous=RenderTexture.active;var pixels=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
            try
            {
                RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();
                var colors=pixels.GetPixels32();Require(colors.Count(c=>c.r+c.g+c.b>60)>colors.Length/10,"Offscreen frame is blank; render loop did not run.");
                File.WriteAllBytes(Path.Combine(directory,name+".png"),pixels.EncodeToPNG());
            }
            finally{RenderTexture.active=previous;Destroy(pixels);}
        }
        private static void Click(Component owner,string name)
        {
            var buttons=owner.GetComponentsInChildren<Button>(true).Where(b=>(b.name==name||b.GetComponentInChildren<Text>()?.text==name)&&b.IsActive()&&b.IsInteractable()).ToArray();
            Require(buttons.Length==1,"Expected one usable button: "+name+"; found "+buttons.Length);buttons[0].onClick.Invoke();
        }
        private async Task ClickNative(MainMenuController owner,string name)
        {
            var button=owner.View.Root.Q<ToolkitButton>(name);
            Require(button!=null&&button.enabledInHierarchy,"Expected one usable native button: "+name);
            button.Focus();await NextFrame();
            using(var evt=NavigationSubmitEvent.GetPooled()){evt.target=button;button.SendEvent(evt);}
            await NextFrame();
        }
        private async Task NextFrame(){var frame=Time.frameCount;await Wait(()=>Time.frameCount>frame);}
        private async Task Wait(Func<bool> condition)
        {var deadline=Time.realtimeSinceStartupAsDouble+30;while(!condition()){lifetime.Token.ThrowIfCancellationRequested();if(Time.realtimeSinceStartupAsDouble>deadline)throw new TimeoutException("A4 flow condition timed out after "+string.Join(",",checks));await Task.Yield();}}
        private void Record(string check){checks.Add(check);Debug.Log("A4_FLOW_CHECK "+check);}
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        private static TileId Id(TileColor color,int number,int copy=0)=>new TileId(2*(13*(int)color+number-1)+copy);
        private void OnDestroy(){lifetime.Cancel();lifetime.Dispose();}
    }
}
#endif
