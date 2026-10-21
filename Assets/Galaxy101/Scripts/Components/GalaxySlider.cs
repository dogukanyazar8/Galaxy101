using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    public sealed class GalaxySlider : VisualElement
    {
        private readonly VisualElement track,fill,knob;
        private readonly int minimum,maximum;
        private int value;
        private int captured=-1;
        private float downX;
        public bool IsDragging=>captured>=0;
        public int Value {get=>value;set=>SetValue(value,true);}
        public event Action<int> ValueChanged;
        public static int MapValue(float pointer,float left,float width,int minimum,int maximum)
        {
            if(width<=0||minimum>=maximum)throw new ArgumentOutOfRangeException(nameof(width));
            return minimum+(int)Math.Floor(Mathf.Clamp01((pointer-left)/width)*(maximum-minimum)+.5f);
        }
        public GalaxySlider(int minimum,int maximum,int initial,MenuView view)
        {
            if(minimum>=maximum)throw new ArgumentOutOfRangeException(nameof(maximum));
            this.minimum=minimum;this.maximum=maximum;focusable=true;tabIndex=0;
            AddToClassList("galaxy-slider");
            track=view.Box("slider-track");fill=view.Box("slider-fill");knob=view.Box("slider-knob");
            view.Decorate(track,"var(--dp)",6);view.Decorate(fill,"linear-gradient(90deg,var(--a2),var(--a1))",6);
            view.Decorate(knob,"radial-gradient(circle at 50% 35%,#fff,#E8EAF2)",13,2,"rgba(0,0,0,.25)");
            var dot=view.Box("slider-dot");view.Decorate(dot,"linear-gradient(var(--a1),var(--a2))",4);knob.Add(dot);
            var halo=view.Box("slider-halo");halo.pickingMode=PickingMode.Ignore;halo.style.borderTopColor=halo.style.borderBottomColor=halo.style.borderLeftColor=halo.style.borderRightColor=view.C("gl");knob.Insert(0,halo);
            track.Add(fill);track.Add(knob);Add(track);SetValue(initial,false);
            RegisterCallback<PointerDownEvent>(evt=>{if(evt.button!=0)return;captured=evt.pointerId;downX=evt.position.x;RemoveFromClassList("dragging");this.CapturePointer(captured);Focus();knob.AddToClassList("dragging");Pick(evt.position.x);evt.StopPropagation();});
            RegisterCallback<PointerMoveEvent>(evt=>{if(captured!=evt.pointerId||!this.HasPointerCapture(evt.pointerId))return;if(Mathf.Abs(evt.position.x-downX)>1)AddToClassList("dragging");Pick(evt.position.x);evt.StopPropagation();});
            RegisterCallback<PointerUpEvent>(evt=>Release(evt.pointerId));
            RegisterCallback<PointerCancelEvent>(evt=>Release(evt.pointerId));
            RegisterCallback<PointerCaptureOutEvent>(_=>{captured=-1;RemoveFromClassList("dragging");knob.RemoveFromClassList("dragging");});
            RegisterCallback<KeyDownEvent>(evt=>
            {
                int step=evt.keyCode==KeyCode.LeftArrow||evt.keyCode==KeyCode.DownArrow?-1:evt.keyCode==KeyCode.RightArrow||evt.keyCode==KeyCode.UpArrow?1:0;
                if(step==0)return;Value+=step;evt.StopImmediatePropagation();
            });
        }
        public void SetValueWithoutNotify(int next)=>SetValue(next,false);
        private void Pick(float pointer)=>SetValue(MapValue(pointer,track.worldBound.x,track.worldBound.width,minimum,maximum),true);
        private void Release(int pointer){if(captured!=pointer)return;if(this.HasPointerCapture(pointer))this.ReleasePointer(pointer);captured=-1;RemoveFromClassList("dragging");knob.RemoveFromClassList("dragging");}
        private void SetValue(int next,bool notify)
        {
            next=Mathf.Clamp(next,minimum,maximum);bool changed=next!=value;value=next;
            float pct=100f*(value-minimum)/(maximum-minimum);fill.style.width=Length.Percent(pct);knob.style.left=Length.Percent(pct);
            if(changed&&notify)ValueChanged?.Invoke(value);
        }
    }
}
