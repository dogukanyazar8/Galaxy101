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
            GalaxyCursor.Apply(); // Ayarlar → Görüntü → İmleç (default Normal: the Galaxy 101 cursor)
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
            var effects=new GameObject("Galaxy background");effects.transform.SetParent(transform,false);background=effects.AddComponent<GalaxyBackground>();background.Initialize(View.BackgroundTarget);
            View.BackgroundChanged+=background.Configure;View.PlayRequested+=OnPlay;Model.Changed+=ApplySettings;
            Galaxy101.Audio.GalaxyAudio.TrackScreens(this,()=>Auth!=null&&Auth.Visible?Auth.Screen:Lobby!=null&&Lobby.Visible?"lobby":Ranked!=null&&Ranked.Visible?"ranked":"menu"); // v4 audio: screen music + ui.screen
            ApplySettings();background.Configure(Model.Planet,(bool)Model.Settings["motion"],(string)Model.Settings["quality"]);
            background.SetFocused(UnityEngine.Application.isFocused||UnityEngine.Application.isBatchMode);
            Debug.Log("GALAXY101_MENU_READY version="+UnityEngine.Application.version+" theme="+Model.Planet+" language="+(string)Model.Settings["lang"]);
            // spec/18: splash → sign-in (or a remembered account) before the menu. Batch runs (tests, captures) skip it.
            if(AuthOnStart??!UnityEngine.Application.isBatchMode)
            {
                // Back from a table with the session still open: no splash or sign-in again.
                var signed=Galaxy101.Online.GalaxyOnline.Instance;
                if(signed!=null&&signed.SignedIn){ApplyOnline(signed);HookOnline(signed);if(ReturnToLobby){ReturnToLobby=false;if(FromRanked){FromRanked=false;OpenRanked();}else OpenLobby();}}
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
            Auth.Completed+=()=>{ApplyOnline(online);HookOnline(online);};
            Auth.Show();
        }
        /// <summary>The menu shows the server's profile card and balance once signed in; signed out, the sign-in comes back.</summary>
        private void ApplyOnline(Galaxy101.Online.GalaxyOnline online)
        {
            if(!online.SignedIn){if(Auth==null||!Auth.Visible)ShowAuth();return;}
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
                    backend=new RankedOnlineBackend(online);
                }
                Ranked=new RankedScreen(View,backend);Ranked.Matched+=OnRankedMatched;
            }
            Ranked.Show();
        }
        /// <summary>Both accepted: the table opens (the server's match, or a local table for the demo season), "Dereceli · Solo".</summary>
        private void OnRankedMatched(RankedStart start)
        {
            if(this==null||start==null||!OpenMatchTable)return;
            var online=Galaxy101.Online.GalaxyOnline.Instance;
            if(start.Match is Galaxy101.Contracts.MatchStartMessage match&&online!=null)Galaxy101.Table.TableGame.PendingOnline=new Galaxy101.Table.NetworkTableSession(online,match.MatchId);
            Galaxy101.Table.TableGame.PendingRoom=View.L("Dereceli · ","Ranked · ")+(start.Mode=="duo"?"Duo":start.Mode=="squad"?"Squad":"Solo");
            FromRanked=true;
            _=StartLocalAsync(GalaxyTableScene);
        }
        private System.Action onlineChanged;
        private void HookOnline(Galaxy101.Online.GalaxyOnline online)
        {
            View.Services.Online.Load(null); // stats, daily rewards, friends and settings for the menu and its badges
            if(onlineChanged==null){onlineChanged=()=>{if(this!=null)ApplyOnline(online);};online.Changed+=onlineChanged;}
            if(!invitesHooked){invitesHooked=true;inviteOff=online.Realtime.On<Galaxy101.Contracts.InviteMessage>(Galaxy101.Contracts.MessageTypes.Invite,message=>{if(this==null||Auth!=null&&Auth.Visible)return;OpenLobby();Lobby.ShowInvite(message);});}
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
        /// A lobby table became a match. Until the networked table lands (V7) the 3D table opens with the same options
        /// against the local engine, so the flow from lobby to table is complete.
        /// </summary>
        private void OnMatchStarted(Galaxy101.Contracts.TableStateMessage state)
        {
            Debug.Log("GALAXY101_MATCH_STARTED match="+state.MatchId+" seat="+state.YourSeat);
            if(!OpenMatchTable)return;
            Lobby?.Hide();
            var online=Galaxy101.Online.GalaxyOnline.Instance;
            if(online!=null&&state.MatchId.HasValue)
            {
                Galaxy101.Table.TableGame.PendingOnline=new Galaxy101.Table.NetworkTableSession(online,state.MatchId.Value);
                Galaxy101.Table.TableGame.PendingRoom=state.Table==null?null:state.Table.Name+" · "+(state.Table.Type=="pairs"?View.L("Eşli","Pairs"):View.L("Tekli","Singles"));
            }
            _=StartLocalAsync(GalaxyTableScene);
        }
        private void OnPlay(int mode)=>OnPlay(mode,GalaxyTableScene);
        private async void OnPlay(int mode,string scene)
        {
            PlayRequested?.Invoke(mode);Debug.Log("GALAXY101_PLAY_REQUESTED mode="+mode);
            if(mode==0){OpenLobby();return;}
            if(mode==3){StartRanked();return;}
            if(mode!=4||navigating)return;
            try{await StartLocalAsync(scene);}
            catch(Exception exception){Debug.LogException(exception);if(this!=null)View.Toast(View.L("Masa açılamadı. Yeniden deneyin.","Could not open the table. Try again."));}
        }
        /// <summary>Seconds before the loading screen gives up and shows "Masa açılamadı" (spec/17 §9).</summary>
        public float TableLoadTimeout {get;set;}=15;
        public TableLoadingOverlay Loading {get;private set;}
        public async Task StartLocalAsync(string scene=GalaxyTableScene)
        {
            Initialize();if(navigating||View.Modals.Panel!=null||View.Modals.Dialog!=null)return;navigating=true;
            bool overlay=scene==GalaxyTableScene;
            try
            {
                if(overlay){Loading=Loading??new TableLoadingOverlay(View,transform);Loading.Show(Galaxy101.Table.TableRules.Current.MatchHands);}
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
                        if(Time.realtimeSinceStartup>deadline&&!Loading.ErrorVisible)Loading.ShowError(()=>Loading.Hide(),()=>deadline=Time.realtimeSinceStartup+TableLoadTimeout);
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
        private void OnApplicationFocus(bool value){background?.SetFocused(value||UnityEngine.Application.isBatchMode);View?.SetFocused(value||UnityEngine.Application.isBatchMode);}
        private void OnDestroy()
        {
            if(Model!=null)Model.Changed-=ApplySettings;
            if(View!=null){if(background!=null)View.BackgroundChanged-=background.Configure;View.PlayRequested-=OnPlay;View.Dispose();}
            localization?.Dispose();lobbyClient?.Dispose();inviteOff?.Invoke();
            var online=Galaxy101.Online.GalaxyOnline.Instance;if(online!=null&&onlineChanged!=null)online.Changed-=onlineChanged;
        }
    }
}
