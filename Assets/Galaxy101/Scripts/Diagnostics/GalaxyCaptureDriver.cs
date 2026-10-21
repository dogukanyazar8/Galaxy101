#if UNITY_EDITOR || OKEY_DIAGNOSTICS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Galaxy101
{
    /// <summary>Explicit opt-in acceptance capture of the application's own UI render target.</summary>
    public sealed class GalaxyCaptureDriver : MonoBehaviour
    {
        private static readonly string[] Files = {
            "01_ana_ekran_saturn.png","02_tema_mars.png","03_tema_neptun.png","04_tema_jupiter.png","05_tema_venus.png","06_mod_secimi_esli.png",
            "10_ayarlar_oyun.png","11_ayarlar_ses.png","12_ayarlar_goruntu.png","13_ayarlar_dil.png","14_ayarlar_hesap.png",
            "15_profili_duzenle.png","16_profili_duzenle_altin_cerceve.png","17_profili_duzenle_gecersiz_ad.png","18_ayarlar_hesap_kaydedildi.png",
            "19_cikis_onay.png","20_cikis_yapildi.png","21_hesap_sil_onay.png","22_hesap_sil_SIL_yazildi.png",
            "30_magaza_cip.png","31_magaza_elmas.png","32_magaza_gorunum.png",
            "40_arkadaslar_cevrimici.png","41_arkadaslar_tumu.png","42_arkadaslar_arama_ay.png","43_arkadaslar_istekler.png",
            "44_arkadas_ekle_bos.png","45_arkadas_ekle_bulundu.png","46_arkadas_ekle_istek_gonderildi.png","47_arkadas_ekle_bulunamadi.png",
            "50_liderlik_haftalik.png","51_liderlik_sezon.png","52_liderlik_arkadaslar.png","60_gunluk_odul.png","61_gunluk_odul_toplandi.png",
            "70_kurallar_1_taslar.png","71_kurallar_2_dagitim.png","72_kurallar_3_el_acmak.png","73_kurallar_4_tas_islemek.png","74_kurallar_5_bitirmek.png","75_kurallar_6_puanlama.png",
            "80_profilim_genel.png","81_profilim_rozetler.png","82_profilim_mac_gecmisi.png","83_profil_paylas.png","84_profil_paylas_kopyalandi.png",
            "90_EN_settings_language.png","91_EN_settings_game.png","92_EN_main.png","93_EN_shop.png","94_EN_friends.png","95_EN_daily_reward.png","96_EN_rules.png","97_EN_my_profile.png"
        };
        public static string[] ReferenceFiles => (string[])Files.Clone();
        private string outputDirectory,persistencePath;
        private MainMenuController controller;
        private UIDocument document;
        private GalaxyBackground background;
        private PanelSettings originalPanel,capturePanel;
        private RenderTexture framebuffer;
        private Camera presentationCamera;
        private MenuGammaPresenter presenter;
        private bool presenterWasEnabled;
        private RenderTexture originalCameraTarget;
        private JObject manifest;
        private readonly JArray captures=new JArray();
        private bool started,finished,presentationCapture;
        private double startedAt;

        public static string ResolveOutput(string[] arguments)
        {
            int index=Array.FindIndex(arguments??Array.Empty<string>(),s=>s=="-galaxyCaptureOutput");
            if(index<0) return null;
            if(index+1>=arguments.Length||string.IsNullOrWhiteSpace(arguments[index+1])||!Path.IsPathFullyQualified(arguments[index+1]))
                throw new ArgumentException("-galaxyCaptureOutput requires an absolute output directory.");
            return Path.GetFullPath(arguments[index+1]);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            string output;
            try {output=ResolveOutput(Environment.GetCommandLineArgs());}
            catch(ArgumentException error) {Debug.LogError(error.Message);UnityEngine.Application.Quit(2);return;}
            if(output==null) return;
            var owner=new GameObject("Galaxy101 owned framebuffer capture");DontDestroyOnLoad(owner);
            var driver=owner.AddComponent<GalaxyCaptureDriver>();driver.outputDirectory=output;
            driver.presentationCapture=Array.IndexOf(Environment.GetCommandLineArgs(),"-galaxyCapturePresentation")>=0;
            driver.persistencePath=Path.Combine(output,"capture-state",Guid.NewGuid().ToString("N"),"menu.json");
            driver.startedAt=Time.realtimeSinceStartupAsDouble;
            driver.manifest=new JObject { ["status"]="initializing",["version"]=UnityEngine.Application.version,["referenceResolution"]="1280x720",["captureCountExpected"]=56,
                ["source"]="UIDocument PanelSettings.targetTexture and GalaxyBackground.RenderOnce; no desktop capture",["persistence"]="isolated capture-state directory",["animationTime"]=0,
                ["timerPolicy"]="Visible countdown labels normalized to 12:00:00 / 12h 0m for reproducibility",["clipboardPolicy"]="Copied-feedback state is rendered without accessing the system clipboard",["captures"]=driver.captures };
            if(driver.presentationCapture)driver.manifest["source"]="Owned URP camera framebuffer after native menu gamma presentation; no desktop capture";
            driver.manifest["colorPolicy"]="CSS gamma UI in UNorm, decoded once for the linear camera";
            SceneManager.sceneLoaded+=driver.OnSceneLoaded;
        }
        private void OnSceneLoaded(Scene scene,LoadSceneMode mode)
        {
            if(started||finished) return;
            var menu=FindAnyObjectByType<MainMenuController>(FindObjectsInactive.Include);
            if(menu==null) return;
            started=true;
            try
            {
                Directory.CreateDirectory(outputDirectory);
                if(menu.View!=null) throw new InvalidOperationException("Capture must initialize the menu before its normal Start method to isolate persistence.");
                controller=menu;controller.Initialize(persistencePath);controller.View.SetFocused(false);
                document=controller.GetComponent<UIDocument>();background=controller.GetComponentInChildren<GalaxyBackground>();background.SetFocused(false);
                originalPanel=document.panelSettings;
                if(!presentationCapture)
                {
                    capturePanel=Instantiate(originalPanel);capturePanel.name="Galaxy101 isolated capture panel";
                    capturePanel.scaleMode=PanelScaleMode.ScaleWithScreenSize;capturePanel.referenceResolution=new Vector2Int(1280,720);capturePanel.screenMatchMode=PanelScreenMatchMode.MatchWidthOrHeight;capturePanel.match=.5f;
                    document.panelSettings=capturePanel;
                }
                else
                {
                    presenter=menu.GetComponent<MenuGammaPresenter>()??throw new InvalidOperationException("The menu gamma presenter is missing.");
                    presenterWasEnabled=presenter.enabled;presenter.enabled=false;
                    presentationCamera=menu.GetComponentInChildren<Camera>()??throw new InvalidOperationException("The menu presentation camera is missing.");
                    presentationCamera.enabled=true;
                    originalCameraTarget=presentationCamera.targetTexture;
                }
                Resize(1280,720);manifest["status"]="capturing";
                StartCoroutine(RunSafely(CaptureAll()));
            }
            catch(Exception error) {Fail(error);}
        }
        private void Update()
        {
            if(!finished&&Time.realtimeSinceStartupAsDouble-startedAt>240) Fail(new TimeoutException("Owned framebuffer capture did not finish within 240 seconds."));
        }
        private IEnumerator RunSafely(IEnumerator root)
        {
            var stack=new Stack<IEnumerator>();stack.Push(root);
            while(stack.Count>0)
            {
                object current;
                try
                {
                    if(!stack.Peek().MoveNext()) {stack.Pop();continue;}
                    current=stack.Peek().Current;
                    if(current is IEnumerator nested) {stack.Push(nested);continue;}
                }
                catch(Exception error) {Fail(error);yield break;}
                yield return current;
            }
        }
        private IEnumerator CaptureAll()
        {
            yield return Settle();
            foreach(string file in Files)
            {
                yield return PrepareState(file);
                yield return Capture(file,1280,720,true);
            }
            yield return PrepareState(Files[0]);
            yield return Capture("main_1920x1080.png",1920,1080,false);
            yield return Capture("main_2560x1440.png",2560,1440,false);
            manifest["status"]="completed";WriteManifest();finished=true;
            Debug.Log("GALAXY101_CAPTURE_COMPLETE count="+captures.Count+" output="+outputDirectory);
            UnityEngine.Application.Quit(0);
        }
        private IEnumerator PrepareState(string file)
        {
            var view=controller.View;var model=controller.Model;
            var query=view.Root.Q<TextField>("friend-query");if(query!=null) query.value="";
            while(view.Modals.Dialog!=null||view.Modals.Panel!=null) view.CloseTop();
            model.Settings=model.DefaultSettings();model.Profile=new JObject { ["name"]="Doğukan Y.",["color"]="#FF8A5B",["status"]="",["frame"]=0 };
            model.Chips=125400;model.Gems=340;model.SelectedMode=0;model.Planet="Satürn";model.DayClaimed=false;
            model.Invited.Clear();model.RequestsHandled.Clear();model.SentRequests.Clear();model.QuestsClaimed.Clear();model.OwnedSkins.Clear();
            var quests=(JArray)model.Content["dailyQuests"];for(int i=0;i<quests.Count;i++) if((bool?)quests[i]["pre"]==true) model.QuestsClaimed.Add(i);
            int number=int.Parse(file.Substring(0,2),System.Globalization.CultureInfo.InvariantCulture);
            if(number>=18) model.Profile["frame"]=1;
            if(number>=90) model.Settings["lang"]="en";
            switch(number)
            {
                case 2:model.Planet="Mars";break;case 3:model.Planet="Neptün";break;case 4:model.Planet="Jüpiter";break;case 5:model.Planet="Venüs";break;case 6:model.SelectedMode=1;break;
            }
            model.NotifyChanged();yield return Settle();
            if(number>=10&&number<=14) view.OpenPanel("settings",number-10);
            else if(number>=15&&number<=22)
            {
                view.OpenPanel("settings",4);yield return Settle();
                if(number<=17) view.OpenDialog("profile");
                else if(number==19||number==20) view.OpenDialog("logout");
                else if(number==21||number==22) view.OpenDialog("delete");
            }
            else if(number>=30&&number<=32) view.OpenPanel("shop",number-30);
            else if(number>=40&&number<=47)
            {
                view.OpenPanel("friends",number==40?0:number<=42?1:2);
                if(number>=44) {yield return Settle();view.OpenDialog("add");}
            }
            else if(number>=50&&number<=52) view.OpenPanel("leader",number-50);
            else if(number==60||number==61) view.OpenPanel("daily");
            else if(number>=70&&number<=75) view.OpenPanel("rules",number-70);
            else if(number>=80&&number<=84)
            {
                view.OpenPanel("me",Math.Min(2,number-80));
                if(number>=83) {view.SetTab("me",0);yield return Settle();view.OpenDialog("share");}
            }
            else if(number>=90)
            {
                switch(number) {case 90:view.OpenPanel("settings",3);break;case 91:view.OpenPanel("settings",0);break;case 93:view.OpenPanel("shop",2);break;case 94:view.OpenPanel("friends",2);break;case 95:view.OpenPanel("daily");break;case 96:view.OpenPanel("rules",5);break;case 97:view.OpenPanel("me",2);break;}
            }
            yield return Settle();
            if(number==16) yield return Click("draft-frame-1");
            if(number==17){yield return Click("draft-frame-1");Field("profile-name").value="Do";}
            if(number==20) yield return Click("confirm-account-action");
            if(number==22) Field("delete-confirmation").value="SİL";
            if(number==42) Field("friend-query").value="ay";
            if(number==45||number==46||number==47)
            {
                Field("add-friend-query").value=number==47?"XX-0000":"MK-3321";yield return Click("search-add-friend");
                if(number==46) {yield return Settle();yield return Click("send-friend-request");}
            }
            if(number==61||number==95)
            {
                view.Services.Rewards.ClaimDay();
                var ready=view.Services.Rewards.GetQuests();for(int i=0;i<ready.Count;i++) if((bool)ready[i]["complete"]&&!(bool)ready[i]["claimed"]) {view.Services.Rewards.ClaimQuest(i);break;}
            }
            yield return Settle();
            if(number==84)
            {
                var button=Button("copy-profile-link");button.text="✓ "+view.L("Kopyalandı","Copied");view.Decorate(button,"#3CCB6E",14,4,"#22914A");button.style.color=Color.white;
            }
            view.SetFocused(false);background.SetFocused(false);background.Configure(model.Planet,false,"Yüksek");
        }
        private TextField Field(string name) => controller.View.Root.Q<TextField>(name)??throw new InvalidOperationException("Capture state lacks text field: "+name);
        private Button Button(string name) => controller.View.Root.Q<Button>(name)??throw new InvalidOperationException("Capture state lacks button: "+name);
        private IEnumerator Click(string name)
        {
            var button=Button(name);if(!button.enabledInHierarchy) throw new InvalidOperationException("Capture action is disabled: "+name);
            button.Focus();yield return null;
            using(var evt=NavigationSubmitEvent.GetPooled()) {evt.target=button;button.SendEvent(evt);}
        }
        private static IEnumerator Settle() {double until=Time.realtimeSinceStartupAsDouble+.32;while(Time.realtimeSinceStartupAsDouble<until)yield return null;}
        private void Resize(int width,int height)
        {
            if(framebuffer!=null&&framebuffer.width==width&&framebuffer.height==height) return;
            if(framebuffer!=null) {if(presentationCapture)presentationCamera.targetTexture=null;else capturePanel.targetTexture=null;framebuffer.Release();Destroy(framebuffer);}
            if(presentationCapture)
            {
                // The headless probe controls its own viewport, without resizing an OS window.
                // The same native UI and presentation camera remain in the rendering path.
                var ui=presenter.Output;
                if(ui.width!=width||ui.height!=height){ui.Release();ui.width=width;ui.height=height;if(!ui.Create())throw new InvalidOperationException("Could not resize the owned presentation UI target.");document.panelSettings.targetTexture=ui;}
            }
            framebuffer=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,presentationCapture?RenderTextureReadWrite.sRGB:RenderTextureReadWrite.Linear) {name="Galaxy101 acceptance framebuffer",filterMode=FilterMode.Bilinear};
            if(!framebuffer.Create()) throw new InvalidOperationException("Could not create owned acceptance framebuffer.");
            if(presentationCapture)presentationCamera.targetTexture=framebuffer;else capturePanel.targetTexture=framebuffer;
        }
        private IEnumerator Capture(string file,int width,int height,bool reference)
        {
            Resize(width,height);background.RenderOnce(width,height);controller.View.Root.MarkDirtyRepaint();
            yield return Settle();
            NormalizeTimers();if(controller.View.Root.panel.focusController.focusedElement is Button)controller.View.Root.Focus();controller.View.Root.MarkDirtyRepaint();
            // Two completed player-loop frames let UI Toolkit draw its offscreen panel.
            // Avoid WaitForEndOfFrame, which is unavailable in some batch/editor loops.
            yield return null;yield return null;
            if(presentationCapture)
            {
                if(captures.Count==0) LogPresentationState();
                // A headless player does not drive the normal screen render loop. Explicitly
                // submit this same camera through URP's supported offscreen rendering API.
                var request=new UnityEngine.Rendering.RenderPipeline.StandardRequest {destination=framebuffer};
                if(!UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(presentationCamera,request))
                    throw new InvalidOperationException("The configured pipeline cannot render the menu presentation capture.");
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(presentationCamera,request);
                if(captures.Count==0) Debug.Log("GALAXY101_PRESENTATION_RENDERED pipeline="+UnityEngine.Rendering.RenderPipelineManager.currentPipeline?.GetType().FullName);
            }
            string path=Path.Combine(outputDirectory,file);
            var previous=RenderTexture.active;Texture2D pixels=null;
            try
            {
                RenderTexture.active=framebuffer;pixels=new Texture2D(width,height,TextureFormat.RGBA32,false);pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();
                float minimum=3,maximum=0;int visible=0;
                for(int y=0;y<9;y++) for(int x=0;x<16;x++)
                {
                    var color=pixels.GetPixel(Mathf.RoundToInt((x+.5f)*width/16),Mathf.RoundToInt((y+.5f)*height/9));
                    float brightness=color.r+color.g+color.b;minimum=Mathf.Min(minimum,brightness);maximum=Mathf.Max(maximum,brightness);if(color.a>.1f) visible++;
                }
                bool nonblank=visible>=100&&maximum-minimum>.15f;
                if(!nonblank) throw new InvalidOperationException("Owned UI framebuffer is blank or uniform for "+file+"; visible="+visible+", contrast="+(maximum-minimum));
                float exposedMinimum=3,exposedMaximum=0,exposedBrightness=0;
                bool hasModal=controller.View.Modals.Panel!=null||controller.View.Modals.Dialog!=null;
                if(hasModal)
                {
                    // The colored foreground card must not hide a missing, uniform gray
                    // underlay from the whole-frame contrast check.
                    foreach(float x in new[]{.015f,.055f,.945f,.985f})foreach(float y in new[]{.015f,.15f,.4f,.7f,.985f})
                    {
                        var sample=pixels.GetPixel(Mathf.RoundToInt(x*(width-1)),Mathf.RoundToInt(y*(height-1)));
                        float sum=sample.r+sample.g+sample.b;exposedMinimum=Mathf.Min(exposedMinimum,sum);exposedMaximum=Mathf.Max(exposedMaximum,sum);exposedBrightness+=sum/60;
                    }
                    if(exposedBrightness>.4f||exposedMaximum-exposedMinimum<.025f)throw new InvalidOperationException("The exposed main scene is gray, blank or missing behind "+file+"; brightness="+exposedBrightness+", contrast="+(exposedMaximum-exposedMinimum));
                }
                File.WriteAllBytes(path,pixels.EncodeToPNG());
                var model=controller.Model;var view=controller.View;
                captures.Add(new JObject { ["file"]=file,["reference"]=reference,["width"]=width,["height"]=height,["planet"]=model.Planet,["language"]=view.Loc.Language,["mode"]=model.SelectedMode,
                    ["panel"]=view.Modals.Panel,["tab"]=view.Modals.Panel==null?0:view.Tab(view.Modals.Panel),["dialog"]=view.Modals.Dialog,["chips"]=model.Chips,["gems"]=model.Gems,["dayClaimed"]=model.DayClaimed,["backgroundSeconds"]=background.TimeState.Seconds,
                    ["profileFrame"]=(int)model.Profile["frame"],["nonblank"]=nonblank,["sampleContrast"]=maximum-minimum,["visibleSamples"]=visible,
                    ["modalBackgroundChecked"]=hasModal,["exposedBackgroundBrightness"]=hasModal?exposedBrightness:0,["exposedBackgroundContrast"]=hasModal?exposedMaximum-exposedMinimum:0 });
                WriteManifest();Debug.Log("GALAXY101_CAPTURE_FRAME "+file+" "+width+"x"+height);
            }
            finally {RenderTexture.active=previous;if(pixels!=null) Destroy(pixels);}
        }
        private void LogPresentationState()
        {
            var camera=presentationCamera;
            var cameraData=camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            var pipeline=UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
            Debug.Log("GALAXY101_PRESENTATION_STATE batch="+UnityEngine.Application.isBatchMode+" cameras="+Camera.allCamerasCount+
                " registered="+string.Join(",",Camera.allCameras.Select(c=>c.name+":"+c.enabled+":"+c.gameObject.activeInHierarchy))+
                " enabled="+camera.enabled+" active="+camera.gameObject.activeInHierarchy+" scene="+camera.gameObject.scene.IsValid()+":"+camera.gameObject.scene.name+
                " flags="+camera.gameObject.hideFlags+" type="+cameraData.renderType+" rect="+camera.pixelRect+" planes="+camera.nearClipPlane+":"+camera.farClipPlane+
                " target="+camera.targetTexture.IsCreated()+" culling="+camera.TryGetCullingParameters(out _)+
                " quality="+QualitySettings.names[QualitySettings.GetQualityLevel()]+" qualityPipeline="+QualitySettings.renderPipeline+
                " graphicsPipeline="+pipeline+" pipeline="+UnityEngine.Rendering.RenderPipelineManager.currentPipeline?.GetType().FullName);
            var previous=RenderTexture.active;Texture2D source=null;
            try
            {
                var ui=presenter.Output;RenderTexture.active=ui;source=new Texture2D(ui.width,ui.height,TextureFormat.RGBA32,false);
                source.ReadPixels(new Rect(0,0,ui.width,ui.height),0,0);source.Apply();
                File.WriteAllBytes(Path.Combine(outputDirectory,"_presentation-source-first.png"),source.EncodeToPNG());
            }
            finally {RenderTexture.active=previous;if(source!=null)Destroy(source);}
        }
        private void NormalizeTimers()
        {
            controller.View.Root.Query<Label>().ForEach(label=>
            {
                if(Regex.IsMatch(label.text??"",@"^\d{2}:\d{2}:\d{2}$")) label.text="12:00:00";
                else if((label.text??"").StartsWith("Yenilenmeye ",StringComparison.Ordinal)) label.text="Yenilenmeye 12 sa 0 dk";
                else if((label.text??"").StartsWith("Resets in ",StringComparison.Ordinal)) label.text="Resets in 12h 0m";
            });
        }
        private void WriteManifest() => File.WriteAllText(Path.Combine(outputDirectory,"manifest.json"),manifest.ToString(Formatting.Indented));
        private void Fail(Exception error)
        {
            if(finished) return;finished=true;Debug.LogException(error);
            try {Directory.CreateDirectory(outputDirectory);manifest["status"]="failed";manifest["error"]=error.ToString();WriteManifest();}
            catch(Exception failure) when(failure is IOException||failure is UnauthorizedAccessException) {Debug.LogError("Capture manifest failed: "+failure.Message);}
            UnityEngine.Application.Quit(3);
        }
        private void OnDestroy()
        {
            SceneManager.sceneLoaded-=OnSceneLoaded;
            if(presentationCamera!=null&&presentationCamera.targetTexture==framebuffer)presentationCamera.targetTexture=originalCameraTarget;
            if(presenter!=null)presenter.enabled=presenterWasEnabled;
            if(capturePanel!=null&&document!=null&&document.panelSettings==capturePanel&&originalPanel!=null) document.panelSettings=originalPanel;
            if(capturePanel!=null) {capturePanel.targetTexture=null;Destroy(capturePanel);}
            if(framebuffer!=null) {framebuffer.Release();Destroy(framebuffer);}
        }
    }
}
#endif
