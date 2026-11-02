using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Galaxy101
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class MainMenuController : MonoBehaviour
    {
        public MenuView View {get;private set;}
        public MenuModel Model {get;private set;}
        private GalaxyBackground background;
        private GalaxyShips ships;
        private VisualElement shipsLayer;
        private AuthCursorVisual menuCursor;
        private UIDocument document;
        private bool navigating;
        private LocalizationBridge localization;
        public event Action<int> PlayRequested;
        /// <summary>"Botlara karşı" opens the 3D Galaxy table; the legacy diagnostic probes keep the classic table.</summary>
        public const string GalaxyTableScene="GalaxyTable",LegacyTableScene="Table";
        private void Start()
        {
            Initialize();
            Galaxy101.Table.ThreeEnvironment.Prewarm(); // the table's reflection cube, on worker threads while the menu is up
#if UNITY_EDITOR || OKEY_DIAGNOSTICS
            var args=Environment.GetCommandLineArgs();
            if(Array.IndexOf(args,"-okeyPractice")>=0||Array.IndexOf(args,"-okeyA3Output")>=0||Array.IndexOf(args,"-okeyA4Output")>=0)OnPlay(4,LegacyTableScene);
#endif
        }
        public void Initialize(string persistencePath=null)
        {
            if(View!=null)return;
            document=GetComponent<UIDocument>();
            if(document.panelSettings==null)document.panelSettings=Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
            if(document.panelSettings==null)throw new InvalidOperationException("Missing authored Galaxy101 PanelSettings.");
            var presenter=GetComponent<MenuGammaPresenter>()??gameObject.AddComponent<MenuGammaPresenter>();presenter.Initialize(document);
            string path=persistencePath??GalaxyPreferencesBridge.ResolveStatePath();
            var content=Resources.Load<TextAsset>("Galaxy101/content");var themes=Resources.Load<TextAsset>("Galaxy101/themes");
            if(content==null||themes==null)throw new InvalidOperationException("Galaxy101 menu data resources are missing.");
            Model=new MenuModel(content.text,themes.text,path);var services=new MenuServices(Model);var loc=new LocalizationBridge(Model);localization=loc;
            View=new MenuView(document.rootVisualElement,Model,services,loc);View.BuildMain();
            menuCursor=new AuthCursorVisual(View.Root,()=>Auth==null||!Auth.Visible,"game-cursor",()=>Model.Planet,()=>View.C("var(--a1)"));
            var effects=new GameObject("Galaxy background");effects.transform.SetParent(transform,false);background=effects.AddComponent<GalaxyBackground>();background.Initialize(View.BackgroundTarget);
            View.BackgroundChanged+=background.Configure;View.PlayRequested+=OnPlay;Model.Changed+=ApplySettings;
            shipsLayer=View.Box("fill");shipsLayer.name="menu.ships";shipsLayer.pickingMode=PickingMode.Ignore;
            View.Root.Q<VisualElement>("main-layer").Insert(0,shipsLayer);
            ships=new GameObject("Galaxy ships").AddComponent<GalaxyShips>();ships.transform.SetParent(transform,false);
            ships.Initialize(shipsLayer);ships.UseMenuLayout();View.BackgroundChanged+=ships.Configure;
            ships.Configure(Model.Planet,(bool)Model.Settings["motion"],(string)Model.Settings["quality"]);
            Galaxy101.Audio.GalaxyAudio.TrackScreens(this,()=>Auth!=null&&Auth.Visible?Auth.Screen:Lobby!=null&&Lobby.Visible?"lobby":Ranked!=null&&Ranked.Visible?"ranked":"menu"); // v4 audio: screen music + ui.screen
            ApplySettings();background.Configure(Model.Planet,(bool)Model.Settings["motion"],(string)Model.Settings["quality"]);
            background.SetFocused(UnityEngine.Application.isFocused||UnityEngine.Application.isBatchMode);
            Debug.Log("GALAXY101_MENU_READY version="+UnityEngine.Application.version+" theme="+Model.Planet+" language="+(string)Model.Settings["lang"]);
            // spec/18: splash → sign-in (or a remembered account) before the menu. Batch runs (tests, captures) skip it.
            if(AuthOnStart??!UnityEngine.Application.isBatchMode)
            {
                // Back from a table with the session still open: no splash or sign-in again.
                var signed=Galaxy101.Online.GalaxyOnline.Instance;
                if(signed!=null&&signed.SignedIn&&!signed.Replaced){ApplyOnline(signed);HookOnline(signed);if(ReturnToLobby){ReturnToLobby=false;if(FromRanked){FromRanked=false;OpenRanked();}else OpenLobby();}if(PendingToast!=null){View.Toast(PendingToast);PendingToast=null;}}
                else ShowAuth();
            }
        }
        /// <summary>null = sign in unless running in batch mode; tests force it on or off.</summary>
        public static bool? AuthOnStart;
        public AuthFlow Auth {get;private set;}
        public void ShowAuth()
        {
            var online=Galaxy101.Online.GalaxyOnline.Ensure();
            Auth=new AuthFlow(View,transform,online);
            Auth.Completed+=()=>{View.CloseAllModals();ApplyOnline(online);HookOnline(online);};
            Auth.Show();
        }
        /// <summary>The menu shows the server's profile card and balance once signed in; signed out, the sign-in comes back.</summary>
        private void ApplyOnline(Galaxy101.Online.GalaxyOnline online)
        {
            if(!online.SignedIn||online.Replaced){Lobby?.Hide();lobbyClient?.Dispose();lobbyClient=null;Lobby=null;Ranked?.Hide();Ranked=null;if(Auth==null||!Auth.Visible)ShowAuth();return;}
            if(online.Profile!=null){Model.Profile["name"]=online.Profile.Name;Model.Profile["color"]=online.Profile.Color;Model.Profile["status"]=online.Profile.Status??"";Model.Profile["frame"]=online.Profile.Frame;if(online.Profile.Progress!=null){Model.Level=online.Profile.Progress.Level;Model.Xp=online.Profile.Progress.LevelXp;}}
            if(online.Profile!=null&&!string.IsNullOrEmpty(online.Profile.FriendCode))Model.FriendCode=online.Profile.FriendCode;
            if(online.Wallet!=null){Model.Chips=online.Wallet.Chips;Model.Gems=online.Wallet.Gems;Model.OwnedSkins.Clear();foreach(var item in online.Wallet.OwnedItems??new System.Collections.Generic.List<string>())Model.OwnedSkins.Add(item);}
            View.Refresh();
        }
        private bool invitesHooked;
        public DevTestMenu DevMenu {get;private set;}
        /// <summary>spec/25: open the 3D table straight into a test scenario (developer menu).</summary>
        public void StartScenario(string key)
        {
            Galaxy101.Table.TableGame.PendingScenario=key;
            _=StartLocalAsync(GalaxyTableScene);
        }
        private void Update()
        {
            if(shipsLayer!=null)shipsLayer.style.display=Auth!=null&&Auth.Visible?DisplayStyle.None:DisplayStyle.Flex;
            if(!DevTestMenu.Available||View==null)return;
            var keyboard=UnityEngine.InputSystem.Keyboard.current;
            if(keyboard!=null&&keyboard.f9Key.wasPressedThisFrame){DevMenu=DevMenu??new DevTestMenu(View,StartScenario);DevMenu.Toggle();}
        }
        /// <summary>Set by an online table when it closes: the menu opens straight into the lobby.</summary>
        public static bool ReturnToLobby;
        /// <summary>The table was a Dereceli match: back to the Dereceli screen (its result card), not the Klasik lobby.</summary>
        public static bool FromRanked;
        /// <summary>The Dereceli screen (spec/27, S16); its backend is the signed-in session unless the state router or a test supplies one.</summary>
        public RankedScreen Ranked {get;private set;}
        public static Func<IRankedBackend> RankedBackend;
        /// <summary>Dereceli (HEMEN OYNA): the Dereceli screen; its MAÇ ARA starts the server's queue.</summary>
        private void StartRanked()=>OpenRanked();
        public void OpenRanked()
        {
            Initialize();if(Ranked!=null&&Ranked.Visible)return;
            if(Ranked==null)
            {
                IRankedBackend backend=RankedBackend?.Invoke();
                if(backend==null)
                {
                    var online=Galaxy101.Online.GalaxyOnline.Instance;
                    if(online==null||!online.SignedIn){View.Toast(View.L("Dereceli için giriş yapmalısın","Sign in to play ranked"));return;}
                    backend=RankedOnlineBackend.For(online);
                }
                Ranked=new RankedScreen(View,backend);Ranked.Matched+=OnRankedMatched;
            }
            Ranked.Show();
        }
        /// <summary>
        /// The ranked match the table opens with (v4 protocol): RankedStart.Match is the server's V4MatchStart (table id, seats,
        /// countdownMs), Seat your seat, Mode solo / duo / squad. The table lane (v4/table) takes it from here at scene start
        /// (docs/v4/ranked/RAPOR.md §4); null for the demo season's local table.
        /// </summary>
        public static RankedStart PendingRanked;
        /// <summary>Everyone accepted and matchStart came: the table opens, "Dereceli · Solo".</summary>
        private void OnRankedMatched(RankedStart start)
        {
            if(this==null||start==null||!OpenMatchTable)return;
            PendingRanked=start.Match!=null?start:null;
            // The server's match (v4 matchStart) plays online; the demo season's match is a local table (R-94: 5 hands, 30 s, no stake).
            var session=Galaxy101.Online.V4TableSession.Current;
            Galaxy101.Table.TableGame.Pending=new Galaxy101.Table.TableLaunch{Session=session!=null&&!session.Closed?session:null,Room=View.L("Dereceli · ","Ranked · ")+(start.Mode=="duo"?"Duo":start.Mode=="squad"?"Squad":"Solo")};
            FromRanked=true;
            _=StartLocalAsync(GalaxyTableScene);
        }
        private Action rankedOff;
        /// <summary>A party leader put this player in a ranked search (or a match was found) while the screen was closed: open it.</summary>
        private void HookRanked(Galaxy101.Online.GalaxyOnline online)
        {
            rankedOff?.Invoke();
            var ranked=RankedOnlineBackend.For(online);
            Action open=()=>{if(this!=null&&RankedBackend==null&&(Auth==null||!Auth.Visible))OpenRanked();};
            ranked.Incoming+=open;rankedOff=()=>ranked.Incoming-=open;
        }
        private System.Action onlineChanged;
        private void HookOnline(Galaxy101.Online.GalaxyOnline online)
        {
            View.Services.Online.Load(null); // stats, daily rewards, friends and settings for the menu and its badges
            if(onlineChanged==null){onlineChanged=()=>{if(this!=null)ApplyOnline(online);};online.Changed+=onlineChanged;}
            if(!invitesHooked){invitesHooked=true;inviteOff=online.Realtime.On<Galaxy101.Contracts.InviteMessage>(Galaxy101.Contracts.MessageTypes.Invite,message=>{if(this==null||Auth!=null&&Auth.Visible)return;OpenLobby();Lobby.ShowInvite(message);});}
            HookRanked(online); // ranked: a party leader's queue opens the Dereceli screen here too
        }
        private System.Action inviteOff;
        /// <summary>The Klasik lobby (spec/20); its backend is the signed-in session unless a test supplies one.</summary>
        public LobbyScreen Lobby {get;private set;}
        public static Func<Galaxy101.Online.ILobbyBackend> LobbyBackend;
        /// <summary>Tests keep the lobby on screen when a match starts.</summary>
        public static bool OpenMatchTable=true;
        private Galaxy101.Online.LobbyClient lobbyClient;
        public void OpenLobby()
        {
            Initialize();if(Lobby!=null&&Lobby.Visible)return;
            if(Lobby==null)
            {
                Galaxy101.Online.ILobbyBackend backend=LobbyBackend?.Invoke();
                if(backend==null){lobbyClient=new Galaxy101.Online.LobbyClient(Galaxy101.Online.GalaxyOnline.Ensure());backend=lobbyClient;}
                Lobby=new LobbyScreen(View,backend);
                Lobby.BuyRequested+=()=>View.OpenPanel("shop");
                Lobby.Closed+=()=>background?.UseMenuLayout();
                Lobby.MatchStarted+=OnMatchStarted;
            }
            // S09: the planet rises to the top-right corner behind the lobby.
            background?.UseStaticLayout(new Vector4(900,120,1300,760),new Vector4(1215,60,300,1),new Vector4(1215,60,.95f,0),View.C("var(--pg)"));
            Lobby.Show();
        }
        /// <summary>
        /// A lobby table became a match (the earlier protocol's lobby). The table plays the v4 protocol: the match's
        /// matchStart has opened it already (<see cref="OnMatchStarted(Galaxy101.Online.V4TableSession)"/>); an earlier
        /// protocol's match has no table to play on.
        /// </summary>
        private void OnMatchStarted(Galaxy101.Contracts.TableStateMessage state)
        {
            Debug.Log("GALAXY101_MATCH_STARTED match="+state.MatchId+" seat="+state.YourSeat);
            var session=Galaxy101.Online.V4TableSession.Current;
            if(session!=null&&!session.Closed)OnMatchStarted(session);
        }
        /// <summary>A v4 matchStart (waiting room, invite, ranked, vsBotsStart): the table opens on the server's match.</summary>
        public void OnMatchStarted(Galaxy101.Online.V4TableSession session)
        {
            if(session==null||!OpenMatchTable||this==null||navigating)return;
            Lobby?.Hide();
            var table=session.Client.Table;
            string room=table==null?null:table.Ranked?View.L("Dereceli","Ranked"):table.VsBots?null:table.Name+" · "+(table.Mode=="pairs"?View.L("Eşli","Pairs"):View.L("Tekli","Singles"));
            Galaxy101.Table.TableGame.Pending=new Galaxy101.Table.TableLaunch{Session=session,Room=room,Stake=table?.Bet??0};
            FromRanked=table!=null&&table.Ranked;
            _=StartLocalAsync(GalaxyTableScene);
        }
        /// <summary>Set by the table when a match ends (REMOVED.md T45/T46: no match-end screen): the lobby shows it as a toast.</summary>
        public static string PendingToast;
        private void OnPlay(int mode)=>OnPlay(mode,GalaxyTableScene);
        private async void OnPlay(int mode,string scene)
        {
            PlayRequested?.Invoke(mode);Debug.Log("GALAXY101_PLAY_REQUESTED mode="+mode);
            if(mode==0){OpenLobby();return;}
            if(mode==3){StartRanked();return;}
            if(mode!=4||navigating)return;
            // Botlara karşı (K-10, R-94): the server's match code offline with three bots; no chips, XP (D-42).
            if(scene==GalaxyTableScene)Galaxy101.Table.TableGame.Pending=new Galaxy101.Table.TableLaunch{VsBots=true};
            try{await StartLocalAsync(scene);}
            catch(Exception exception){Debug.LogException(exception);if(this!=null)View.Toast(View.L("Masa açılamadı. Yeniden deneyin.","Could not open the table. Try again."));}
        }
        /// <summary>The loading screen's line (prototype g.kick): classic table, ranked or (null) against bots.</summary>
        private string LoadingKick(Galaxy101.Table.TableLaunch launch)
        {
            var table=launch?.Session?.Client?.Table??launch?.Table;
            if(table==null||table.VsBots&&!table.Ranked)return null;
            string hands=" · 101 · "+table.Hands+View.L(" EL"," HANDS");
            if(table.Ranked)return View.L("DERECELİ","RANKED")+hands;
            return View.L("KLASİK · ","CLASSIC · ")+(table.Mode=="pairs"?View.L("EŞLİ","PAIRS"):View.L("TEKLİ","SINGLES"))+hands;
        }
        /// <summary>Seconds before the loading screen gives up and shows "Masa açılamadı" (spec/17 §9).</summary>
        public float TableLoadTimeout {get;set;}=15;
        public TableLoadingOverlay Loading {get;private set;}
        public async Task StartLocalAsync(string scene=GalaxyTableScene)
        {
            Initialize();if(navigating||View.Modals.Panel!=null||View.Modals.Dialog!=null)return;navigating=true;
            bool overlay=scene==GalaxyTableScene;
            var launch=Galaxy101.Table.TableGame.Pending;
            // T51 "Lobiye dön" while the scene still loads: it cannot be stopped, so the table it opens goes straight back.
            void BackToLobby(){Loading.Hide();if(launch!=null)launch.Cancelled=true;}
            try
            {
                if(overlay){Loading=Loading??new TableLoadingOverlay(View,transform);Loading.Show(Galaxy101.Table.TableGame.Pending?.Table?.Hands??Galaxy101.Table.TableRules.Current.MatchHands,LoadingKick(Galaxy101.Table.TableGame.Pending));}
                else View.Root.SetEnabled(false);
                var load=SceneManager.LoadSceneAsync(scene,LoadSceneMode.Single);
                if(load==null)
                {
                    if(!overlay)throw new InvalidOperationException(scene+" scene is missing from the Windows build.");
                    Loading.ShowError(()=>Loading.Hide(),()=>{navigating=false;_=StartLocalAsync(scene);});return;
                }
                float deadline=Time.realtimeSinceStartup+TableLoadTimeout;
                while(!load.isDone)
                {
                    if(overlay)
                    {
                        Loading.SetProgress(load.progress/.9f);
                        // T51: a load that has not finished in time offers the lobby or another wait.
                        if(Time.realtimeSinceStartup>deadline&&!Loading.ErrorVisible&&launch?.Cancelled!=true)Loading.ShowError(BackToLobby,()=>deadline=Time.realtimeSinceStartup+TableLoadTimeout);
                    }
                    await Task.Yield();
                }
            }
            finally{if(this!=null){navigating=false;View.Root.SetEnabled(true);}}
        }
        private void ApplySettings()
        {
            if(Model==null)return;
            GameDisplay.Apply((string)Model.Settings["wmode"],(string)Model.Settings["res"]);
            Galaxy101.Audio.GalaxyAudio.ApplySettings(Model.Settings); // v4 audio: master × music/sfx per source (was AudioListener.volume = master)
            FramePacing.Apply((int)Model.Settings["fps"]);
        }
        private void OnApplicationFocus(bool value){background?.SetFocused(value||UnityEngine.Application.isBatchMode);ships?.SetFocused(value||UnityEngine.Application.isBatchMode);View?.SetFocused(value||UnityEngine.Application.isBatchMode);}
        private void OnDestroy()
        {
            Auth?.Hide();
            menuCursor?.Dispose();
            if(View!=null&&ships!=null)View.BackgroundChanged-=ships.Configure;
            if(Model!=null)Model.Changed-=ApplySettings;
            if(View!=null){if(background!=null)View.BackgroundChanged-=background.Configure;View.PlayRequested-=OnPlay;View.Dispose();}
            localization?.Dispose();lobbyClient?.Dispose();inviteOff?.Invoke();rankedOff?.Invoke();
            var online=Galaxy101.Online.GalaxyOnline.Instance;if(online!=null&&onlineChanged!=null)online.Changed-=onlineChanged;
        }
    }
}
