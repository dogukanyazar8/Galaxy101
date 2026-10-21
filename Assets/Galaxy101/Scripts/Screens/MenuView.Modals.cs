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
            }).Every(16);
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
        private void AddModal(VisualElement layer,VisualElement card,int width,int height,bool nested)
        {
            string key=nested?Modals.Dialog:Modals.Panel;
            var mounted=nested?dialogCard:panelCard;
            if(mounted!=null&&mounted.parent!=null&&(nested?mountedDialog:mountedPanel)==key)
            {
                mounted.Clear();foreach(var child in card.Children().ToArray())mounted.Add(child);
                Decorate(mounted,"pn",nested?26:28,10,"pnS");GalaxyFilters.Shadow(mounted,0,40,45,new Color(0,0,0,.6f));
                return;
            }
            layer.Clear();
            var dim=Box("modal-backdrop");dim.name=nested?"dialog-backdrop":"panel-backdrop";Decorate(dim,nested?"rgba(0,0,0,.5)":"rgba(0,0,0,.55)");
            dim.RegisterCallback<PointerDownEvent>(evt=>{if(evt.target!=dim)return;CloseTop();evt.StopImmediatePropagation();});layer.Add(dim);
            var center=Box("modal-center");center.pickingMode=PickingMode.Ignore;center.style.width=Length.Percent(100);center.style.height=Length.Percent(100);layer.Add(center);
            card.style.width=width;if(height>0)card.style.height=height;card.style.flexShrink=0;card.AddToClassList(nested?"dialog-card":"panel-card");
            Decorate(card,"pn",nested?26:28,10,"pnS");GalaxyFilters.Shadow(card,0,40,45,new Color(0,0,0,.6f));card.style.overflow=Overflow.Hidden;card.focusable=true;card.pickingMode=PickingMode.Position;center.Add(card);
            if(nested){dialogCard=card;mountedDialog=key;}else{panelCard=card;mountedPanel=key;}
            double began=Time.realtimeSinceStartupAsDouble;float duration=nested?.26f:.28f;
            card.style.opacity=0;card.style.scale=new Scale(new Vector3(.96f,.96f,1));card.style.translate=new Translate(0,14);
            IVisualElementScheduledItem job=null;
            job=card.schedule.Execute(()=>
            {
                float f=Mathf.Clamp01((float)(Time.realtimeSinceStartupAsDouble-began)/duration);float ease=ModalProgress(f);
                card.style.opacity=Mathf.Clamp01(ease);card.style.scale=new Scale(new Vector3(.96f+.04f*ease,.96f+.04f*ease,1));card.style.translate=new Translate(0,14*(1-ease));
                if(f>=1)job.Pause();
            }).Every(16);
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
