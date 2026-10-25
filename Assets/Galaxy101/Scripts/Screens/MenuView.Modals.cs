using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    public sealed partial class MenuView
    {
        private void AnimateClose(VisualElement layer)
        {
            if(layer?.panel==null||layer.childCount==0)return;
            var ghost=Box("fill");ghost.pickingMode=PickingMode.Ignore;layer.parent.Add(ghost);
            foreach(var item in layer.Children().ToArray())ghost.Add(item);
            foreach(var item in ghost.Query<VisualElement>().ToList()){item.pickingMode=PickingMode.Ignore;item.focusable=false;item.tabIndex=-1;}ghost.SetEnabled(false);ghost.AddToClassList("closing-ghost");
            double began=Time.realtimeSinceStartupAsDouble;IVisualElementScheduledItem close=null;
            close=ghost.schedule.Execute(()=>
            {
                float t=Mathf.Clamp01((float)(Time.realtimeSinceStartupAsDouble-began)/.14f);ghost.style.opacity=1-t;
                if(t>=1){close.Pause();ghost.RemoveFromHierarchy();}
            }).Every(0);
        }
        private void BuildPanel()
        {
            string key=Modals.Panel;
            panelLayer.UnregisterCallback<PointerDownEvent>(BlockUnderlying,TrickleDown.TrickleDown);panelLayer.UnregisterCallback<ClickEvent>(BlockUnderlyingClick,TrickleDown.TrickleDown);
            if(Modals.Dialog!=null){panelLayer.RegisterCallback<PointerDownEvent>(BlockUnderlying,TrickleDown.TrickleDown);panelLayer.RegisterCallback<ClickEvent>(BlockUnderlyingClick,TrickleDown.TrickleDown);}
            if(key=="settings"){AddModal(panelLayer,settings.Build(this),900,560,false);return;}
            var card=Box("panel-card");card.name="panel-"+key;card.style.width=980;card.style.height=610;
            var header=Row();header.AddToClassList("panel-header");var meta=Model.Content["panels"]?[key]??throw new InvalidOperationException("Missing supplied panel: "+key);
            var icon=Box("panel-icon");Decorate(icon,(string)meta["tile"],16,4,(string)meta["sh"]);icon.Add(Icon(key=="me"?"profile":key,28,key=="shop"?"aT":"#fff"));header.Add(icon);
            var pair=meta[Loc.Language=="en"?"en":"tr"];var names=Column(Text((string)pair[0],26,true),Text((string)pair[1],13,false,"mt"));names.style.minWidth=0;names.style.marginLeft=14;header.Add(names);
            var spacer=Box();spacer.style.flexGrow=1;header.Add(spacer);
            string[] labels=key=="shop"?shop.Tabs(this):key=="friends"?friends.Tabs(this):key=="leader"?leader.Tabs(this):key=="me"?profile.Tabs(this):Array.Empty<string>();
            if(labels.Length>0)
            {
                var segment=Box("panel-tabs");Decorate(segment,"dp",14);
                for(int i=0;i<labels.Length;i++)
                {int index=i;var button=Button(labels[i],()=>SetTab(key,index),"plain tab-button");button.name=key+"-tab-"+i;if(i==labels.Length-1)button.style.marginRight=0;button.style.color=C(i==Tab(key)?"aT":"mt");if(i==Tab(key))Decorate(button,"linear-gradient(var(--a1),var(--a2))",10,3,"aS");segment.Add(button);}
                header.Add(segment);
            }
            var close=Button("",CloseTop,"secondary modal-close");close.name="panel-close";close.tooltip=L("Kapat","Close");close.Add(Icon("close",18));header.Add(close);card.Add(header);
            var body=Box("panel-body");body.name="panel-body";
            VisualElement contents=key=="shop"?shop.Build(this):key=="friends"?friends.Build(this):key=="leader"?leader.Build(this):key=="daily"?daily.Build(this):key=="rules"?rules.Build(this):profile.Build(this);
            contents.style.flexGrow=1;contents.style.minHeight=0;body.Add(contents);card.Add(body);AddModal(panelLayer,card,980,610,false);
        }
        private void BlockUnderlying(PointerDownEvent evt){if(Modals.Dialog!=null)evt.StopImmediatePropagation();}
        private void BlockUnderlyingClick(ClickEvent evt){if(Modals.Dialog!=null)evt.StopImmediatePropagation();}
        private void BuildDialog()
        {
            string key=Modals.Dialog;VisualElement content;
            switch(key)
            {
                case "profile":content=edit.Build(this);break;
                case "add":content=addFriend.Build(this);break;
                case "share":content=share.Build(this);break;
                case "bye":done.From=DoneFrom;content=done.Build(this);break;
                default:confirm.Kind=key;content=confirm.Build(this);break;
            }
            int width=key=="profile"?720:key=="add"?560:key=="share"?640:key=="bye"?420:480;
            AddModal(dialogLayer,content,width,0,true);
        }
        /// <summary>The card's content moved into one page element, so a refresh can replace it as a whole.</summary>
        private static VisualElement Page(VisualElement card)
        {
            // The page lays its children out as the card would have (some cards are rows).
            var page=new VisualElement{name="modal-page",pickingMode=PickingMode.Ignore};page.style.flexGrow=1;page.style.minHeight=0;page.style.minWidth=0;
            page.style.flexDirection=card.style.flexDirection.keyword==StyleKeyword.Undefined?card.style.flexDirection:FlexDirection.Column;
            if(card.style.alignItems.keyword==StyleKeyword.Undefined)page.style.alignItems=card.style.alignItems;
            if(card.style.justifyContent.keyword==StyleKeyword.Undefined)page.style.justifyContent=card.style.justifyContent;
            if(card.style.flexWrap.keyword==StyleKeyword.Undefined)page.style.flexWrap=card.style.flexWrap;
            if(card.ClassListContains("g-settings"))page.style.flexDirection=FlexDirection.Row; // Panels.uss .g-settings
            if(card.ClassListContains("row")||card.ClassListContains("g-panel-row")){page.style.flexDirection=FlexDirection.Row;page.style.alignItems=Align.Center;}
            foreach(var child in card.Children().ToArray())page.Add(child);
            return page;
        }
        /// <summary>
        /// The card's rounded clip lives on its page: the card itself stays unclipped so its ledge (0 10px 0 --pnS),
        /// drawn below its box, is not cut away.
        /// </summary>
        private static void Clip(VisualElement page,float radius)
        {
            if(page==null)return;page.style.overflow=Overflow.Hidden;
            page.style.borderTopLeftRadius=page.style.borderTopRightRadius=page.style.borderBottomLeftRadius=page.style.borderBottomRightRadius=Mathf.Max(0,radius);
        }
        /// <summary>
        /// A refreshed modal: the new page replaces the old one in place and, within the same tab, takes over its scroll
        /// offsets — no jump to the top. (Old and new are never on screen together: overlapping filtered subtrees trip
        /// UI Toolkit's render-tree compositor.)
        /// </summary>
        private static void SwapPage(VisualElement card,VisualElement page,bool keepScroll)
        {
            var old=card.Children().FirstOrDefault(c=>c.name=="modal-page");
            card.Insert(card.IndexOf(old)>=0?card.IndexOf(old):card.childCount,page);Clip(page,card.style.borderTopLeftRadius.value.value-1);
            if(old==null)return;
            var offsets=keepScroll?old.Query<ScrollView>().ToList().Select(v=>v.scrollOffset).ToList():new System.Collections.Generic.List<Vector2>();
            old.RemoveFromHierarchy();
            var scrolls=page.Query<ScrollView>().ToList();
            for(int i=0;i<scrolls.Count&&i<offsets.Count;i++)
            {
                var view=scrolls[i];var offset=offsets[i];view.scrollOffset=offset;
                EventCallback<GeometryChangedEvent> restore=null;restore=_=>{view.scrollOffset=offset;view.contentContainer.UnregisterCallback(restore);};view.contentContainer.RegisterCallback(restore);
            }
        }
        private void AddModal(VisualElement layer,VisualElement card,int width,int height,bool nested)
        {
            string key=nested?Modals.Dialog:Modals.Panel;
            var mounted=nested?dialogCard:panelCard;
            if(mounted!=null&&mounted.parent!=null&&(nested?mountedDialog:mountedPanel)==key)
            {
                // Scroll positions carry over only within the same tab; a new tab starts at the top.
                string view=key+"|"+Tab(key);bool sameView=mounted.userData as string==view;mounted.userData=view;
                SwapPage(mounted,Page(card),sameView);
                return;
            }
            layer.Clear();
            var dim=Box("modal-backdrop");dim.name=nested?"dialog-backdrop":"panel-backdrop";Decorate(dim,nested?"rgba(0,0,0,.5)":"rgba(0,0,0,.55)");GalaxyFilters.BackdropBlur(dim,nested?4:6); // menu.closePanel / closeDlg: backdrop-filter blur(6px) / blur(4px)
            dim.RegisterCallback<PointerDownEvent>(evt=>{if(evt.target!=dim)return;CloseTop();evt.StopImmediatePropagation();});layer.Add(dim);
            var center=Box("modal-center");center.pickingMode=PickingMode.Ignore;center.style.width=Length.Percent(100);center.style.height=Length.Percent(100);layer.Add(center);
            card.Add(Page(card));card.userData=key+"|"+Tab(key);
            card.style.width=width;if(height>0)card.style.height=height;card.style.flexShrink=0;card.AddToClassList(nested?"dialog-card":"panel-card");
            Decorate(card,"pn",nested?26:28,10,"pnS");GalaxyFilters.Shadow(card,0,40,45,new Color(0,0,0,.6f));Clip(card.Q("modal-page"),nested?25:27);card.focusable=true;card.pickingMode=PickingMode.Position;center.Add(card);
            if(nested){dialogCard=card;mountedDialog=key;}else{panelCard=card;mountedPanel=key;}
            // fadeIn .2s (dialogs .18s) ease-out on the whole overlay, under the card's own modalIn.
            float fade=nested?.18f:.2f;double fadeFrom=Time.realtimeSinceStartupAsDouble;layer.style.opacity=0;
            IVisualElementScheduledItem fader=null;
            fader=layer.schedule.Execute(()=>
            {
                float f=Mathf.Clamp01((float)(Time.realtimeSinceStartupAsDouble-fadeFrom)/fade);layer.style.opacity=CssBezier(0,0,.58f,1,f);
                if(f>=1){layer.style.opacity=StyleKeyword.Null;fader.Pause();}
            }).Every(16);
            double began=Time.realtimeSinceStartupAsDouble;float duration=nested?.26f:.28f;
            card.style.opacity=0;card.style.scale=new Scale(new Vector3(.96f,.96f,1));card.style.translate=new Translate(0,14);
            IVisualElementScheduledItem job=null;
            job=card.schedule.Execute(()=>
            {
                float f=Mathf.Clamp01((float)(Time.realtimeSinceStartupAsDouble-began)/duration);float ease=ModalProgress(f);
                card.style.opacity=Mathf.Clamp01(ease);card.style.scale=new Scale(new Vector3(.96f+.04f*ease,.96f+.04f*ease,1));card.style.translate=new Translate(0,14*(1-ease));
                if(f>=1)job.Pause();
            }).Every(0);
            card.schedule.Execute(()=>
            {
                if(nested&&Modals.Dialog!=key||!nested&&(Modals.Panel!=key||Modals.Dialog!=null))return;
                var first=card.Query<VisualElement>().ToList().FirstOrDefault(e=>e!=card&&e.focusable&&e.enabledInHierarchy&&e.resolvedStyle.display!=DisplayStyle.None);(first??card).Focus();
            }).ExecuteLater(1);
        }
        private static float ModalProgress(float fraction)
        {
            if(fraction<=0)return 0;if(fraction>=1)return 1;
            // CSS cubic-bezier(.2,1.2,.4,1): solve x first, preserving the y overshoot.
            float lo=0,hi=1,t=.5f;
            for(int i=0;i<18;i++)
            {
                t=(lo+hi)*.5f;float q=1-t;
                float x=3*q*q*t*.2f+3*q*t*t*.4f+t*t*t;
                if(x<fraction)lo=t;else hi=t;
            }
            return 3*(1-t)*(1-t)*t*1.2f+3*(1-t)*t*t+t*t*t;
        }
    }
}
