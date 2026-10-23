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
            ApplySettings();background.Configure(Model.Planet,(bool)Model.Settings["motion"],(string)Model.Settings["quality"]);
            background.SetFocused(UnityEngine.Application.isFocused||UnityEngine.Application.isBatchMode);
            Debug.Log("GALAXY101_MENU_READY version="+UnityEngine.Application.version+" theme="+Model.Planet+" language="+(string)Model.Settings["lang"]);
            // spec/18: splash → sign-in (or a remembered account) before the menu. Batch runs (tests, captures) skip it.
            if(AuthOnStart??!UnityEngine.Application.isBatchMode)ShowAuth();
        }
        /// <summary>null = sign in unless running in batch mode; tests force it on or off.</summary>
        public static bool? AuthOnStart;
        public AuthFlow Auth {get;private set;}
        public void ShowAuth()
        {
            var online=Galaxy101.Online.GalaxyOnline.Ensure();
            Auth=new AuthFlow(View,transform,online);
            Auth.Completed+=()=>{ApplyOnline(online);online.Changed+=()=>ApplyOnline(online);};
            if(!invitesHooked){invitesHooked=true;online.Realtime.On<Galaxy101.Contracts.InviteMessage>(Galaxy101.Contracts.MessageTypes.Invite,message=>{if(this==null||Auth!=null&&Auth.Visible)return;OpenLobby();Lobby.ShowInvite(message);});}
            Auth.Show();
        }
        /// <summary>The menu shows the server's name, colour and balance once signed in.</summary>
        private void ApplyOnline(Galaxy101.Online.GalaxyOnline online)
        {
            if(online.Profile!=null){Model.Profile["name"]=online.Profile.Name;Model.Profile["color"]=online.Profile.Color;}
            if(online.Wallet!=null){Model.Chips=online.Wallet.Chips;Model.Gems=online.Wallet.Gems;}
            View.Refresh();
        }
        private bool invitesHooked;
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
            OnPlay(4,GalaxyTableScene);
        }
        private void OnPlay(int mode)=>OnPlay(mode,GalaxyTableScene);
        private async void OnPlay(int mode,string scene)
        {
            PlayRequested?.Invoke(mode);Debug.Log("GALAXY101_PLAY_REQUESTED mode="+mode);
            if(mode==0){OpenLobby();return;}
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
            bool offscreen=UnityEngine.Application.isBatchMode;
            GameDisplay.Apply((string)Model.Settings["wmode"],(string)Model.Settings["res"]);
            if(!offscreen)AudioListener.volume=(int)Model.Settings["master"]/100f;
            FramePacing.Apply((int)Model.Settings["fps"]);
        }
        private void OnApplicationFocus(bool value){background?.SetFocused(value||UnityEngine.Application.isBatchMode);View?.SetFocused(value||UnityEngine.Application.isBatchMode);}
        private void OnDestroy()
        {
            if(Model!=null)Model.Changed-=ApplySettings;
            if(View!=null){if(background!=null)View.BackgroundChanged-=background.Configure;View.PlayRequested-=OnPlay;View.Dispose();}
            localization?.Dispose();lobbyClient?.Dispose();
        }
    }
}
