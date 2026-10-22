using System.Threading.Tasks;
using System;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Okey101.Presentation
{
    public sealed class MainMenu : MonoBehaviour
    {
        [SerializeField] private Button play,settings,quit;
        [SerializeField] private SettingsPanel settingsPanel;
        [SerializeField] private Text error;
        private bool initialized;
        private Task navigation;
        public void BuildView()
        {
            var cameraObject=new GameObject("Menu camera",typeof(Camera));cameraObject.transform.SetParent(transform,false);
            var camera=cameraObject.GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.cullingMask=0;camera.backgroundColor=new Color(.025f,.075f,.06f);
            var canvas=MenuUI.Canvas(transform,"Main menu interface");
            var background=TableUI.Panel(canvas,"Forest background",Vector2.zero,Vector2.one,new Color(.025f,.075f,.06f));
            TableUI.Panel(background,"Brass divider",new Vector2(.46f,.16f),new Vector2(.462f,.84f),new Color(.6f,.43f,.20f));
            var logo=MenuUI.Text(background,"101",new Vector2(.06f,.45f),new Vector2(.42f,.76f),210);logo.color=new Color(.88f,.73f,.46f);
            MenuUI.Text(background,"OKEY",new Vector2(.06f,.33f),new Vector2(.42f,.47f),64);
            MenuUI.Text(background,"Bir masa. Bir el. Keyifli oyun.",new Vector2(.06f,.23f),new Vector2(.42f,.32f),27);
            MenuUI.Text(background,"EŞSİZ 101",new Vector2(.53f,.74f),new Vector2(.89f,.84f),48);
            MenuUI.Text(background,"Sabit 101  ·  En az 5 çift\nSiz ve üç yerel bot",new Vector2(.53f,.61f),new Vector2(.89f,.73f),26);
            play=TableUI.Button(background,"Yerel Oyna",new Vector2(.55f,.46f),new Vector2(.87f,.56f),null,32);
            settings=TableUI.Button(background,"Ayarlar",new Vector2(.55f,.33f),new Vector2(.87f,.42f),null,28);
            quit=TableUI.Button(background,"Çıkış",new Vector2(.55f,.20f),new Vector2(.87f,.29f),null,28);
            error=MenuUI.Text(background,"",new Vector2(.51f,.10f),new Vector2(.91f,.18f),23);
            MenuUI.Text(background,UnityEngine.Application.version+"  ·  "+Okey101.Application.ReplayRecord.RuleVersion,new Vector2(.06f,.06f),new Vector2(.42f,.13f),22);
            var panel=new GameObject("Settings",typeof(RectTransform),typeof(SettingsPanel));panel.transform.SetParent(canvas,false);
            settingsPanel=panel.GetComponent<SettingsPanel>();settingsPanel.BuildView();panel.SetActive(false);
        }
        private void Initialize()
        {
            if(initialized)return;initialized=true;if(play==null)BuildView();MenuUI.EnsureEvents(transform);
            MenuUI.Apply(MenuUI.LoadSettings());
            play.onClick.AddListener(StartFromUI);settings.onClick.AddListener(()=>settingsPanel.Show(MenuUI.LoadSettings(),null));
            quit.onClick.AddListener(()=>{if(!settingsPanel.IsOpen)UnityEngine.Application.Quit();});
            Debug.Log("T12_MENU_READY");
        }
        private void Start()
        {
            Initialize();
#if UNITY_EDITOR || OKEY_DIAGNOSTICS
            var args=Environment.GetCommandLineArgs();
            if(Array.IndexOf(args,"-okeyPractice")>=0||Array.IndexOf(args,"-okeyA3Output")>=0||Array.IndexOf(args,"-okeyA4Output")>=0)StartFromUI();
#endif
        }
        private async void StartFromUI()
        {
            try { await StartLocalAsync(); }
            catch(Exception exception) { if(this!=null){Debug.LogException(exception);error.text="Masa açılamadı. Yeniden deneyin.";} }
        }
        public Task StartLocalAsync()
        {
            Initialize();
            if(settingsPanel.IsOpen)return Task.CompletedTask;
            if(navigation!=null&&!navigation.IsCompleted)return navigation;
            navigation=OpenTableAsync();return navigation;
        }
        private async Task OpenTableAsync()
        {
            play.interactable=false;settings.interactable=false;quit.interactable=false;
            try
            {
                var load=SceneManager.LoadSceneAsync("Table",LoadSceneMode.Single);
                while(!load.isDone)await Task.Yield();
            }
            finally { if(this!=null) {play.interactable=true;settings.interactable=true;quit.interactable=true;} }
        }
#if UNITY_EDITOR || OKEY_DIAGNOSTICS
        private static long diagnosticNextSeed=-1;
        /// <summary>One-shot diagnostic deal seed. Ordinary player builds always use cryptographic randomness.</summary>
        public static uint? DiagnosticNextSeed
        {
            get{long value=System.Threading.Interlocked.Read(ref diagnosticNextSeed);return value<0?(uint?)null:(uint)value;}
            set{System.Threading.Interlocked.Exchange(ref diagnosticNextSeed,value.HasValue?(long)value.Value:-1);}
        }
#endif
        internal static uint NewSeed()
        {
#if UNITY_EDITOR || OKEY_DIAGNOSTICS
            long diagnostic=System.Threading.Interlocked.Exchange(ref diagnosticNextSeed,-1);
            if(diagnostic>=0)return (uint)diagnostic;
#endif
            var bytes=new byte[4];using(var random=RandomNumberGenerator.Create())random.GetBytes(bytes);return BitConverter.ToUInt32(bytes,0);
        }
    }
}
