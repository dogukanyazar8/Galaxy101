using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    public sealed partial class MenuView : IDisposable
    {
        public MenuModel Model {get;}
        public MenuServices Services {get;}
        public LocalizationBridge Loc {get;}
        public VisualElement Root {get;}
        public ModalStack Modals {get;}=new ModalStack();
        public event Action<int> PlayRequested;
        public event Action<string,bool,string> BackgroundChanged;
        private readonly Dictionary<string,int> tabs=new Dictionary<string,int>();
        private readonly Dictionary<string,Font> fonts=new Dictionary<string,Font>();
        private readonly Dictionary<string,Texture2D> paints=new Dictionary<string,Texture2D>();
        private sealed class GradientPaint { public string Css; }
        private readonly ConditionalWeakTable<VisualElement,GradientPaint> gradientPaints=new ConditionalWeakTable<VisualElement,GradientPaint>();
        private sealed class Corners{public float Radius;}
        private readonly ConditionalWeakTable<VisualElement,Corners> corners=new ConditionalWeakTable<VisualElement,Corners>();
        private readonly SettingsController settings=new SettingsController();
        private readonly ShopController shop=new ShopController();
        private readonly FriendsController friends=new FriendsController();
        private readonly LeaderboardController leader=new LeaderboardController();
        private readonly DailyRewardController daily=new DailyRewardController();
        private readonly RulesController rules=new RulesController();
        private readonly ProfileController profile=new ProfileController();
        private readonly EditProfileController edit=new EditProfileController();
        private readonly AddFriendController addFriend=new AddFriendController();
        private readonly ConfirmController confirm=new ConfirmController();
        private readonly DoneController done=new DoneController();
        private readonly ShareController share=new ShareController();
        private VisualElement mainLayer,panelLayer,dialogLayer,toastLayer;
        private string focusBeforePanel,focusBeforeDialog,focusAfterClose;
        private JObject preview;
        private bool disposed,rebuilding;
        private IVisualElementScheduledItem refreshJob;
        private readonly List<Action<float>> motions=new List<Action<float>>();
        private readonly List<Action<float>> panelMotions=new List<Action<float>>();
        private readonly List<Action<float>> dialogMotions=new List<Action<float>>();
        private List<Action<float>> buildingMotions;
        private float panelAnimationOrigin,dialogAnimationOrigin;
        private string mainSignature,mountedPanel,mountedDialog;
        private VisualElement panelCard,dialogCard;
        private float animationTime;
        private bool focused=true;
        private string lastTheme;
        public VisualElement BackgroundTarget {get;private set;}
        public string DoneFrom {get;set;}="logout";

        public MenuView(VisualElement root,MenuModel model,MenuServices services,LocalizationBridge loc)
        {
            Root=root??throw new ArgumentNullException(nameof(root));Model=model??throw new ArgumentNullException(nameof(model));
            Services=services??throw new ArgumentNullException(nameof(services));Loc=loc??throw new ArgumentNullException(nameof(loc));
            var style=Resources.Load<StyleSheet>("Galaxy101/UI/Base");if(style!=null)Root.styleSheets.Add(style);
            var panelStyle=Resources.Load<StyleSheet>("Galaxy101/UI/Panels");if(panelStyle!=null)Root.styleSheets.Add(panelStyle);
            foreach(var name in new[]{"Saturn","Mars","Neptune","Jupiter","Venus"}){var theme=Resources.Load<StyleSheet>("Galaxy101/UI/Theme."+name);if(theme!=null)Root.styleSheets.Add(theme);}
            Root.AddToClassList("galaxy-root");Root.focusable=true;
            Root.RegisterCallback<KeyDownEvent>(OnKey,TrickleDown.TrickleDown);Root.RegisterCallback<PointerDownEvent>(OnPointer,TrickleDown.TrickleDown);Root.RegisterCallback<NavigationSubmitEvent>(OnSubmit,TrickleDown.TrickleDown);
            Model.Changed+=Refresh;Loc.Changed+=Refresh;
            Services.Online.Notice+=(tr,en,kind)=>{if(!disposed)Flash(L(tr,en),kind);}; // a signed-in action's outcome
        }
        public string L(string tr,string en=null)=>Loc.Text(tr,en);
        public string N(long value)=>NumberFormat.Number(value,Loc.Language);
        public string P(int value)=>NumberFormat.Percent(value,Loc.Language);
        public Color C(string input)
        {
            if(string.IsNullOrEmpty(input)||input=="transparent"||input=="none")return Color.clear;
            var token=Model.Themes[Model.Planet]?["cssVars"]?[input];if(token!=null)input=(string)token;
            if(input.StartsWith("var(--",StringComparison.Ordinal)){string key=input.Substring(6).TrimEnd(')');input=(string)Model.Themes[Model.Planet]?["cssVars"]?[key]??"transparent";}
            if(ColorUtility.TryParseHtmlString(input,out var color))return color;
            var match=Regex.Match(input,@"rgba?\(([^)]+)\)");
            if(match.Success){var parts=match.Groups[1].Value.Split(',');return new Color(float.Parse(parts[0],CultureInfo.InvariantCulture)/255,float.Parse(parts[1],CultureInfo.InvariantCulture)/255,float.Parse(parts[2],CultureInfo.InvariantCulture)/255,parts.Length==4?float.Parse(parts[3],CultureInfo.InvariantCulture):1);}
            throw new ArgumentException("Unsupported design color: "+input,nameof(input));
        }
        public VisualElement Box(string classes="")
        {var item=new VisualElement();foreach(string name in classes.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries))item.AddToClassList(name);return item;}
        public Label Text(string text,int size=15,bool heading=false,string color=null)
        {
            var label=new Label(text){pickingMode=PickingMode.Ignore,enableRichText=false};label.AddToClassList(heading?"heading":"body");
            label.style.fontSize=size;label.style.unityFont=Font(heading?"Montserrat-700":"Baloo2-700");label.style.color=C(color??"#F4F1E8");
            return label;
        }
        /// <summary>
        /// CSS letter-spacing in px. UI Toolkit applies letter-spacing as hundredths of the font size,
        /// so a reference "1.5px" at 10px text must be authored as 15.
        /// </summary>
        public static void Tracking(VisualElement element,float px,float fontSize){element.style.letterSpacing=px*100f/fontSize;}
        public Font Font(string key)
        {if(!fonts.TryGetValue(key,out var font)){font=Resources.Load<Font>("Galaxy101/Fonts/"+key);if(font==null)throw new InvalidOperationException("Missing authored Galaxy font: "+key);fonts.Add(key,font);}return font;}
        public Button Button(string text,Action click,string classes="primary")
        {
            var button=new GalaxyButton(click){text=text,enableRichText=false};button.AddToClassList("galaxy-button");
            foreach(string item in classes.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries))button.AddToClassList(item);
            button.style.unityFont=Font("Montserrat-700");button.style.fontSize=15;button.style.color=C(classes.Contains("primary")?"var(--aT)":"#F4F1E8");
            string bg=classes.Contains("danger")?"#FF5E6C":classes.Contains("success")?"#3CCB6E":classes.Contains("secondary")?"var(--pnH)":classes.Contains("plain")?"transparent":"linear-gradient(var(--a1),var(--a2))";
            string shade=classes.Contains("danger")?"#C23E4A":classes.Contains("success")?"#22914A":classes.Contains("secondary")?"var(--pnS)":"var(--aS)";
            Decorate(button,bg,14,classes.Contains("plain")?0:4,shade);
            // style-hover: brightness 1.25 on panel-coloured (secondary) controls, 1.12 on green and nav buttons, 1.08 on gold.
            // "bright-NNN": the v4 style-hover filter:brightness(N/100) of that control (G101-Menu, e.g. bright-120).
            var tuned=Regex.Match(classes,@"(?<![\w-])bright-(\d+)");
            float brightness=tuned.Success?int.Parse(tuned.Groups[1].Value,CultureInfo.InvariantCulture)/100f:classes.Contains("mode-card")?1:classes.Contains("settings-button")||classes.Contains("secondary")?1.25f:classes.Contains("nav-button")||classes.Contains("success")?1.12f:1.08f;
            if(brightness>1)GalaxyFilters.HoverBrightness(button,brightness);return button;
        }
        public VisualElement Row(params VisualElement[] children){var row=Box("row");foreach(var child in children)if(child!=null)row.Add(child);return row;}
        public VisualElement Column(params VisualElement[] children){var column=Box("column");foreach(var child in children)if(child!=null)column.Add(child);return column;}
        public VisualElement Template(string name)
        {
            var asset=Resources.Load<VisualTreeAsset>("Galaxy101/UI/"+name);
            if(asset==null)throw new InvalidOperationException("Missing Galaxy UI template: "+name);
            return asset.CloneTree();
        }
        /// <summary>
        /// Every text input's look: the caret in the text colour (caret-color: auto), the browser's selection highlight and,
        /// while empty, the shell's placeholder colour (input::placeholder rgba(169,174,221,.5)).
        /// </summary>
        public void InputLook(TextField field,string color)
        {
            Color text=C(color),hint=new Color(169/255f,174/255f,221/255f,.5f);
            field.textSelection.cursorColor=text;field.textSelection.selectionColor=new Color(.2f,.56f,1f,.45f);
            var input=field.Q(className:TextField.inputUssClassName);if(input==null)return;
            void Tint()=>input.style.color=string.IsNullOrEmpty(field.value)?hint:text;
            field.RegisterValueChangedCallback(_=>Tint());Tint();
        }
        /// <summary>
        /// An input box's CSS inset ring, drawn over the box without layout (a border would shift the text on focus):
        /// "rest" inset 0 2px 0 rgba(0,0,0,.3), "focus" inset 0 0 0 2px --a1, "bad" #FF5E6C, "line" --bd. Returns the setter.
        /// </summary>
        public Action<string> InsetRing(VisualElement box,float radius)
        {
            var ring=Box();ring.name="inset-ring";ring.pickingMode=PickingMode.Ignore;ring.style.position=Position.Absolute;ring.style.left=ring.style.top=ring.style.right=ring.style.bottom=0;
            ring.style.borderTopLeftRadius=ring.style.borderTopRightRadius=ring.style.borderBottomLeftRadius=ring.style.borderBottomRightRadius=radius;box.Add(ring);
            return state=>
            {
                float all=state=="rest"?0:2;Color edge=state=="focus"?C("var(--a1)"):state=="bad"?C("#FF5E6C"):C("var(--bd)");
                ring.style.borderLeftWidth=ring.style.borderRightWidth=ring.style.borderBottomWidth=all;ring.style.borderTopWidth=2;
                ring.style.borderLeftColor=ring.style.borderRightColor=ring.style.borderBottomColor=edge;ring.style.borderTopColor=state=="rest"?new Color(0,0,0,.3f):edge;
            };
        }
        public void Decorate(VisualElement element,string background,int radius=0,int shadow=0,string shadowColor=null)
        {
            // border-radius: 999px — CSS shrinks oversized radii together, so a pill keeps half-circle ends; UI Toolkit clamps
            // each axis on its own and draws an ellipse. Pill radii follow half the shorter side.
            float applied=radius;
            if(corners.TryGetValue(element,out var held))held.Radius=radius;
            else if(radius>=999)
            {
                var pill=new Corners{Radius=radius};corners.Add(element,pill);
                element.RegisterCallback<GeometryChangedEvent>(evt=>
                {
                    if(pill.Radius<999)return;float r=Mathf.Min(evt.newRect.width,evt.newRect.height)/2;if(r<=0)return;
                    element.style.borderTopLeftRadius=element.style.borderTopRightRadius=element.style.borderBottomLeftRadius=element.style.borderBottomRightRadius=r;
                });
            }
            if(radius>=999&&element.layout.width>0&&element.layout.height>0)applied=Mathf.Min(element.layout.width,element.layout.height)/2;
            element.style.borderTopLeftRadius=element.style.borderTopRightRadius=element.style.borderBottomLeftRadius=element.style.borderBottomRightRadius=applied;
            if(!string.IsNullOrEmpty(background)&&background.Contains("gradient"))
            {
                if(!gradientPaints.TryGetValue(element,out var paint))
                {
                    paint=new GradientPaint();gradientPaints.Add(element,paint);var binding=paint;
                    element.RegisterCallback<GeometryChangedEvent>(evt=>{if(evt.newRect.size!=evt.oldRect.size&&binding.Css!=null)PaintGradient(element,binding.Css);});
                }
                paint.Css=background;PaintGradient(element,background);
            }
            else{if(gradientPaints.TryGetValue(element,out var paint))paint.Css=null;element.style.backgroundImage=StyleKeyword.None;element.style.backgroundColor=C(background);}
            GalaxyFilters.HardShadow(element,shadow,C(shadowColor??"var(--pnS)"));
        }
        private void PaintGradient(VisualElement element,string css)
        {
            if(disposed)return;
            float width=element.layout.width,height=element.layout.height;
            var angle=Regex.Match(css,@"^linear-gradient\(\s*([-\d.]+)deg");
            bool oblique=angle.Success&&!Mathf.Approximately(Mathf.Repeat(float.Parse(angle.Groups[1].Value,CultureInfo.InvariantCulture),90),0);
            bool aspectMatters=oblique||css.StartsWith("radial",StringComparison.Ordinal)&&css.Contains("circle");
            float aspect=aspectMatters&&width>0&&height>0?Mathf.Round(width/height*10000)/10000:1;
            string key=Model.Planet+":"+css+":"+aspect.ToString("F4",CultureInfo.InvariantCulture);
            if(!paints.TryGetValue(key,out var texture)){texture=new GalaxySurface(this).CreateGradient(css,Mathf.Max(.0001f,aspect),1);paints.Add(key,texture);}
            element.style.backgroundColor=Color.clear;element.style.backgroundImage=new StyleBackground(texture);
        }
        /// <summary>
        /// A framed avatar as G101-Menu draws it (frameOf / frameOpts): Klasik is the conic a1/dp face, a league frame
        /// (10–15, a saved 1 = Altın) the G101Ring with "box-shadow: 0 0 16px glow". The initials disc sits at the frame's
        /// inset — insetS 5/9 (62), inset 7/15 (110), insetM 8/14 (104), insetL 9/17 (124), chips inS 5/7 (40, 44) — inside
        /// a 3px (chips 2px) ring in the gap colour (top bar: gapS). Chips carry no initials and no glow.
        /// </summary>
        public VisualElement Avatar(int size,JObject data=null,bool level=false,bool topBar=false)
        {
            data=data??Model.Profile;var root=Box("avatar");root.style.width=root.style.height=size;root.style.flexShrink=0;
            int frame=(int?)data["frame"]??0;var look=FrameLook(frame,topBar);bool chip=size<=44,league=look.League!=null;
            if(league&&!chip)
            {
                // A transparent face casts its CSS box-shadow outside the circle only: an opaque band under the ring art
                // (which covers it) casts the glow instead.
                var glow=Box("avatar-frame-glow");glow.pickingMode=PickingMode.Ignore;Place(glow,0,0,size,size);
                glow.style.borderTopLeftRadius=glow.style.borderTopRightRadius=glow.style.borderBottomLeftRadius=glow.style.borderBottomRightRadius=size/2f;
                glow.style.borderLeftWidth=glow.style.borderRightWidth=glow.style.borderTopWidth=glow.style.borderBottomWidth=Mathf.Min(12,size/4f);
                glow.style.borderLeftColor=glow.style.borderRightColor=glow.style.borderTopColor=glow.style.borderBottomColor=C(look.Gap);
                GalaxyFilters.Shadow(glow,0,0,8,look.Glow);root.Add(glow);
            }
            var face=Box("avatar-frame");face.pickingMode=PickingMode.Ignore;Place(face,0,0,size,size);root.Add(face);
            if(league)root.Add(LeagueRing(look.League,size));else Decorate(face,look.Face,size/2);
            int inset=chip?(league?7:5):size>=124?(league?17:9):size==104?(league?14:8):size<=62?(league?9:5):(league?15:7);
            var inner=Box("avatar-inner");inner.pickingMode=PickingMode.Ignore;inner.style.position=Position.Absolute;
            inner.style.left=inner.style.right=inner.style.top=inner.style.bottom=inset;Decorate(inner,(string)data["color"]??"#FF8A5B",size/2);
            inner.style.borderLeftWidth=inner.style.borderRightWidth=inner.style.borderTopWidth=inner.style.borderBottomWidth=chip?2:3;
            inner.style.borderLeftColor=inner.style.borderRightColor=inner.style.borderTopColor=inner.style.borderBottomColor=C(look.Gap);
            if(!chip){var initials=Text(ProfileValidation.Initials((string)data["name"]??""),Mathf.RoundToInt(size*.325f),true,"#fff");initials.name="avatar-initials";inner.Add(initials);}
            root.Add(inner);
            if(level)
            {
                // SV badge: 900 10px, padding 1px 7px, radius 7, bottom -7 (62); 11px, 2px 9px, 8, -7 (104, 110); 12px, 2px 10px, 8, -8 (124).
                bool large=size>=124;int font=size<=62?10:large?12:11;
                // The top bar's badge is literal markup ("SV 24") in G101-Menu, the same in English (EKSIKLER EK-611).
                var badge=Text(large?L("SEVİYE "+Level24,"LEVEL "+Level24):topBar?"SV "+Level24:L("SV "+Level24,"LV "+Level24),font,false,"var(--aT)");badge.AddToClassList("avatar-level");badge.style.unityFont=Font("Baloo2-800");
                badge.style.paddingTop=badge.style.paddingBottom=size<=62?1:2;badge.style.paddingLeft=badge.style.paddingRight=size<=62?7:large?10:9;badge.style.bottom=large?-8:-7;
                Decorate(badge,"var(--a1)",size<=62?7:8);root.Add(badge);
            }
            return root;
        }
        private int Level24=>Model.Level;
        public VisualElement Icon(string name,int size=24,string color=null)
        {var icon=new GalaxyIcon(name,C(color??"var(--ic)"));icon.style.width=icon.style.height=size;icon.style.flexShrink=0;return icon;}
        public VisualElement Tile(string code,int width=46,int height=62)
        {
            if(code=="|"){var gap=Box();gap.style.width=22;return gap;}
            string[] parts=code.Split(':');string body=parts[0].Replace("*","");bool highlighted=parts[0].Contains("*");
            string tint=(string)Model.Content["tileColors"]?[body.Substring(0,1)]??"var(--t1)";
            var tile=Box("galaxy-tile");tile.style.width=width;tile.style.height=height;tile.style.flexShrink=0;
            int radius=Mathf.RoundToInt(width*9f/46);Decorate(tile,"linear-gradient(var(--tf1),var(--tf2))",radius,Mathf.Max(3,width/15),"var(--te2)");
            tile.style.paddingTop=height*6f/62;
            var edge=Box();edge.pickingMode=PickingMode.Ignore;edge.style.position=Position.Absolute;edge.style.left=edge.style.right=edge.style.bottom=0;edge.style.height=height*5f/62;edge.style.backgroundColor=C("var(--te)");edge.style.borderBottomLeftRadius=edge.style.borderBottomRightRadius=radius;tile.Add(edge);
            var number=Text(body.Substring(1),Mathf.RoundToInt(width*.61f),true,tint);number.style.height=width*28f/46;number.style.flexShrink=0;number.style.unityTextAlign=TextAnchor.MiddleCenter;tile.Add(number);
            var dot=Box();dot.style.width=dot.style.height=width*.175f;dot.style.marginTop=3;Decorate(dot,tint,20);tile.Add(dot);
            if(highlighted)
            {
                var ring=Box();ring.pickingMode=PickingMode.Ignore;ring.style.position=Position.Absolute;ring.style.left=ring.style.right=ring.style.top=ring.style.bottom=-3;Decorate(ring,"transparent",radius+3);
                ring.style.borderTopWidth=ring.style.borderLeftWidth=ring.style.borderRightWidth=ring.style.borderBottomWidth=3;ring.style.borderTopColor=ring.style.borderLeftColor=ring.style.borderRightColor=ring.style.borderBottomColor=C("var(--a1)");tile.Add(ring);GalaxyFilters.Shadow(tile,0,0,9,C("var(--gl)"));
            }
            if(parts.Length>1){string lang=Loc.Language;var tag=Text((string)Model.Content["ruleTileTags"]?[lang]?[parts[1]],11,false,"var(--a1)");tag.style.unityFont=Font("Baloo2-800");tag.AddToClassList("tile-tag");tile.Add(tag);}
            return tile;
        }
        public VisualElement Progress(float fraction,string color=null)
        {
            var track=Box("progress-track");Decorate(track,"var(--dp)",6);var fill=Box("progress-fill");fill.style.width=Length.Percent(Mathf.Clamp01(fraction)*100);Decorate(fill,color??"linear-gradient(90deg,var(--a2),var(--a1))",6);track.Add(fill);return track;
        }
        /// <summary>
        /// overflow-y: auto with a thin scrollbar. <paramref name="stableGutter"/>: "scrollbar-gutter: stable" — the gutter
        /// stays reserved and the thumb shows only when the content overflows; without it the bar takes room only then.
        /// </summary>
        public ScrollView Scroll(bool stableGutter=true)
        {
            var scroll=new ScrollView(ScrollViewMode.Vertical){verticalScrollerVisibility=stableGutter?ScrollerVisibility.AlwaysVisible:ScrollerVisibility.Auto,horizontalScrollerVisibility=ScrollerVisibility.Hidden};scroll.AddToClassList("galaxy-scroll");
            if(!stableGutter)return scroll;
            void Sync(GeometryChangedEvent _)=>scroll.verticalScroller.style.opacity=scroll.contentContainer.layout.height>scroll.contentViewport.layout.height+.5f?1:0;
            scroll.contentContainer.RegisterCallback<GeometryChangedEvent>(Sync);scroll.contentViewport.RegisterCallback<GeometryChangedEvent>(Sync);return scroll;
        }
        public int Tab(string panel)=>tabs.TryGetValue(panel,out int index)?index:0;
        public void SetTab(string panel,int index){tabs[panel]=index;Refresh();}
        public void PreviewSettings(JObject draft){preview=draft;UpdateBackground();}
        public void SetFocused(bool value){focused=value;}
        public void AnimateElement(Action<float> action)
        {
            float began=buildingMotions==dialogMotions?dialogAnimationOrigin:buildingMotions==panelMotions?panelAnimationOrigin:0;
            Animate(time=>action(GameClock.Frozen?time:time-began)); // frozen: each animation's own time is the canonical instant
        }
        private void UpdateBackground(){var values=preview??Model.Settings;BackgroundChanged?.Invoke(Model.Planet,(bool?)values["motion"]??true,(string)values["quality"]??"Yüksek");}
        public void Copy(string value)=>Services.Share.CopyToClipboard(value);
        /// <summary>
        /// The game shell's flash(text, kind): a pill at the top centre (Baloo 800 15 px, a dark disc with a diamond in the
        /// text colour), toastIn .35s, gone after 3.4 s. Kinds: ok, gold, bad, info.
        /// </summary>
        public void Flash(string text,string kind="info")
        {
            if(toastLayer==null)return;
            foreach(var old in toastLayer.Query<VisualElement>("shell-flash").ToList())old.RemoveFromHierarchy();
            (string bg,string fg)=kind=="ok"?("#3CCB6E","#062B14"):kind=="bad"?("linear-gradient(#FF7A86,#E8455A)","#fff"):kind=="gold"?("linear-gradient(var(--a1),var(--a2))","var(--aT)"):("rgba(5,8,22,.85)","#F4F1E8");
            var pill=new VisualElement{name="shell-flash",pickingMode=PickingMode.Ignore};pill.style.position=Position.Absolute;pill.style.top=22;pill.style.left=640;
            pill.style.flexDirection=FlexDirection.Row;pill.style.alignItems=Align.Center;pill.style.paddingTop=pill.style.paddingBottom=10;pill.style.paddingLeft=10;pill.style.paddingRight=20;
            Decorate(pill,bg,999);GalaxyFilters.Shadow(pill,0,10,15,new Color(0,0,0,.4f));
            var disc=new VisualElement{pickingMode=PickingMode.Ignore};disc.style.width=disc.style.height=26;disc.style.marginRight=10;disc.style.alignItems=Align.Center;disc.style.justifyContent=Justify.Center;Decorate(disc,"rgba(0,0,0,.18)",13);
            var diamond=new VisualElement{pickingMode=PickingMode.Ignore};diamond.style.width=diamond.style.height=9;diamond.style.rotate=new Rotate(45);Decorate(diamond,fg,2);disc.Add(diamond);pill.Add(disc);
            var label=Text(text,15,false,fg);label.style.unityFont=Font("Baloo2-800");label.style.whiteSpace=WhiteSpace.NoWrap;label.style.paddingLeft=label.style.paddingRight=label.style.paddingTop=label.style.paddingBottom=0;label.style.marginLeft=label.style.marginRight=0;pill.Add(label);
            toastLayer.Add(pill);
            float began=UnityEngine.Time.realtimeSinceStartup;IVisualElementScheduledItem job=null;
            // toastIn: translate(-50%,-14px) → translate(-50%,0), opacity 0 → 1 (ease).
            job=pill.schedule.Execute(()=>
            {
                float f=UnityEngine.Mathf.Clamp01((UnityEngine.Time.realtimeSinceStartup-began)/.35f),k=CssBezier(.25f,.1f,.25f,1,f);
                pill.style.opacity=k;pill.style.translate=new Translate(Length.Percent(-50),-14*(1-k));if(f>=1)job.Pause();
            }).Every(0);
            pill.style.opacity=0;pill.style.translate=new Translate(Length.Percent(-50),-14);
            pill.schedule.Execute(()=>pill.RemoveFromHierarchy()).ExecuteLater(3400);
            LastFlash=text;
        }
        /// <summary>The last flash's text (tests).</summary>
        public string LastFlash{get;private set;}
        public void Toast(string text)
        {
            if(toastLayer==null)return;toastLayer.Clear();var toast=Text(text,14,true,"#fff");toast.AddToClassList("toast");Decorate(toast,"#3CCB6E",12,4,"#22914A");toastLayer.Add(toast);
            toast.schedule.Execute(()=>toast.RemoveFromHierarchy()).ExecuteLater(1800);
        }
        public void Refresh()
        {
            if(disposed||rebuilding)return;
            if(Root.panel==null){Rebuild();return;}
            if(refreshJob!=null)return;
            refreshJob=Root.schedule.Execute(()=>
            {
                if(Root.Query<GalaxySlider>().ToList().Any(slider=>slider.IsDragging))return;
                refreshJob.Pause();refreshJob=null;Rebuild();
            }).Every(16);
        }
        public void OpenPanel(string key,int? tab=null)
        {
            if(Modals.Panel=="settings"&&key!="settings")settings.Discard();
            if(Modals.Panel==null)focusBeforePanel=FocusIdentity(Root.panel?.focusController?.focusedElement as VisualElement);
            bool newlyOpened=Modals.Panel!=key;Modals.OpenPanel(key);if(tab.HasValue)tabs[key]=tab.Value;
            if(newlyOpened)Services.Online.Load(key); // signed in: the panel's data, fresh from the server
            if(key=="settings"&&newlyOpened)settings.OnOpen(this);Refresh();
        }
        public void OpenDialog(string key)
        {
            if(Modals.Dialog==null)focusBeforeDialog=FocusIdentity(Root.panel?.focusController?.focusedElement as VisualElement);
            if(key=="profile")edit.OnOpen(this);if(key=="add")addFriend.OnOpen(this);
            if(Modals.Dialog!=key)Services.Online.Load(key);
            if(key=="logout"||key=="delete"||key=="quit"){confirm.Kind=key;confirm.OnOpen();DoneFrom=key;}
            if(key=="bye")done.From=DoneFrom;
            Modals.OpenDialog(key);Refresh();
        }
        /// <summary>
        /// Oyundan çık (G101-Menu cf.go, isQuit): the farewell over the main menu (the settings close under it), then the
        /// game quits 1.1 s later (onQuit).
        /// </summary>
        public void QuitGame()
        {
            if(Modals.Panel=="settings"){settings.Discard();preview=null;}
            DoneFrom="quit";Modals.ClosePanel();OpenDialog("bye");
            Root.schedule.Execute(()=>
            {
#if UNITY_EDITOR
                Debug.Log("Galaxy 101: Oyundan çık (the editor keeps running).");
#else
                Application.Quit();
#endif
            }).ExecuteLater(1100);
        }
        public void CloseTop()
        {
            bool dialog=Modals.Dialog!=null;if(!dialog&&Modals.Panel=="settings"){settings.Discard();preview=null;}
            focusAfterClose=dialog?focusBeforeDialog:focusBeforePanel;
            AnimateClose(dialog?dialogLayer:panelLayer);Modals.CloseTop();Refresh();
        }
        private string FocusIdentity(VisualElement element)
        {
            var field=element as TextField??element?.GetFirstAncestorOfType<TextField>();if(field!=null)return field.name;
            if(element==null||element==Root)return null;
            if(!string.IsNullOrEmpty(element.name)&&!element.name.StartsWith("unity-",StringComparison.Ordinal))return element.name;
            var indexes=new List<int>();for(var item=element;item!=null&&item!=Root;item=item.parent){if(item.parent==null)return null;indexes.Add(item.parent.IndexOf(item));}
            indexes.Reverse();return "@"+string.Join("/",indexes);
        }
        private VisualElement ResolveFocus(string identity)
        {
            if(string.IsNullOrEmpty(identity))return null;if(identity[0]!='@')return Root.Q<VisualElement>(identity);
            var item=Root;foreach(string part in identity.Substring(1).Split('/')){if(!int.TryParse(part,out int index)||index<0||index>=item.childCount)return null;item=item[index];}return item;
        }
        private void OnSubmit(NavigationSubmitEvent evt)
        {
            if(Modals.Dialog!=null&&evt.target is VisualElement target&&!dialogLayer.Contains(target))evt.StopImmediatePropagation();
        }
        private void OnPointer(PointerDownEvent evt)=>Root.RemoveFromClassList("keyboard-navigation");
        private void OnKey(KeyDownEvent evt)
        {
            if(evt.keyCode==KeyCode.Tab||evt.keyCode==KeyCode.Return||evt.keyCode==KeyCode.Space||evt.keyCode==KeyCode.UpArrow||evt.keyCode==KeyCode.DownArrow||evt.keyCode==KeyCode.LeftArrow||evt.keyCode==KeyCode.RightArrow)Root.AddToClassList("keyboard-navigation");
            if(evt.keyCode==KeyCode.Tab&&(Modals.Dialog!=null||Modals.Panel!=null))
            {
                var scope=Modals.Dialog!=null?dialogLayer:panelLayer;
                var candidates=scope.Query<VisualElement>().ToList().Where(e=>e.focusable&&e.tabIndex>=0&&e.enabledInHierarchy&&IsVisible(e,scope)&&!e.ClassListContains("dialog-card")&&!e.ClassListContains("panel-card")).ToList();
                if(candidates.Count>0)
                {
                    var current=Root.panel?.focusController?.focusedElement as VisualElement;int index=candidates.IndexOf(current);
                    index=evt.shiftKey?(index<=0?candidates.Count-1:index-1):(index+1)%candidates.Count;candidates[index].Focus();
                }
                evt.StopImmediatePropagation();
            }
            if(evt.keyCode==KeyCode.Escape&&(Modals.Dialog!=null||Modals.Panel!=null)){CloseTop();evt.StopImmediatePropagation();}
        }
        private static bool IsVisible(VisualElement element,VisualElement scope)
        {
            for(var item=element;item!=null;item=item.parent)
            {if(item.resolvedStyle.display==DisplayStyle.None||item.resolvedStyle.visibility==Visibility.Hidden)return false;if(item==scope)return true;}
            return false;
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;Model.Changed-=Refresh;Loc.Changed-=Refresh;refreshJob?.Pause();
            Root.UnregisterCallback<KeyDownEvent>(OnKey,TrickleDown.TrickleDown);Root.UnregisterCallback<PointerDownEvent>(OnPointer,TrickleDown.TrickleDown);Root.UnregisterCallback<NavigationSubmitEvent>(OnSubmit,TrickleDown.TrickleDown);
            animationJob?.Pause();
            foreach(var texture in paints.Values)if(texture!=null){if(UnityEngine.Application.isPlaying)UnityEngine.Object.Destroy(texture);else UnityEngine.Object.DestroyImmediate(texture);}paints.Clear();motions.Clear();panelMotions.Clear();dialogMotions.Clear();
        }
    }
}
