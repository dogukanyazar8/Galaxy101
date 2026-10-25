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
            mainSignature=null;sections.Clear();mountedPanel=mountedDialog=null;panelCard=dialogCard=null;
            animationJob?.Pause();float last=Time.realtimeSinceStartup;
            // Every panel update — each rendered frame, at 144 Hz too — rather than a fixed 16 ms that beats against the
            // display. The main layer's own sections rest while it is hidden (lobby, a scene on top).
            animationJob=Root.schedule.Execute(()=>
            {
                float now=Time.realtimeSinceStartup;float delta=Mathf.Clamp(now-last,0,.05f);last=now;if(!focused)return;
                // Frozen (state router, D-61): every animation sits at the canonical instant, as G101Clock.freeze sets each CSS
                // animation's currentTime to 100000 + ft ms.
                if(GameClock.Frozen)animationTime=GameClock.Seconds;else animationTime+=delta;
                foreach(var motion in motions)motion(animationTime);
                if(mainLayer!=null&&mainLayer.resolvedStyle.display!=DisplayStyle.None)foreach(var section in sections.Values)foreach(var motion in section.Motions)motion(animationTime);
                foreach(var motion in panelMotions)motion(animationTime);
                foreach(var motion in dialogMotions)motion(animationTime);
            }).Every(0);
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
                Section("top",common+"|"+Model.Chips+"|"+Model.Gems+"|"+Model.Level+"|"+CurrentRankIndex()+"|"+Model.Profile.ToString(Newtonsoft.Json.Formatting.None),BuildTopBar);
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
                // No blur behind panels and dialogs: the design's overlay sits in a fadeIn container, which Chrome makes the
                // backdrop root, so its backdrop-filter has nothing to blur — the menu shows through only darkened.
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
            var avatar=Avatar(62,level:true,topBar:true);GalaxyFilters.HoverBrightness(avatar,1.1f);avatar.name="menu.openMe";avatar.tooltip=L("Profilim","My profile");avatar.AddToClassList("avatar-button");avatar.focusable=true;avatar.RegisterCallback<ClickEvent>(_=>OpenPanel("me"));avatar.RegisterCallback<NavigationSubmitEvent>(evt=>{OpenPanel("me");evt.StopImmediatePropagation();});top.Add(avatar);
            avatar.Q<Label>("avatar-initials").name="menu.me.ini";
            // Name over the league row (gap 5): 24 px G101Emblem, gap 8, rank name 700 14px in the league's colour (rk.txt).
            int rank=CurrentRankIndex();var emblem=Emblem(rank,24);emblem.style.marginRight=8;
            var rankName=Text(RankLabel(rank),14,true,RankInk(rank));rankName.name="menu.t.rank";rankName.style.whiteSpace=WhiteSpace.NoWrap;
            var me=Text((string)Model.Profile["name"],18,true);me.name="menu.me.name";me.style.whiteSpace=WhiteSpace.NoWrap;
            var name=Column(me,Row(emblem,rankName));name.AddToClassList("top-name");top.Add(name);
            var spacer=Box();spacer.style.flexGrow=1;top.Add(spacer);
            top.Add(Currency(false));top.Add(Currency(true));
            var settingsButton=Button("",()=>OpenPanel("settings"),"secondary settings-button");settingsButton.name="menu.openSettings";settingsButton.tooltip=L("Ayarlar","Settings");settingsButton.Add(Icon("settings",24));top.Add(settingsButton);
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
            var amount=Text(N(gems?Model.Gems:Model.Chips),17,true);amount.name=gems?"menu.gemsLabel":"menu.chipsLabel";amount.style.minWidth=gems?44:84;pill.Add(amount);
            var plus=Button("+",()=>OpenPanel("shop",gems?1:0),"success currency-plus");plus.name=gems?"menu.buyGems":"menu.buyChips";plus.tooltip=L(gems?"Elmas satın al":"Çip satın al",gems?"Buy gems":"Buy chips");plus.style.fontSize=20;plus.style.unityTextAlign=TextAnchor.MiddleCenter;Decorate(plus,"#3CCB6E",14,3,"#22914A");pill.Add(plus);return pill;
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
                // g101TileIn 1.1s cubic-bezier(.16,1,.3,1) both, delays .15/.29/.43 s: opacity 0 → 1 by 55 %, translateY(46px)
                // scale(.82) → none (its rotateX(-70deg) has no 2D equivalent, UNITY_FARKLARI UF-601); then g101Float 6 s
                // cubic-bezier(.45,0,.55,1) infinite, delays 0/-2/-4 s: translate 0 → -7px (50 %) → 0.
                float delay=.15f+.14f*i;
                Animate(t=>
                {
                    float f=Mathf.Clamp01((t-delay)/1.1f),k=CssBezier(.16f,1,.3f,1,f);tile.style.opacity=f>=.55f?1:CssBezier(.16f,1,.3f,1,f/.55f);
                    float p=Mathf.Repeat(t+2*index,6)/6,y=-7*(p<.5f?CssBezier(.45f,0,.55f,1,p*2):1-CssBezier(.45f,0,.55f,1,p*2-1));
                    tile.style.translate=new Translate(0,y+46*(1-k));tile.style.scale=new Scale(Vector2.one*(.82f+.18f*k));
                });
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
            var play=Button("",Play,"primary play-button");play.name="menu.play";Place(play,0,0,360,86);Decorate(play,"linear-gradient(var(--a1),var(--a2))",24);play.style.overflow=Overflow.Hidden;
            play.RegisterCallback<PointerDownEvent>(_=>host.AddToClassList("pressed"),TrickleDown.TrickleDown);
            play.RegisterCallback<PointerUpEvent>(_=>host.RemoveFromClassList("pressed"),TrickleDown.TrickleDown);
            play.RegisterCallback<PointerCaptureOutEvent>(_=>host.RemoveFromClassList("pressed"));
            Shine(play,2.8f,0,false);var title=Text(L("HEMEN OYNA","PLAY NOW"),34,true,"var(--aT)");Tracking(title,1,34);title.name="menu.t.play";title.style.whiteSpace=WhiteSpace.NoWrap;title.style.height=36;play.Add(title);
            var subtitle=Text(PlaySubtitle(),13,false,"var(--aT)");playSubtitle=subtitle;subtitle.name="menu.playSub";subtitle.style.unityFont=Font("Baloo2-800");subtitle.style.marginTop=4;subtitle.style.opacity=.8f;play.Add(subtitle);host.Add(play);
        }
        /// <summary>
        /// v4 modes (G101-Menu modes, menu.STR.modes): Klasik (Tekli / Eşli tables in the lobby), Dereceli, Botlara karşı — all open (soon: false).
        /// Cards keep the model's mode indices (0 Klasik, 3 Dereceli, 4 Botlara karşı) so saves and tests stay valid.
        /// </summary>
        public static readonly int[] V3Modes = { 0, 3, 4 };
        /// <summary>v4: no mode is YAKINDA (G101-Menu modes[].soon = false; KARARLAR K-10).</summary>
        public static bool IsSoon(int mode) => false;
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
                card.name="menu.m.pick["+i+"]";Place(card,0,0,208,78);Decorate(card,color,18,5,shade);
                card.style.flexDirection=FlexDirection.Column;card.style.justifyContent=Justify.Center;card.style.alignItems=Align.FlexStart;card.style.paddingLeft=card.style.paddingRight=14;
                // overflow: hidden on an inner layer, so the card's 5 px ledge below it is not clipped with the glyph.
                var clip=Box("mode-clip");clip.pickingMode=PickingMode.Ignore;clip.style.position=Position.Absolute;clip.style.left=clip.style.top=clip.style.right=clip.style.bottom=0;clip.style.overflow=Overflow.Hidden;
                clip.style.borderTopLeftRadius=clip.style.borderTopRightRadius=clip.style.borderBottomLeftRadius=clip.style.borderBottomRightRadius=18;card.Add(clip);
                // The design's white selection/hover ring (box-shadow 0 2.5px 0 5px #fff) is left out on purpose: the cards
                // only lift, light up and glow.
                // Mode glyph: 700 58px/1, rgba(255,255,255,.16), right -4 bottom -14, clipped by the card.
                var glyph=Text(glyphs[i],58,true,"rgba(255,255,255,.16)");glyph.name="menu.m.glyph["+i+"]";glyph.style.height=58;glyph.style.unityTextAlign=TextAnchor.MiddleRight;glyph.pickingMode=PickingMode.Ignore;
                glyph.style.position=Position.Absolute;glyph.style.right=-4;glyph.style.bottom=-14;clip.Add(glyph);
                var gloss=Box();Place(gloss,0,0,208,39);gloss.pickingMode=PickingMode.Ignore;Decorate(gloss,"linear-gradient(rgba(255,255,255,.18),transparent)",18);clip.Add(gloss);
                var title=Text(names[i],18,true,"#fff");title.name="menu.m.name["+i+"]";title.pickingMode=PickingMode.Ignore;card.Add(title);
                var line=Row();line.pickingMode=PickingMode.Ignore;line.style.marginTop=4;var dot=Box();dot.style.width=dot.style.height=6;dot.style.marginRight=5;Decorate(dot,"#8EF0B0",3);GalaxyFilters.Shadow(dot,0,0,3,C("#8EF0B0"));line.Add(dot);
                var count=Text(online[i],11,false,"rgba(255,255,255,.9)");count.style.unityFont=Font("Baloo2-800");line.Add(count);card.Add(line);
                if(IsSoon(index)){var soon=Text(L("YAKINDA","SOON"),9,false,"#fff");soon.name="menu.t.soon["+i+"]";soon.style.unityFont=Font("Baloo2-800");Tracking(soon,1.2f,9);soon.style.position=Position.Absolute;soon.style.right=10;soon.style.bottom=9;soon.style.paddingTop=soon.style.paddingBottom=3;soon.style.paddingLeft=soon.style.paddingRight=7;Decorate(soon,"rgba(0,0,0,.32)",6);clip.Add(soon);}
                var tick=Box("mode-tick");tick.pickingMode=PickingMode.Ignore;tick.style.position=Position.Absolute;tick.style.right=8;tick.style.top=8;tick.style.width=tick.style.height=22;tick.style.alignItems=Align.Center;tick.style.justifyContent=Justify.Center;Decorate(tick,"#fff",99);tick.Add(Icon("tick",13,"#1B2440"));clip.Add(tick);
                Color glow=C(color);glow.a=.6f;
                var state=new ModeCard{Index=index,Card=card,Tick=tick,Glow=glow};modeCards.Add(state);
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
            public int Index;public Button Card;public VisualElement Tick;public Color Glow;public bool Hover,Focused,On;
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
        // menu.STR.subs.0–2
        private string PlaySubtitle()=>Model.SelectedMode==3?L("Dereceli · Solo / Duo / Squad","Ranked · Solo / Duo / Squad"):Model.SelectedMode==4?L("Botlara karşı · antrenman · bahis yok","Vs bots · practice · no stake"):L("Klasik · Tekli / Eşli · masalar","Classic · Singles / Pairs · tables");
        /// <summary>A mode card: saved, then the cards and HEMEN OYNA's subtitle change in place — nothing is rebuilt.</summary>
        private void PickMode(int index)
        {
            int previous=Model.SelectedMode;Model.SelectedMode=index;
            if(!Model.Persist()){Model.SelectedMode=previous;Toast(L("Değişiklikler kaydedilemedi. Tekrar dene.","Could not save changes. Try again."));}
            ApplyModes();
        }
        /// <summary>HEMEN OYNA: Klasik opens the lobby, Dereceli the ranked screen, Botlara karşı the practice table.</summary>
        private void Play()
        {
            if(IsSoon(Model.SelectedMode)){Toast(L("Botlara karşı yakında açılıyor","Vs bots is coming soon"));return;}
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
            var version=Text("v"+UnityEngine.Application.version,11,false,"var(--mt)");version.style.letterSpacing=0;version.style.opacity=.6f;brand.Add(version);AddMain(brand);
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
        private Label Badge(VisualElement parent,string text,bool sale=false)
        {
            var badge=Text(text,sale?10:11,false,"#fff");badge.style.unityFont=Font(sale?"Nunito-700":"Baloo2-800");// %50: 900 10px Nunito — the prototype loads only Nunito 700 (vendor/fonts.css)badge.AddToClassList(sale?"sale-badge":"notification-badge");badge.style.whiteSpace=WhiteSpace.NoWrap;
            if(sale){badge.style.height=18;badge.style.paddingTop=badge.style.paddingBottom=4;badge.style.paddingLeft=badge.style.paddingRight=6;badge.style.unityTextAlign=TextAnchor.MiddleCenter;}
            Decorate(badge,"#FF5E6C",sale?7:10,sale?2:0,"#C23E4A");parent.Add(badge);return badge;
        }
        private void Animate(Action<float> action){(buildingMotions??motions).Add(action);action(animationTime);}
        /// <summary>CSS cubic-bezier timing function, solved for the curve's x by bisection.</summary>
        /// <summary>
        /// Chrome's "line-height: normal" for the design fonts: the hhea ascent and descent, each rounded to whole pixels
        /// (Montserrat .968 / .251 em, Baloo 2 1.078 / .524 em) — Montserrat 14 px is an 18 px line, not 17.07.
        /// </summary>
        /// <summary>A CSS line box: <paramref name="height"/> px (or the font's "normal" line height) with the glyphs centred in it.</summary>
        public static Label CssLine(Label label,float height=0)
        {
            var font=label.style.unityFont.value;
            label.style.height=height>0?height:CssLineHeight(font!=null?font.name:null,label.style.fontSize.value.value);label.style.flexShrink=0;
            var align=label.style.unityTextAlign.value;
            label.style.unityTextAlign=align==TextAnchor.UpperCenter||align==TextAnchor.MiddleCenter||align==TextAnchor.LowerCenter?TextAnchor.MiddleCenter
                :align==TextAnchor.UpperRight||align==TextAnchor.MiddleRight||align==TextAnchor.LowerRight?TextAnchor.MiddleRight:TextAnchor.MiddleLeft;
            return label;
        }
        public static float CssLineHeight(string font,float size)
        {
            bool baloo=font!=null&&font.StartsWith("Baloo");float ascent=baloo?1.078f:.968f,descent=baloo?.524f:.251f;
            return Mathf.Floor(ascent*size+.5f)+Mathf.Floor(descent*size+.5f);
        }

        /// <summary>
        /// menuIn 1.2s cubic-bezier(.16,1,.3,1) after the splash: the menu fades in from 1.05× and a 12 px blur that clears
        /// by 60 %.
        /// </summary>
        public void PlayMenuIn()
        {
            if(mainLayer==null)return;var target=mainLayer;float began=Time.realtimeSinceStartup;
            target.style.opacity=0;target.style.scale=new Scale(Vector2.one*1.05f);GalaxyFilters.Blur(target,12);
            IVisualElementScheduledItem job=null;
            job=target.schedule.Execute(()=>
            {
                float f=Mathf.Clamp01((Time.realtimeSinceStartup-began)/1.2f),k=CssBezier(.16f,1,.3f,1,f);
                target.style.opacity=Mathf.Clamp01(k);target.style.scale=new Scale(Vector2.one*(1.05f-.05f*k));
                GalaxyFilters.Blur(target,f<.6f?12*(1-CssBezier(.16f,1,.3f,1,f/.6f)):0);
                if(f>=1){target.style.opacity=StyleKeyword.Null;target.style.scale=StyleKeyword.Null;target.style.filter=StyleKeyword.Null;job.Pause();}
            }).Every(0);
        }

        /// <summary>
        /// A CSS paragraph: lines <paramref name="lineHeight"/> px apart with the glyphs centred in each line box, laid out
        /// before the first frame (no relayout): rich-text line-height between lines, and the half-leading of the first and
        /// last line taken off the label's vertical margins (set them before calling). With <paramref name="pretty"/>,
        /// text-wrap: pretty — the last line never holds a single word: the word before it moves down with it, as Chrome
        /// balances the end of a paragraph; the last break found for the text is reused so a rebuild does not re-wrap.
        /// </summary>
        public static Label CssParagraph(Label label,float lineHeight,bool pretty)
        {
            string plain=label.text??"";label.enableRichText=true;label.style.whiteSpace=WhiteSpace.Normal;label.style.flexShrink=0;
            // The text itself is never markup (a player's status may hold "<b>"): only the line-height tag is parsed.
            string Wrap(string body)=>"<line-height="+lineHeight.ToString("0.##",System.Globalization.CultureInfo.InvariantCulture)+"px><noparse>"
                +System.Text.RegularExpressions.Regex.Replace(body,"</noparse",m=>"<\u200B/noparse",System.Text.RegularExpressions.RegexOptions.IgnoreCase)+"</noparse>";
            var font=label.style.unityFont.value;float size=label.style.fontSize.value.value;
            float half=(lineHeight-NaturalLineHeight(font!=null?font.name:null,size))/2;
            label.style.marginTop=label.style.marginTop.value.value+half;label.style.marginBottom=label.style.marginBottom.value.value+half;
            string key=size+"|"+plain;
            label.text=Wrap(pretty&&prettyBreaks.TryGetValue(key,out var known)?known:plain);
            if(!pretty)return label;
            float laidFor=-1;
            label.RegisterCallback<GeometryChangedEvent>(_=>
            {
                float width=label.contentRect.width;if(width<=1||Mathf.Abs(width-laidFor)<.5f)return;laidFor=width;
                string broken=Pretty(label,plain,width);prettyBreaks[key]=broken;
                if(label.text!=Wrap(broken))label.text=Wrap(broken);
            });
            return label;
        }
        private static readonly Dictionary<string,string> prettyBreaks=new Dictionary<string,string>();
        /// <summary>A label's single-line height: the font's ascent + descent (<see cref="CssLineHeight"/>'s metrics), rounded up.</summary>
        private static float NaturalLineHeight(string font,float size)
        {
            bool baloo=font!=null&&font.StartsWith("Baloo");return Mathf.Ceil(size*(baloo?1.078f+.524f:.968f+.251f)-.01f);
        }
        private static string Pretty(Label label,string plain,float width)
        {
            var words=plain.Split(' ');if(words.Length<3)return plain;
            // Greedy lines as the label would break them.
            var lines=new List<int>();int start=0;
            for(int i=1;i<=words.Length;i++)
            {
                string candidate=string.Join(" ",words,start,i-start);
                if(i-start>1&&label.MeasureTextSize(candidate,0,VisualElement.MeasureMode.Undefined,0,VisualElement.MeasureMode.Undefined).x>width){lines.Add(start);start=i-1;}
            }
            lines.Add(start);
            if(lines.Count<2||lines[lines.Count-1]!=words.Length-1)return plain;
            int cut=words.Length-2;
            return string.Join(" ",words,0,cut)+"\n"+string.Join(" ",words,cut,words.Length-cut);
        }

        /// <summary>font-variant-numeric: tabular-nums — every digit on the font's tabular advance (Montserrat 0.69 em, Baloo 2 0.59 em).</summary>
        public static Label TabularDigits(Label label)
        {
            string em=label.style.unityFont.value!=null&&label.style.unityFont.value.name.StartsWith("Baloo")?"0.59em":"0.69em";
            label.enableRichText=true;
            label.text=System.Text.RegularExpressions.Regex.Replace(label.text,"[0-9]+",m=>"<mspace="+em+">"+m.Value+"</mspace>");
            return label;
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
