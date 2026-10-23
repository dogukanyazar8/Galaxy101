using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    public sealed partial class MenuView
    {
        private static readonly string[] PlanetIds={"saturn","mars","neptune","jupiter","venus"};
        private static readonly string[] PlanetNames={"Satürn","Mars","Neptün","Jüpiter","Venüs"};
        private IVisualElementScheduledItem animationJob;
        public void BuildMain()
        {
            Root.Clear();var template=Template("MainMenu");template.style.flexGrow=1;// the 1280×720 canvas sits in the middle of any screen shape (bars around it)
            template.style.alignSelf=Align.Stretch;template.style.alignItems=Align.Center;template.style.justifyContent=Justify.Center;Root.Add(template);
            var canvas=Root.Q<VisualElement>("galaxy-canvas")??throw new InvalidOperationException("MainMenu UXML lacks galaxy-canvas.");
            BackgroundTarget=canvas.Q<VisualElement>("background");mainLayer=canvas.Q<VisualElement>("main-layer");panelLayer=canvas.Q<VisualElement>("modal-layer");dialogLayer=canvas.Q<VisualElement>("dialog-layer");toastLayer=canvas.Q<VisualElement>("toast-layer");
            sceneUnderlay=canvas.Q<VisualElement>("scene-underlay");dialogUnderlay=canvas.Q<VisualElement>("dialog-underlay");
            mainSignature=null;sections.Clear();mountedPanel=mountedDialog=null;panelCard=dialogCard=null;
            animationJob?.Pause();float last=Time.realtimeSinceStartup;
            animationJob=Root.schedule.Execute(()=>
            {
                float now=Time.realtimeSinceStartup;float delta=Mathf.Clamp(now-last,0,.05f);last=now;if(!focused)return;animationTime+=delta;
                foreach(var motion in motions)motion(animationTime);
                foreach(var section in sections.Values)foreach(var motion in section.Motions)motion(animationTime);
                foreach(var motion in panelMotions)motion(animationTime);
                foreach(var motion in dialogMotions)motion(animationTime);
            }).Every(16);
            Rebuild();
        }
        private sealed class MainSection{public VisualElement Host;public string Signature;public readonly List<Action<float>> Motions=new List<Action<float>>();}
        private readonly Dictionary<string,MainSection> sections=new Dictionary<string,MainSection>();
        private VisualElement sectionHost;
        private void AddMain(VisualElement element)=>(sectionHost??mainLayer).Add(element);
        /// <summary>
        /// (Re)builds one part of the main layer, in its place, only when its signature changes.
        /// </summary>
        private void Section(string key,string signature,Action build)
        {
            if(!sections.TryGetValue(key,out var section))sections.Add(key,section=new MainSection());
            if(section.Signature==signature&&section.Host!=null&&section.Host.parent==mainLayer)return;
            var old=section.Host;var host=new VisualElement{name="main-"+key,pickingMode=PickingMode.Ignore};host.AddToClassList("fill");
            if(old!=null&&old.parent==mainLayer)mainLayer.Insert(mainLayer.IndexOf(old),host);else mainLayer.Add(host);
            section.Motions.Clear();var previousTarget=buildingMotions;buildingMotions=section.Motions;sectionHost=host;
            try{build();}finally{sectionHost=null;buildingMotions=previousTarget;}
            section.Host=host;section.Signature=signature;
            old?.RemoveFromHierarchy();
        }
        private void Rebuild()
        {
            if(disposed||rebuilding||mainLayer==null)return;rebuilding=true;
            bool restoringAfterClose=focusAfterClose!=null;
            var oldFocus=Root.panel?.focusController?.focusedElement as VisualElement;string identity=focusAfterClose??FocusIdentity(oldFocus);focusAfterClose=null;
            TextField oldField=oldFocus as TextField??oldFocus?.GetFirstAncestorOfType<TextField>();int cursor=oldField?.cursorIndex??0,selection=oldField?.selectIndex??0;string typedValue=oldField?.value;
            try
            {
                int planetIndex=Array.IndexOf(PlanetNames,Model.Planet);if(planetIndex<0)planetIndex=0;
                if(lastTheme!=null)Root.RemoveFromClassList("theme-"+lastTheme);lastTheme=PlanetIds[planetIndex];Root.AddToClassList("theme-"+lastTheme);
                // Each part of the menu is rebuilt only when what it shows changed: picking a mode must not recreate the
                // top bar, logo or footer (their filtered shadows would blink for a frame).
                string common=Model.Planet+"|"+Loc.Language;
                Section("top",common+"|"+Model.Chips+"|"+Model.Gems+"|"+Model.Profile.ToString(Newtonsoft.Json.Formatting.None),BuildTopBar);
                Section("logo",common,()=>BuildLogo(planetIndex));
                Section("play",common,BuildPlay);
                Section("modes",common,BuildModes);
                ApplyModes();
                Section("footer",common+"|"+Model.DayClaimed+"|"+Services.Friends.GetFriends(true).Count,BuildFooter);
                mainSignature=common;
                mainLayer.EnableInClassList("main-blocked",Modals.Panel!=null||Modals.Dialog!=null);mainLayer.SetEnabled(Modals.Panel==null&&Modals.Dialog==null);
                mainLayer.pickingMode=Modals.Panel==null&&Modals.Dialog==null?PickingMode.Position:PickingMode.Ignore;
                panelLayer.pickingMode=Modals.Panel!=null?PickingMode.Position:PickingMode.Ignore;
                dialogLayer.pickingMode=Modals.Dialog!=null?PickingMode.Position:PickingMode.Ignore;
                if(Modals.Panel!=null)
                {
                    if(mountedPanel!=Modals.Panel)panelAnimationOrigin=animationTime;
                    panelMotions.Clear();buildingMotions=panelMotions;BuildPanel();
                }
                else{panelLayer.Clear();panelCard=null;mountedPanel=null;panelMotions.Clear();}
                if(Modals.Dialog!=null)
                {
                    if(mountedDialog!=Modals.Dialog)dialogAnimationOrigin=animationTime;
                    dialogMotions.Clear();buildingMotions=dialogMotions;BuildDialog();
                }
                else{dialogLayer.Clear();dialogCard=null;mountedDialog=null;dialogMotions.Clear();}
                buildingMotions=null;
                GalaxyFilters.Blur(sceneUnderlay,Modals.Panel!=null?6:0);
                GalaxyFilters.Blur(dialogUnderlay,Modals.Dialog!=null?4:0);
                UpdateBackground();
                if(identity!=null)
                {
                    void RestoreFocus()
                    {
                        var target=ResolveFocus(identity);if(target==null||!target.enabledInHierarchy)return;
                        if(Modals.Dialog!=null&&!dialogLayer.Contains(target)||Modals.Dialog==null&&Modals.Panel!=null&&!panelLayer.Contains(target))return;
                        target.Focus();if(!restoringAfterClose&&oldField!=null&&target is TextField field){field.value=typedValue;field.schedule.Execute(()=>field.SelectRange(cursor,selection)).ExecuteLater(1);}
                    }
                    if(restoringAfterClose)RestoreFocus();else Root.schedule.Execute(RestoreFocus).ExecuteLater(2);
                }
            }
            finally{buildingMotions=null;rebuilding=false;}
        }
        private void BuildTopBar()
        {
            var top=Box("top-bar");AddMain(top);
            var avatar=Avatar(62,level:true);GalaxyFilters.HoverBrightness(avatar,1.1f);avatar.name="profile-open";avatar.AddToClassList("avatar-button");avatar.focusable=true;avatar.RegisterCallback<ClickEvent>(_=>OpenPanel("me"));avatar.RegisterCallback<NavigationSubmitEvent>(evt=>{OpenPanel("me");evt.StopImmediatePropagation();});top.Add(avatar);
            var name=Column(Text((string)Model.Profile["name"],18,true),Row(RankDiamond(18),Text(L("Altın II","Gold II"),14,true,"#FFC94A")));name.AddToClassList("top-name");top.Add(name);
            var spacer=Box();spacer.style.flexGrow=1;top.Add(spacer);
            top.Add(Currency(false));top.Add(Currency(true));
            var settingsButton=Button("",()=>OpenPanel("settings"),"secondary settings-button");settingsButton.name="settings-open";settingsButton.tooltip=L("Ayarlar","Settings");settingsButton.Add(Icon("settings",24));top.Add(settingsButton);
        }
        private VisualElement Currency(bool gems)
        {
            var pill=Box("currency-pill");Decorate(pill,"var(--dp)",19);var icon=Box(gems?"gem currency-icon":"coin currency-icon");
            Decorate(icon,gems?"linear-gradient(135deg,#7FE3FF,#3D7BFF)":"radial-gradient(circle at 35% 30%,#FFE9A8,#F5A623 60%,#B86E0F)",gems?4:99);
            if(gems)icon.style.rotate=new Rotate(45);
            GalaxyFilters.Shadow(icon,0,0,5,gems?new Color(.498f,.89f,1,.5f):new Color(1,.788f,.29f,.45f));icon.style.overflow=Overflow.Hidden;
            if(gems)Shine(icon,3.2f,1.6f,true,8,6);
            else
            {
                var shineWindow=Box();shineWindow.pickingMode=PickingMode.Ignore;Place(shineWindow,2,2,24,24);shineWindow.style.borderTopLeftRadius=shineWindow.style.borderTopRightRadius=shineWindow.style.borderBottomLeftRadius=shineWindow.style.borderBottomRightRadius=12;shineWindow.style.overflow=Overflow.Hidden;
                Shine(shineWindow,3.2f,0,true,9,4);icon.Add(shineWindow);icon.Add(new CssDashedCircle(28,2,C("#FFF3C8")));
            }
            pill.Add(icon);
            var amount=Text(N(gems?Model.Gems:Model.Chips),17,true);amount.name=gems?"gems-balance":"chips-balance";amount.style.minWidth=gems?44:84;pill.Add(amount);
            var plus=Button("+",()=>OpenPanel("shop",gems?1:0),"success currency-plus");plus.name=gems?"gems-buy":"chips-buy";plus.tooltip=L(gems?"Elmas satın al":"Çip satın al",gems?"Buy gems":"Buy chips");plus.style.fontSize=20;plus.style.unityTextAlign=TextAnchor.MiddleCenter;Decorate(plus,"#3CCB6E",14,3,"#22914A");pill.Add(plus);return pill;
        }
        public VisualElement RankDiamond(int size)
        {
            var diamond=Box("rank-diamond");diamond.style.width=diamond.style.height=size;diamond.style.rotate=new Rotate(45);Decorate(diamond,"linear-gradient(135deg,#FFE08A,#F5A623)",Mathf.Max(3,size/5));
            GalaxyFilters.Shadow(diamond,0,0,5,new Color(1,.788f,.29f,.5f));var numeral=Text("II",Mathf.RoundToInt(size*.5f),true,"#3A1F00");numeral.style.rotate=new Rotate(-45);diamond.Add(numeral);return diamond;
        }
        private void BuildLogo(int planet)
        {
            var logo=Resources.Load<Texture2D>("Galaxy101/Artwork/logo-"+PlanetIds[planet]);
            if(logo==null)throw new InvalidOperationException("Missing Galaxy logo artwork.");
            var image=new Image{image=logo,pickingMode=PickingMode.Ignore};image.name="galaxy-logo";image.style.position=Position.Absolute;image.style.width=logo.width*.5f;image.style.height=logo.height*.5f;image.style.left=640-logo.width*.25f;image.style.top=44;AddMain(image);
            float[] x={506,596,696},y={191.6f,177.6f,191.6f};int[] w={78,88,78},h={108,122,108};float[] rotation={-10,2,10};string[] tint={"t1","t2","t3"};
            for(int i=0;i<3;i++)
            {
                int index=i;var tile=Box("logo-tile");tile.name="logo-tile-"+i;Place(tile,x[i],y[i],w[i],h[i]);tile.style.rotate=new Rotate(rotation[i]);
                Decorate(tile,"linear-gradient(var(--tf1),var(--tf2))",i==1?13:12,i==1?6:5,"var(--te2)");tile.style.borderBottomWidth=i==1?8:7;tile.style.borderBottomColor=C("var(--te)");
                var number=Text(i==1?"0":"1",i==1?74:64,true,"var(--"+tint[i]+")");number.style.height=i==1?74:64;number.style.unityTextAlign=TextAnchor.MiddleCenter;tile.Add(number);
                var dot=Box();dot.style.width=dot.style.height=i==1?16:14;dot.style.marginTop=3;Decorate(dot,"var(--"+tint[i]+")",99);tile.Add(dot);AddMain(tile);
                // floatA 3.2s ease-in-out, staggered .4s: 0 → -14px → 0.
                Animate(t=>{float f=Mathf.Repeat(t-index*.4f,3.2f)/3.2f;tile.style.translate=new Translate(0,-14*(f<.5f?CssBezier(.42f,0,.58f,1,f*2):1-CssBezier(.42f,0,.58f,1,f*2-1)));});
            }
        }
        private void BuildPlay()
        {
            // The host carries the hover/active transform so the whole CSS box-shadow stack moves with the button.
            var host=Box("play-host");host.name="play-host";Place(host,460,352,360,86);AddMain(host);
            // box-shadow: 0 7px 0 aS (top) · pulsing 0 0 0 0..16px gl ring · 0 14px 26px rgba(0,0,0,.4) (bottom).
            var caster=Box("play-shadow");caster.pickingMode=PickingMode.Ignore;Place(caster,0,7,360,86);Decorate(caster,"var(--aS)",24);GalaxyFilters.Shadow(caster,0,7,13,new Color(0,0,0,.4f));host.Add(caster);
            var pulse=Box("play-pulse");pulse.pickingMode=PickingMode.Ignore;Decorate(pulse,"var(--gl)",24);host.Add(pulse);
            Animate(t=>
            {
                // pulseG 2.2s, default ease per keyframe segment; gl fades to transparent as the spread grows.
                float phase=Mathf.Repeat(t,2.2f)/2.2f,spread=16*(phase<.5f?CssBezier(.25f,.1f,.25f,1,phase*2):1-CssBezier(.25f,.1f,.25f,1,phase*2-1));
                Place(pulse,-spread,-spread,360+spread*2,86+spread*2);pulse.style.borderTopLeftRadius=pulse.style.borderTopRightRadius=pulse.style.borderBottomLeftRadius=pulse.style.borderBottomRightRadius=24+spread;pulse.style.opacity=1-spread/16;
            });
            var depth=Box("play-depth");depth.pickingMode=PickingMode.Ignore;Place(depth,0,7,360,86);Decorate(depth,"var(--aS)",24);host.Add(depth);
            var play=Button("",Play,"primary play-button");play.name="play-now";Place(play,0,0,360,86);Decorate(play,"linear-gradient(var(--a1),var(--a2))",24);play.style.overflow=Overflow.Hidden;
            play.RegisterCallback<PointerDownEvent>(_=>host.AddToClassList("pressed"),TrickleDown.TrickleDown);
            play.RegisterCallback<PointerUpEvent>(_=>host.RemoveFromClassList("pressed"),TrickleDown.TrickleDown);
            play.RegisterCallback<PointerCaptureOutEvent>(_=>host.RemoveFromClassList("pressed"));
            Shine(play,2.8f,0,false);var title=Text(L("HEMEN OYNA","PLAY NOW"),34,true,"var(--aT)");Tracking(title,1,34);title.style.whiteSpace=WhiteSpace.NoWrap;title.style.height=36;play.Add(title);
            var subtitle=Text(PlaySubtitle(),13,false,"var(--aT)");playSubtitle=subtitle;subtitle.name="play-subtitle";subtitle.style.unityFont=Font("Baloo2-800");subtitle.style.marginTop=4;subtitle.style.opacity=.8f;play.Add(subtitle);host.Add(play);
        }
        /// <summary>
        /// v3 modes (spec/20, S06): Klasik (Tekli / Eşli tables in the lobby), Dereceli and Botlara karşı marked YAKINDA.
        /// Cards keep the model's mode indices (0 Klasik, 3 Dereceli, 4 Botlara karşı) so saves and tests stay valid.
        /// </summary>
        public static readonly int[] V3Modes = { 0, 3, 4 };
        public static bool IsSoon(int mode) => mode != 0;
        private void BuildModes()
        {
            var strip=Box("mode-strip");Place(strip,0,474,1280,90);strip.style.flexDirection=FlexDirection.Row;strip.style.justifyContent=Justify.Center;AddMain(strip);
            string[] names={L("Klasik","Classic"),L("Dereceli","Ranked"),L("Botlara karşı","Vs bots")},glyphs={"101","II","•"},colors={"#3D7BFF","#16B6A8","#3CCB6E"},shades={"#2148B0","#0C7F75","#22914A"};
            string[] online={N(14233),N(5630),L("Çevrimdışı","Offline")};
            modeCards.Clear();
            for(int i=0;i<3;i++)
            {
                int index=V3Modes[i];string color=colors[i],shade=shades[i];
                var slot=Box("mode-slot");slot.style.width=208;slot.style.height=78;slot.style.marginRight=i==2?0:12;
                var card=Button("",()=>PickMode(index),"mode-card");
                card.name="mode-"+index;Place(card,0,0,208,78);Decorate(card,color,18,5,shade);
                card.style.flexDirection=FlexDirection.Column;card.style.justifyContent=Justify.Center;card.style.alignItems=Align.FlexStart;card.style.paddingLeft=card.style.paddingRight=14;card.style.overflow=Overflow.Hidden;
                // box-shadow "0 2.5px 0 5px #fff" ring around the selected (and hovered) card, over the page colour gap.
                var ring=Box("mode-selection-ring");ring.pickingMode=PickingMode.Ignore;Place(ring,-8.5f,-12-6,225,95);Decorate(ring,"#fff",26);
                var gap=Box("mode-selection-gap");gap.pickingMode=PickingMode.Ignore;Place(gap,-5.5f,-9-6,219,89);Decorate(gap,"var(--bg3)",23);
                var glyph=Text(glyphs[i],58,true,"rgba(255,255,255,.16)");glyph.pickingMode=PickingMode.Ignore;glyph.style.position=Position.Absolute;glyph.style.right=-4;glyph.style.bottom=-14;card.Add(glyph);
                var gloss=Box();Place(gloss,0,0,208,39);gloss.pickingMode=PickingMode.Ignore;Decorate(gloss,"linear-gradient(rgba(255,255,255,.18),transparent)",18);card.Add(gloss);
                var title=Text(names[i],18,true,"#fff");title.pickingMode=PickingMode.Ignore;card.Add(title);
                var line=Row();line.pickingMode=PickingMode.Ignore;line.style.marginTop=4;var dot=Box();dot.style.width=dot.style.height=6;dot.style.marginRight=5;Decorate(dot,"#8EF0B0",3);GalaxyFilters.Shadow(dot,0,0,3,C("#8EF0B0"));line.Add(dot);
                var count=Text(online[i],11,false,"rgba(255,255,255,.9)");count.style.unityFont=Font("Baloo2-800");line.Add(count);card.Add(line);
                if(IsSoon(index)){var soon=Text(L("YAKINDA","SOON"),9,false,"#fff");soon.name="mode-soon-"+index;soon.style.unityFont=Font("Baloo2-800");Tracking(soon,1.2f,9);soon.style.position=Position.Absolute;soon.style.right=10;soon.style.bottom=9;soon.style.paddingTop=soon.style.paddingBottom=3;soon.style.paddingLeft=soon.style.paddingRight=7;Decorate(soon,"rgba(0,0,0,.32)",6);card.Add(soon);}
                var tick=Box("mode-tick");tick.pickingMode=PickingMode.Ignore;tick.style.position=Position.Absolute;tick.style.right=8;tick.style.top=8;tick.style.width=tick.style.height=22;tick.style.alignItems=Align.Center;tick.style.justifyContent=Justify.Center;Decorate(tick,"#fff",99);tick.Add(Icon("tick",13,"#1B2440"));card.Add(tick);
                Color glow=C(color);glow.a=.6f;
                var state=new ModeCard{Index=index,Card=card,Slot=slot,Ring=ring,Gap=gap,Tick=tick,Glow=glow};modeCards.Add(state);
                card.RegisterCallback<PointerEnterEvent>(_=>{state.Hover=true;ApplyModes();});
                card.RegisterCallback<PointerLeaveEvent>(_=>{state.Hover=false;ApplyModes();});
                card.RegisterCallback<FocusInEvent>(_=>{state.Focused=card.panel?.focusController?.focusedElement==card&&Root.ClassListContains("keyboard-navigation");ApplyModes();});
                card.RegisterCallback<FocusOutEvent>(_=>{state.Focused=false;ApplyModes();});
                slot.Add(card);strip.Add(slot);
            }
            ApplyModes(instant:true);
            Animate(t=>{foreach(var m in modeCards)m.Step(t);});
        }
        /// <summary>One mode card's look, eased like the design's CSS (transform .2s cubic-bezier(.3,1.5,.6,1); filter, box-shadow .2s).</summary>
        private sealed class ModeCard
        {
            public int Index;public Button Card;public VisualElement Slot,Ring,Gap,Tick;public Color Glow;public bool Hover,Focused,On;
            private float lift,scale=1,saturation=.7f,brightness=.78f,ring;
            private float fromLift,fromScale,fromSaturation,fromBrightness,fromRing,toLift=float.NaN,toScale,toSaturation,toBrightness,toRing,began=-9;
            private bool settled;
            public void Target(bool on,float now,bool instant)
            {
                On=on;Card.EnableInClassList("selected",on);
                float l=Hover?-7:on?-6:0,sc=Hover?1.04f:1,sa=Hover||on?1:.7f,br=Hover||on?1:.78f,r=Hover||on||Focused?1:0;
                if(!instant&&l==toLift&&sc==toScale&&sa==toSaturation&&br==toBrightness&&r==toRing)return;
                fromLift=instant?l:lift;fromScale=instant?sc:scale;fromSaturation=instant?sa:saturation;fromBrightness=instant?br:brightness;fromRing=instant?r:ring;
                toLift=l;toScale=sc;toSaturation=sa;toBrightness=br;toRing=r;began=instant?-9:now;settled=false;Step(now);
            }
            public void Step(float now)
            {
                if(settled)return;
                // Never restyle filters inside a hidden menu (the lobby covers it): the compositor asserts on those.
                for(var up=Card.parent;up!=null;up=up.parent)if(up.resolvedStyle.display==DisplayStyle.None){began=-9;return;}
                float f=Mathf.Clamp01((now-began)/.2f),k=CssBezier(.3f,1.5f,.6f,1,f),e=CssBezier(.25f,.1f,.25f,1,f);
                lift=Mathf.LerpUnclamped(fromLift,toLift,k);scale=Mathf.LerpUnclamped(fromScale,toScale,k);
                saturation=Mathf.Lerp(fromSaturation,toSaturation,e);brightness=Mathf.Lerp(fromBrightness,toBrightness,e);ring=Mathf.Lerp(fromRing,toRing,e);
                Card.style.translate=new Translate(0,lift);Card.style.scale=new Scale(Vector2.one*scale);
                var glow=Glow;glow.a*=ring;GalaxyFilters.StableLook(Card,saturation,brightness,0,14,16,glow);
                // The ring pair lives in the slot only while it shows (an element at opacity 0 has empty draw bounds).
                if(ring>.01f){if(Ring.parent==null){Slot.Insert(0,Gap);Slot.Insert(0,Ring);}Ring.style.opacity=Gap.style.opacity=ring;}
                else if(Ring.parent!=null){Ring.RemoveFromHierarchy();Gap.RemoveFromHierarchy();}
                // The tick never leaves the filtered card (changing a filtered element's children or their visibility trips
                // UI Toolkit's render-tree compositor); unselected, it waits outside the card's clip.
                Tick.style.translate=new Translate(On?0:80,0);
                settled=f>=1;
            }
        }
        private readonly List<ModeCard> modeCards=new List<ModeCard>();
        private Label playSubtitle;
        private void ApplyModes(bool instant=false)
        {
            int selected=Array.IndexOf(V3Modes,Model.SelectedMode)<0?V3Modes[0]:Model.SelectedMode;
            foreach(var m in modeCards)m.Target(m.Index==selected,animationTime,instant);
            if(playSubtitle!=null)playSubtitle.text=PlaySubtitle();
        }
        private string PlaySubtitle()=>Model.SelectedMode==3?L("Dereceli · yakında açılıyor","Ranked · coming soon"):Model.SelectedMode==4?L("Botlara karşı · yakında açılıyor","Vs bots · coming soon"):L("Klasik · Tekli / Eşli · masalar","Classic · Singles / Pairs · tables");
        /// <summary>A mode card: saved, then the cards and HEMEN OYNA's subtitle change in place — nothing is rebuilt.</summary>
        private void PickMode(int index)
        {
            int previous=Model.SelectedMode;Model.SelectedMode=index;
            if(!Model.Persist()){Model.SelectedMode=previous;Toast(L("Değişiklikler kaydedilemedi. Tekrar dene.","Could not save changes. Try again."));}
            ApplyModes();
        }
        /// <summary>HEMEN OYNA: Klasik opens the lobby; a YAKINDA mode only says so.</summary>
        private void Play()
        {
            if(IsSoon(Model.SelectedMode)){Toast(Model.SelectedMode==3?L("Dereceli yakında açılıyor","Ranked is coming soon"):L("Botlara karşı yakında açılıyor","Vs bots is coming soon"));return;}
            PlayRequested?.Invoke(Model.SelectedMode);
        }
        private void BuildFooter()
        {
            var shade=Box();Place(shade,0,610,1280,110);shade.pickingMode=PickingMode.Ignore;Decorate(shade,"linear-gradient(transparent,rgba(0,0,0,.55))");AddMain(shade);
            string[] keys={"shop","friends","leader","daily","rules"},tr={"Mağaza","Arkadaşlar","Liderlik","Günlük ödül","Kurallar"},en={"Shop","Friends","Leaderboard","Daily reward","Rules"};
            string[] backgrounds={"linear-gradient(160deg,var(--a1),var(--a2))","linear-gradient(160deg,#7AA6FF,#3D6BF0)","linear-gradient(160deg,#C2A4FF,#7C4DEB)","linear-gradient(160deg,#4FDB80,#25A857)","linear-gradient(160deg,#5BE6D6,#14A596)"};
            string[] shadows={"var(--aS)","#2448B8","#5530B0","#18793F","#0C746A"},labels={"var(--a1)","#A9C4FF","#D4C2FF","#8EF0B0","#9AF2E8"};
            string[] glows={"var(--gl)","rgba(80,130,255,.4)","rgba(150,110,255,.4)","rgba(60,203,110,.4)","rgba(40,210,190,.4)"};
            var nav=Box("footer-navigation");Place(nav,440,622,400,84);AddMain(nav);
            for(int i=0;i<keys.Length;i++)
            {
                string key=keys[i];var item=Button("",()=>OpenPanel(key),"plain nav-button");item.name="nav-"+key;if(i==keys.Length-1)item.style.marginRight=0;
                var square=Box("nav-square");Decorate(square,backgrounds[i],18,4,shadows[i]);GalaxyFilters.Shadow(square,0,0,i==0?11:10,C(glows[i]));
                var highlight=Box();highlight.pickingMode=PickingMode.Ignore;Place(highlight,0,0,56,56);highlight.style.borderTopLeftRadius=highlight.style.borderTopRightRadius=18;highlight.style.borderTopWidth=1;highlight.style.borderTopColor=new Color(1,1,1,i==0?.5f:i==3?.35f:.4f);square.Add(highlight);
                square.Add(Icon(key,i==0?30:28,i==0?"var(--aT)":"#fff"));item.Add(square);
                var label=Text(L(tr[i],en[i]),12,false,labels[i]);label.style.unityFont=Font("Baloo2-800");label.style.whiteSpace=WhiteSpace.NoWrap;label.style.marginTop=6;item.Add(label);
                if(i==0)Badge(item,"%50",true);if(i==1)Badge(item,Services.Friends.GetFriends(true).Count.ToString());if(i==3&&!Model.DayClaimed){var badge=Badge(square,"1");badge.style.top=-7;badge.style.right=-8;}
                // bob 1.6s, default ease: 0 → -4px → 0.
                if(i==3)Animate(t=>{float f=Mathf.Repeat(t,1.6f)/1.6f;item.style.translate=new Translate(0,-4*(f<.5f?CssBezier(.25f,.1f,.25f,1,f*2):1-CssBezier(.25f,.1f,.25f,1,f*2-1)));});nav.Add(item);
            }
            BuildPlanetPicker();
            var brand=Row();brand.AddToClassList("brand");
            var studio=Row();studio.name="brand-studio";studio.style.opacity=.85f;studio.style.marginRight=8;
            var diamond=Box();diamond.style.width=diamond.style.height=9;diamond.style.rotate=new Rotate(45);diamond.style.marginRight=5;Decorate(diamond,"linear-gradient(var(--a1),var(--a2))",2);studio.Add(diamond);
            var cdx=Text("CDX",12,true);cdx.style.unityFont=Font("Montserrat-800");Tracking(cdx,1,12);cdx.style.marginRight=5;studio.Add(cdx);
            // Baloo 2 sits ~1px higher than Montserrat at these sizes; drop GAMES onto the CDX baseline.
            var games=Text("GAMES",11,false,"var(--mt)");Tracking(games,1.5f,11);games.style.translate=new Translate(0,1);studio.Add(games);brand.Add(studio);
            var separator=Box();separator.style.width=1;separator.style.height=10;separator.style.marginRight=8;separator.style.backgroundColor=C("var(--mt)");separator.style.opacity=.4f;brand.Add(separator);
            var version=Text("v0.2.0",11,false,"var(--mt)");version.style.letterSpacing=0;version.style.opacity=.6f;brand.Add(version);AddMain(brand);
        }
        private void BuildPlanetPicker()
        {
            var picker=Box("planet-picker");var label=Text(L("GEZEGEN TEMASI","PLANET THEME"),10,false,"var(--mt)");label.style.unityFont=Font("Baloo2-800");Tracking(label,1.5f,10);label.style.marginLeft=4;label.style.marginBottom=8;picker.Add(label);
            var pill=Row();pill.name="planet-theme-pill";pill.AddToClassList("planet-pill");pill.style.height=48;pill.style.alignSelf=Align.FlexStart;Decorate(pill,"rgba(0,0,0,.35)",24);GalaxyFilters.BackdropBlur(pill,8);
            var orbs=Row();orbs.name="planet-theme-orbs";orbs.style.marginRight=10;pill.Add(orbs);
            for(int i=0;i<PlanetNames.Length;i++)
            {
                string name=PlanetNames[i];var data=Model.Themes[name];bool selected=name==Model.Planet;int size=selected?34:26;
                var orb=Button("",()=>{if(!Services.Settings.SetPlanet(name))Toast(L("Değişiklikler kaydedilemedi. Tekrar dene.","Could not save changes. Try again."));},"plain planet-orb");orb.name="theme-"+PlanetIds[i];orb.tooltip=(string)data[Loc.Language=="en"?"name_en":"name_tr"];orb.style.width=orb.style.height=size;
                orb.style.marginRight=i==PlanetNames.Length-1?0:6;orb.style.overflow=Overflow.Visible;Decorate(orb,"transparent",size/2);
                var outline=Box();outline.pickingMode=PickingMode.Ignore;int spread=selected?4:1;Place(outline,-spread,-spread,size+spread*2,size+spread*2);Decorate(outline,selected?(string)data["cssVars"]?["a1"]:"rgba(255,255,255,.15)",size/2+spread);orb.Add(outline);
                if(selected)
                {
                    var separator=Box();separator.pickingMode=PickingMode.Ignore;Place(separator,-2,-2,size+4,size+4);Decorate(separator,"rgba(0,0,0,.5333333333)",size/2+2);orb.Add(separator);
                    GalaxyFilters.Shadow(orb,0,0,7,C((string)data["cssVars"]?["gl"]));
                }
                var ramp=(JArray)data["pickerOrb"];var face=Box();face.pickingMode=PickingMode.Ignore;Place(face,0,0,size,size);Decorate(face,$"radial-gradient(circle at 35% 30%,{ramp[0]},{ramp[1]} 55%,{ramp[2]})",size/2);orb.Add(face);orbs.Add(orb);
            }
            var theme=Model.Themes[Model.Planet];var title=Text((string)theme[Loc.Language=="en"?"name_en":"name_tr"],15,true,"var(--a1)");title.style.height=16.5f;title.style.flexShrink=0;title.style.whiteSpace=WhiteSpace.NoWrap;
            var description=Text((string)theme[Loc.Language=="en"?"desc_en":"desc_tr"],10,false,"var(--mt)");description.style.whiteSpace=WhiteSpace.NoWrap;
            var names=Column(title,description);names.style.minWidth=64;pill.Add(names);picker.Add(pill);AddMain(picker);
        }
        /// <summary>Planet orb with the reference rings: selected "0 0 0 2px #0008, 0 0 0 4px a1, 0 0 14px gl", else "0 0 0 1px rgba(255,255,255,.15)".</summary>
        public VisualElement PlanetOrb(string planet,int size,bool selected)
        {
            var data=Model.Themes[planet];var orb=Box("planet-orb-face");orb.pickingMode=PickingMode.Ignore;orb.style.width=orb.style.height=size;orb.style.flexShrink=0;
            int spread=selected?4:1;var outline=Box();outline.pickingMode=PickingMode.Ignore;Place(outline,-spread,-spread,size+spread*2,size+spread*2);Decorate(outline,selected?(string)data["cssVars"]?["a1"]:"rgba(255,255,255,.15)",size/2+spread);orb.Add(outline);
            if(selected)
            {
                var separator=Box();separator.pickingMode=PickingMode.Ignore;Place(separator,-2,-2,size+4,size+4);Decorate(separator,"rgba(0,0,0,.5333333333)",size/2+2);orb.Add(separator);
                GalaxyFilters.Shadow(orb,0,0,7,C((string)data["cssVars"]?["gl"]));
            }
            var ramp=(JArray)data["pickerOrb"];var face=Box();face.pickingMode=PickingMode.Ignore;Place(face,0,0,size,size);Decorate(face,$"radial-gradient(circle at 35% 30%,{ramp[0]},{ramp[1]} 55%,{ramp[2]})",size/2);orb.Add(face);
            return orb;
        }
        /// <summary>Settings account avatar: color disc, 3px border in the frame accent (classic a1, gold #FFD964) and the frame's box-shadow.</summary>
        public VisualElement AccountAvatar(int size)
        {
            int frame=(int?)Model.Profile["frame"]??0;var root=Box("account-avatar");root.style.width=root.style.height=size;root.style.flexShrink=0;
            if(frame==1)
            {
                var glow=Box();glow.pickingMode=PickingMode.Ignore;Place(glow,0,0,size,size);Decorate(glow,"#5A3A08",size/2);GalaxyFilters.Shadow(glow,0,0,8,new Color(1,.788f,.29f,.55f));root.Add(glow);
                var ring=Box();ring.pickingMode=PickingMode.Ignore;Place(ring,-2,-2,size+4,size+4);Decorate(ring,"#5A3A08",size/2+2);root.Add(ring);
            }
            var disc=Box();disc.pickingMode=PickingMode.Ignore;Place(disc,0,0,size,size);Decorate(disc,(string)Model.Profile["color"]??"#FF8A5B",size/2);disc.style.alignItems=Align.Center;disc.style.justifyContent=Justify.Center;
            disc.style.borderLeftWidth=disc.style.borderRightWidth=disc.style.borderTopWidth=disc.style.borderBottomWidth=3;
            disc.style.borderLeftColor=disc.style.borderRightColor=disc.style.borderTopColor=disc.style.borderBottomColor=C(frame==1?"#FFD964":"var(--a1)");
            disc.Add(Text(ProfileValidation.Initials((string)Model.Profile["name"]??""),20,true,"#fff"));root.Add(disc);return root;
        }
        private Label Badge(VisualElement parent,string text,bool sale=false)
        {
            var badge=Text(text,sale?10:11,false,"#fff");badge.style.unityFont=Font(sale?"Nunito-900":"Baloo2-800");badge.AddToClassList(sale?"sale-badge":"notification-badge");badge.style.whiteSpace=WhiteSpace.NoWrap;
            if(sale){badge.style.height=18;badge.style.paddingTop=badge.style.paddingBottom=4;badge.style.paddingLeft=badge.style.paddingRight=6;badge.style.unityTextAlign=TextAnchor.MiddleCenter;}
            Decorate(badge,"#FF5E6C",sale?7:10,sale?2:0,"#C23E4A");parent.Add(badge);return badge;
        }
        private void Animate(Action<float> action){(buildingMotions??motions).Add(action);action(animationTime);}
        /// <summary>CSS cubic-bezier timing function, solved for the curve's x by bisection.</summary>
        /// <summary>
        /// Chrome's "line-height: normal" for the design fonts: the hhea ascent and descent, each rounded to whole pixels
        /// (Montserrat .968 / .251 em, Baloo 2 1.078 / .524 em) — Montserrat 14 px is an 18 px line, not 17.07.
        /// </summary>
        public static float CssLineHeight(string font,float size)
        {
            bool baloo=font!=null&&font.StartsWith("Baloo");float ascent=baloo?1.078f:.968f,descent=baloo?.524f:.251f;
            return Mathf.Floor(ascent*size+.5f)+Mathf.Floor(descent*size+.5f);
        }

        internal static float CssBezier(float x1,float y1,float x2,float y2,float x)
        {
            float lo=0,hi=1,t=x;for(int i=0;i<20;i++){t=(lo+hi)/2;float q=1-t,bx=3*q*q*t*x1+3*q*t*t*x2+t*t*t;if(bx<x)lo=t;else hi=t;}
            return 3*(1-t)*(1-t)*t*y1+3*(1-t)*t*t*y2+t*t*t;
        }
        /// <summary>
        /// Play button: shine 2.8s (translateX -160%→320% skewX(-20deg), ease over 0–60%), 60px rgba(255,255,255,.45), static-centered.
        /// Currency icons: iconShine 3.2s ease-in-out (translateX -14→34px over 0–30%, opacity 0→1 by 8%, out at 30–31%), feathered strip.
        /// </summary>
        private void Shine(VisualElement parent,float period,float delay,bool icon,float width=60,float overhang=0)
        {
            var shine=new SkewBand(width,-overhang,-overhang,-20,new Color(1,1,1,icon?.95f:.45f),icon);parent.Add(shine);
            Animate(t=>
            {
                float f=Mathf.Repeat(t-delay,period)/period;
                if(!icon){shine.Left=150+width*Mathf.Lerp(-1.6f,3.2f,f<.6f?CssBezier(.25f,.1f,.25f,1,f/.6f):1);return;}
                shine.Left=Mathf.Lerp(-14,34,f<.3f?CssBezier(.42f,0,.58f,1,f/.3f):1);
                shine.Opacity=f<.08f?CssBezier(.42f,0,.58f,1,f/.08f):f<.3f?1:f<.31f?1-CssBezier(.42f,0,.58f,1,(f-.3f)/.01f):0;
            });
        }
        public static void Place(VisualElement element,float x,float y,float width,float height)
        {element.style.position=Position.Absolute;element.style.left=x;element.style.top=y;element.style.width=width;element.style.height=height;}
    }
}
