using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Okey101.Application;
using UnityEngine;
using UnityEngine.UI;
namespace Okey101.Presentation
{
    public sealed class DebugPanel : MonoBehaviour
    {
#if UNITY_EDITOR || OKEY_DIAGNOSTICS
        [SerializeField] private TextAsset[] fixtures = Array.Empty<TextAsset>();
        private GameBootstrap game;
        private ReplayCodec codec;
        private ScenarioLoader loader;
        private Text status;
        private InputField filename;
        private GameObject card;
        private readonly List<Button> actions = new List<Button>();
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private bool working, destroyed;
        public void Configure(TextAsset[] value) { fixtures = value ?? Array.Empty<TextAsset>(); }
        public void Initialize(GameBootstrap owner, string root = null)
        {
            if (game != null) return;
            game = owner; root = root ?? (BackgroundAcceptance.FlowOutput!=null?Path.Combine(BackgroundAcceptance.FlowOutput,"diagnostics")
                :BackgroundAcceptance.Requested?Path.Combine(BackgroundAcceptance.Output,"diagnostics"):Path.Combine(UnityEngine.Application.persistentDataPath, "Diagnostics"));
            codec = new ReplayCodec(root); loader = new ScenarioLoader(root);
            var overlay = new GameObject("Development diagnostics",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            overlay.transform.SetParent(transform,false); var canvas=overlay.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
            var scaler=overlay.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            TableUI.Button(overlay.transform,"Test paneli",new Vector2(.83f,.81f),new Vector2(.975f,.875f),Toggle);
            var shade=TableUI.Panel(overlay.transform,"Diagnostics overlay",Vector2.zero,Vector2.one,new Color(0,0,0,.8f));card=shade.gameObject;
            var body=TableUI.Panel(shade,"Diagnostic card",new Vector2(.12f,.12f),new Vector2(.88f,.88f));
            TableUI.Label(body,"Diagnostics title",new Vector2(.02f,.87f),new Vector2(.83f,.98f),30).text="YEREL TEST / 101-v1";
            TableUI.Button(body,"Kapat",new Vector2(.84f,.89f),new Vector2(.98f,.97f),Toggle);
            var labels=new[]{"Seri açılışı","Çift açılışı","Okey değişimi","Bitiş ×8","Deste sonu","Dört çift iptali"};
            for(var i=0;i<fixtures.Length;i++)
            {
                var index=i; var column=i%3;var row=i/3;
                actions.Add(TableUI.Button(body,i<labels.Length?labels[i]:fixtures[i].name,new Vector2(.03f+column*.32f,.71f-row*.12f),new Vector2(.33f+column*.32f,.81f-row*.12f),()=>RunUI(async()=>{
                    var imported=loader.Import(fixtures[index].text);
                    if(!imported.Success) { Message("Senaryo doğrulanamadı; mevcut el korundu.");return; }
                    LoadScenario(imported.Value); await Task.CompletedTask;
                })));
            }
            actions.Add(TableUI.Button(body,"Aktif koltuk: bot adımı",new Vector2(.03f,.43f),new Vector2(.48f,.53f),()=>RunUI(StepBotAsync)));
            actions.Add(TableUI.Button(body,"Aynı dağıtımı yeniden başlat",new Vector2(.51f,.43f),new Vector2(.97f,.53f),()=>RunUI(async()=>{
                game.InstallDiagnosticRound(game.Session.CaptureReplay().InitialState);Message("Aynı dağıtım yeniden yüklendi.");await Task.CompletedTask;
            })));
            actions.Add(TableUI.Button(body,"Kaydı dışa aktar",new Vector2(.03f,.30f),new Vector2(.48f,.40f),()=>RunUI(async()=>{ await ExportReplayAsync(); })));
            actions.Add(TableUI.Button(body,"Kaydı tekrar oynat",new Vector2(.51f,.30f),new Vector2(.97f,.40f),()=>RunUI(()=>ReplayAsync(filename.text))));
            var entry=TableUI.Panel(body,"Replay filename",new Vector2(.03f,.19f),new Vector2(.97f,.27f),new Color(.12f,.19f,.17f));
            filename=entry.gameObject.AddComponent<InputField>();var text=TableUI.Label(entry,"Filename",new Vector2(.02f,0),new Vector2(.98f,1));text.alignment=TextAnchor.MiddleLeft;
            filename.textComponent=text;filename.characterLimit=100;
            status=TableUI.Label(body,"Diagnostic status",new Vector2(.03f,.01f),new Vector2(.97f,.17f),22);
            Message("Senaryo seçin. Tek adım aktif koltuğun bir turunu oynar. Kayıtlar yalnız yerel Diagnostics klasöründedir.");
            card.SetActive(false);
        }
        private void Toggle()
        {
            if(working) return;
            if(!card.activeSelf) game.SetBotsEnabled(false);
            card.SetActive(!card.activeSelf);
        }
        private void Update()
        { foreach(var button in actions) button.interactable=!working && game!=null && game.CanControl && !game.BotThinking; }
        public void LoadScenario(string path)
        {
            if(!game.CanControl || game.BotThinking) { Message("Önce devam edin veya mevcut bot turunun durmasını bekleyin.");return; }
            var loaded=loader.Load(path);
            if(!loaded.Success) { Message("Senaryo doğrulanamadı; mevcut el korundu.");return; }
            game.InstallDiagnosticRound(loaded.Value);Message("Senaryo yüklendi. Bot adımı veya normal masa kontrolleri kullanılabilir.");
            Debug.Log("T10_SCENARIO_LOADED file="+path+" hash="+ReplayCodec.Hash(loaded.Value));
        }
        public async Task StepBotAsync()
        {
            await game.StepDiagnosticBotAsync();
            if(!destroyed) { Message("Tek tur tamamlandı. Sonuç ve masa güncellendi.");Debug.Log("T10_STEP version="+game.Session.GetView(0).Version+" hash="+game.Session.CaptureReplay().ExpectedFinalHash); }
        }
        public async Task<string> ExportReplayAsync()
        {
            var record=game.Session.CaptureReplay();var name=codec.NewFileName();
            await Task.Run(()=>codec.Write(record,name),lifetime.Token);
            if(!destroyed) { filename.text=name;Message("Kayıt yazıldı: "+name);Debug.Log("T10_REPLAY_WRITTEN file="+name+" commands="+record.Commands.Count); }
            return name;
        }
        public async Task ReplayAsync(string path)
        {
            var observed=game.Session.GetView(0);game.Session.GetView(0,out var epoch);
            using(var work=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token,epoch))
            {
                var record=await Task.Run(()=>codec.Read(path,work.Token),work.Token);
                if(destroyed || work.IsCancellationRequested) return;
                var current=game.Session.GetView(0);
                if(current.RoundId!=observed.RoundId || current.Version!=observed.Version) return;
                if(!record.Success) { Message("Kayıt doğrulanamadı; mevcut el korundu.");return; }
                var restored=await game.InstallDiagnosticReplayAsync(record.Value,work.Token);
                if(destroyed) return;
                if(!restored.Success) { Message("Tekrar sonucu doğrulanamadı; mevcut el korundu.");return; }
                Message("Tekrar doğrulandı; son durum ve puanlar gösteriliyor.");
                Debug.Log("T10_REPLAY_RESTORED hash="+record.Value.ExpectedFinalHash+" commands="+record.Value.Commands.Count);
            }
        }
        private async void RunUI(Func<Task> action)
        {
            if(working || game.BotThinking || !game.CanControl) return;
            working=true;
            try { await action(); }
            catch(OperationCanceledException) { if(!destroyed) Message("İşlem iptal edildi; mevcut el korundu."); }
            catch(Exception exception) { if(!destroyed) { Debug.LogException(exception);Message("Test işlemi tamamlanamadı. Dosya adı ve klasör erişimini kontrol edin."); } }
            finally { working=false; }
        }
        private void Message(string text) { if(status!=null) status.text=text; }
        private void OnDestroy() { destroyed=true;lifetime.Cancel();lifetime.Dispose(); }
#endif
    }
}
