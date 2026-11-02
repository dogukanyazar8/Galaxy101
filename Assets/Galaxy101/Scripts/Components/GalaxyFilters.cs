using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    public static class GalaxyFilters
    {
        public static void SaturationAndBrightness(VisualElement element,float saturation,float brightness)
        {
            var filters=element.style.filter.value==null?new List<FilterFunction>():new List<FilterFunction>(element.style.filter.value);
            filters.RemoveAll(f=>f.type==FilterFunctionType.Grayscale||f.type==FilterFunctionType.Tint);
            if(saturation<1)filters.Add(Function(FilterFunctionType.Grayscale,new FilterParameter(1-saturation)));
            if(brightness!=1)filters.Add(Function(FilterFunctionType.Tint,new FilterParameter(new Color(brightness,brightness,brightness,1))));
            Apply(element,filters);
        }
        private static void Apply(VisualElement element,List<FilterFunction> filters)=>element.style.filter=new StyleList<FilterFunction>(filters);
        private static FilterFunction Function(FilterFunctionType type,params FilterParameter[] parameters)
        {
            var function=new FilterFunction(type);
            for(int i=0;i<parameters.Length;i++){if(i<function.parameterCount)function.SetParameter(i,parameters[i]);else function.AddParameter(parameters[i]);}
            return function;
        }
        public static void Blur(VisualElement element,float sigma)
        {
            if(element==null)return;
            // Filter an explicit underlay, so display and owned-target rendering use
            // the same pixels instead of sampling a platform swapchain backdrop.
            Apply(element,sigma>0?new List<FilterFunction>{Function(FilterFunctionType.Blur,new FilterParameter(sigma))}:new List<FilterFunction>());
        }
        public static void BackdropBlur(VisualElement element,float sigma)
        {
            element.style.backdropFilter=new StyleList<FilterFunction>(new List<FilterFunction>{Function(FilterFunctionType.Blur,new FilterParameter(sigma))});
        }
        public static void Shadow(VisualElement element,float x,float y,float sigma,Color color)
        {
            var filters=element.style.filter.value==null?new List<FilterFunction>():new List<FilterFunction>(element.style.filter.value);
            filters.RemoveAll(f=>f.type==FilterFunctionType.DropShadow&&f.parameterCount>=3&&f.GetParameter(2).floatValue>0);
            filters.Add(Function(FilterFunctionType.DropShadow,new FilterParameter(x),new FilterParameter(y),new FilterParameter(sigma),new FilterParameter(color)));
            Apply(element,filters);
        }
        /// <summary>
        /// Grayscale, tint and a soft drop shadow that are always present, only their amounts changing: UI Toolkit's
        /// render-tree compositor asserts when an on-screen element's filter list changes shape (a shadow added or removed).
        /// </summary>
        public static void StableLook(VisualElement element,float saturation,float brightness,float x,float y,float sigma,Color shadow)
        {
            var filters=element.style.filter.value==null?new List<FilterFunction>():new List<FilterFunction>(element.style.filter.value);
            filters.RemoveAll(f=>f.type==FilterFunctionType.Grayscale||f.type==FilterFunctionType.Tint||f.type==FilterFunctionType.DropShadow&&f.parameterCount>=3&&f.GetParameter(2).floatValue>0);
            filters.Add(Function(FilterFunctionType.Grayscale,new FilterParameter(Mathf.Clamp01(1-saturation))));
            filters.Add(Function(FilterFunctionType.Tint,new FilterParameter(new Color(brightness,brightness,brightness,1))));
            filters.Add(Function(FilterFunctionType.DropShadow,new FilterParameter(x),new FilterParameter(y),new FilterParameter(sigma),new FilterParameter(shadow)));
            Apply(element,filters);
        }
        public static void ClearSoftShadow(VisualElement element)
        {
            var filters=element.style.filter.value==null?new List<FilterFunction>():new List<FilterFunction>(element.style.filter.value);
            filters.RemoveAll(f=>f.type==FilterFunctionType.DropShadow&&f.parameterCount>=3&&f.GetParameter(2).floatValue>0);Apply(element,filters);
        }
        /// <summary>
        /// CSS "box-shadow: 0 depth 0 color" — the ledge under raised buttons and cards: the face's lower outline and the
        /// same outline shifted down by <paramref name="depth"/>, filled as one anti-aliased vector band. It is drawn by a
        /// child that reaches below the box, so it moves, brightens and survives with the element's own filters (a filtered
        /// element renders offscreen to its subtree's bounds; content painted outside its own box is lost there). No
        /// layout, crisp at any scale.
        /// </summary>
        public static void HardShadow(VisualElement element,float depth,Color color)
        {
            if(!ledges.TryGetValue(element,out var ledge))
            {
                if(depth<=0)return;
                ledge=new Ledge(element);ledges.Add(element,ledge);
            }
            ledge.Set(Mathf.Max(0,depth),color);
        }
        /// <summary>The ledge depth <see cref="HardShadow"/> gave the element (0 when none).</summary>
        public static float LedgeDepth(VisualElement element)=>ledges.TryGetValue(element,out var ledge)?ledge.Depth:0;
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<VisualElement,Ledge> ledges=new System.Runtime.CompilerServices.ConditionalWeakTable<VisualElement,Ledge>();
        private sealed class Ledge:VisualElement
        {
            private readonly VisualElement owner;
            private IVisualElementScheduledItem follow;
            public float Depth;public Color Color;
            public Ledge(VisualElement owner)
            {
                this.owner=owner;name="ledge";pickingMode=PickingMode.Ignore;style.position=Position.Absolute;
                generateVisualContent+=Paint;
                owner.RegisterCallback<GeometryChangedEvent>(_=>Fit());
                if(!Beside())owner.Insert(0,this);
            }
            // Beside the owner instead of inside it: an owner that clips its children (overflow: hidden, set inline), or a
            // plain text element (a label stops measuring its text once it has a child, and would shrink to its padding).
            private bool Beside()=>owner.style.overflow.keyword==StyleKeyword.Undefined&&owner.style.overflow.value==Overflow.Hidden||owner is TextElement&&!(owner is GalaxyButton);
            public void Set(float depth,Color color)
            {
                if(Mathf.Approximately(Depth,depth)&&Color==color)return;
                Depth=depth;Color=color;style.display=depth>0?DisplayStyle.Flex:DisplayStyle.None;Fit();
            }
            // Normally a child reaching below the owner's border box; beside the owner (see Beside) it sits just under it as
            // a sibling and follows its transform, opacity and display.
            private void Fit()
            {
                float w=owner.layout.width,h=owner.layout.height;if(float.IsNaN(w))return;
                bool beside=Beside();if(beside&&owner.parent==null)return;
                if(!beside)
                {
                    if(parent!=owner)owner.Insert(0,this);
                    follow?.Pause();style.translate=StyleKeyword.Null;style.scale=StyleKeyword.Null;style.rotate=StyleKeyword.Null;style.opacity=StyleKeyword.Null;
                    var s=owner.resolvedStyle;style.left=-s.borderLeftWidth;style.top=-s.borderTopWidth;
                }
                else
                {
                    var host=owner.parent;if(parent!=host||host.IndexOf(this)!=host.IndexOf(owner)-1)host.Insert(host.IndexOf(owner),this);
                    style.left=owner.layout.x-host.resolvedStyle.borderLeftWidth;style.top=owner.layout.y-host.resolvedStyle.borderTopWidth;
                    if(follow==null)follow=schedule.Execute(Follow).Every(0);else follow.Resume();
                    Follow();
                }
                style.width=w;style.height=h+Depth;MarkDirtyRepaint();
            }
            private void Follow()
            {
                if(owner.parent!=parent){follow?.Pause();RemoveFromHierarchy();return;}
                var s=owner.resolvedStyle;
                style.translate=new Translate(s.translate.x,s.translate.y);style.scale=s.scale;style.rotate=s.rotate;
                style.transformOrigin=new TransformOrigin(owner.layout.width*.5f,owner.layout.height*.5f);
                style.opacity=s.opacity;style.visibility=s.visibility;
                style.display=Depth>0&&s.display==DisplayStyle.Flex?DisplayStyle.Flex:DisplayStyle.None;
            }
            private void Paint(MeshGenerationContext context)
            {
                float w=owner.layout.width,h=owner.layout.height,d=Depth;
                if(d<=0||Color.a<=0||float.IsNaN(w)||w<=0||h<=0)return;
                var style=owner.resolvedStyle;
                // CSS shrinks radii larger than half a side; the face's bottom corners.
                float left=Mathf.Min(style.borderBottomLeftRadius,w/2,h/2),right=Mathf.Min(style.borderBottomRightRadius,w/2,h/2);
                // The band reaches half a pixel under the face so its anti-aliased edge has no seam.
                const float tuck=.5f;
                var p=context.painter2D;p.fillColor=Color;p.BeginPath();
                p.MoveTo(new Vector2(0,h-left-tuck));
                if(left>0)p.Arc(new Vector2(left,h-left-tuck),left,180,90,ArcDirection.CounterClockwise);else p.LineTo(new Vector2(0,h-tuck));
                p.LineTo(new Vector2(w-right,h-tuck));
                if(right>0)p.Arc(new Vector2(w-right,h-right-tuck),right,90,0,ArcDirection.CounterClockwise);else p.LineTo(new Vector2(w,h-tuck));
                p.LineTo(new Vector2(w,h-right+d));
                if(right>0)p.Arc(new Vector2(w-right,h-right+d),right,0,90);else p.LineTo(new Vector2(w,h+d));
                p.LineTo(new Vector2(left,h+d));
                if(left>0)p.Arc(new Vector2(left,h-left+d),left,90,180);else p.LineTo(new Vector2(0,h+d));
                p.ClosePath();p.Fill();
            }
        }
        /// <summary>
        /// The panels' CSS "box-shadow: x y 2σ color" under a box whose outline is its opaque rounded border box (a
        /// <see cref="HardShadow"/> ledge included) — the pixels <see cref="Shadow"/> gives such a box, at a fraction of the
        /// cost. A drop-shadow filter renders the box's whole subtree offscreen at display resolution and blurs it there: a
        /// 90 px panel shadow takes ~5 ms of GPU a frame at 3840×2160. Here a plain rounded rectangle in the shadow colour,
        /// shrunk so its blur runs at <see cref="SoftShadow.TargetSigma"/> pixels, is blurred and scaled back up (UI Toolkit
        /// filters an element at its own size × pixels per point, before its transform). Drawn by a sibling just under the
        /// element that follows its layout, transform, opacity and display.
        /// </summary>
        public static void BoxShadow(VisualElement element,float x,float y,float sigma,Color color)
        {
            if(!boxShadows.TryGetValue(element,out var shadow)){shadow=new SoftShadow(element);boxShadows.Add(element,shadow);}
            shadow.Set(new Vector2(x,y),Mathf.Max(0,sigma),color);
        }
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<VisualElement,SoftShadow> boxShadows=new System.Runtime.CompilerServices.ConditionalWeakTable<VisualElement,SoftShadow>();
        private sealed class SoftShadow:VisualElement
        {
            // Blur radius (σ) in render-target pixels: the scaled-up Gaussian then stays within one 8-bit level of a full-size one.
            public const float TargetSigma=8;
            private readonly VisualElement owner,shape=new VisualElement{name="box-shadow-shape",pickingMode=PickingMode.Ignore};
            private IVisualElementScheduledItem follow;
            private Vector2 offset;private float sigma;private Color color;
            private Vector4 fitted=new Vector4(float.NaN,0,0,0);
            public SoftShadow(VisualElement owner)
            {
                this.owner=owner;name="box-shadow";pickingMode=PickingMode.Ignore;style.position=Position.Absolute;
                shape.style.position=Position.Absolute;shape.style.transformOrigin=new TransformOrigin(0,0);Add(shape);
                // Re-added where it was, the owner gets no new geometry.
                owner.RegisterCallback<GeometryChangedEvent>(_=>Fit());owner.RegisterCallback<AttachToPanelEvent>(_=>Fit());
            }
            public void Set(Vector2 offset,float sigma,Color color)
            {
                if(offset==this.offset&&Mathf.Approximately(sigma,this.sigma)&&color==this.color)return;
                this.offset=offset;this.sigma=sigma;this.color=color;fitted.x=float.NaN;Fit();
            }
            private void Fit()
            {
                var host=owner.parent;float w=owner.layout.width,h=owner.layout.height;if(host==null||float.IsNaN(w))return;
                // Under the owner and under a ledge drawn beside it (CSS paints the first of "0 10px 0 pnS, 0 40px 90px …" on top).
                int at=host.IndexOf(owner);while(at>0&&(host[at-1].name=="ledge"||host[at-1]==this))at--;
                if(parent!=host||host.IndexOf(this)!=at){RemoveFromHierarchy();at=host.IndexOf(owner);while(at>0&&host[at-1].name=="ledge")at--;host.Insert(at,this);}
                style.left=owner.layout.x-host.resolvedStyle.borderLeftWidth;style.top=owner.layout.y-host.resolvedStyle.borderTopWidth;
                style.width=w;style.height=h;
                if(follow==null)follow=schedule.Execute(Follow).Every(0);else follow.Resume();
                Follow();
            }
            // The owner's transform, opacity and display every frame; the shape again when its size, ledge or the display's
            // pixels per point change (a resolution switch rescales the canvas without a new layout).
            private void Follow()
            {
                if(owner.parent!=parent){follow?.Pause();RemoveFromHierarchy();return;}
                var s=owner.resolvedStyle;
                style.translate=new Translate(s.translate.x,s.translate.y);style.scale=s.scale;style.rotate=s.rotate;
                style.transformOrigin=new TransformOrigin(s.transformOrigin.x,s.transformOrigin.y,s.transformOrigin.z);
                style.opacity=s.opacity;style.visibility=s.visibility;
                style.display=sigma>0&&color.a>0&&s.display==DisplayStyle.Flex?DisplayStyle.Flex:DisplayStyle.None;
                float w=owner.layout.width,h=owner.layout.height+LedgeDepth(owner),ppp=owner.scaledPixelsPerPoint;
                var key=new Vector4(w,h,ppp,0);if(key==fitted||float.IsNaN(w))return;fitted=key;
                float k=Mathf.Max(1,sigma*ppp/TargetSigma),face=owner.layout.height;
                // CSS shrinks radii larger than half a side; a ledge only lengthens the outline's straight sides.
                float Radius(float r)=>Mathf.Min(r,w/2,face/2)/k;
                shape.style.left=offset.x;shape.style.top=offset.y;shape.style.width=w/k;shape.style.height=h/k;
                shape.style.borderTopLeftRadius=Radius(s.borderTopLeftRadius);shape.style.borderTopRightRadius=Radius(s.borderTopRightRadius);
                shape.style.borderBottomRightRadius=Radius(s.borderBottomRightRadius);shape.style.borderBottomLeftRadius=Radius(s.borderBottomLeftRadius);
                shape.style.backgroundColor=color;shape.style.scale=new Scale(new Vector2(k,k));
                GalaxyFilters.Blur(shape,sigma/k);
            }
        }
        /// <summary>
        /// CSS "box-shadow: 0 0 0 spread color" — a solid band around the element's rounded outline (pulse rings, focus
        /// halos), drawn like <see cref="HardShadow"/> by a child reaching <paramref name="reach"/> px around the box.
        /// Returns the setter (spread px up to the reach, colour).
        /// </summary>
        public static Action<float,Color> SpreadRing(VisualElement owner,float reach)
        {
            float spread=0;Color color=Color.clear;
            var ring=new VisualElement{name="spread-ring",pickingMode=PickingMode.Ignore};ring.style.position=Position.Absolute;
            void Fit()
            {
                if(ring.parent!=owner)owner.Insert(0,ring);
                var s=owner.resolvedStyle;float w=owner.layout.width,h=owner.layout.height;if(float.IsNaN(w))return;
                ring.style.left=-s.borderLeftWidth-reach;ring.style.top=-s.borderTopWidth-reach;ring.style.width=w+2*reach;ring.style.height=h+2*reach;ring.MarkDirtyRepaint();
            }
            owner.RegisterCallback<GeometryChangedEvent>(_=>Fit());owner.Insert(0,ring);
            ring.generateVisualContent+=context=>
            {
                float w=owner.layout.width,h=owner.layout.height;if(spread<=.01f||color.a<=.001f||float.IsNaN(w)||w<=0||h<=0)return;
                var style=owner.resolvedStyle;float cap=Mathf.Min(w,h)*.5f;
                float[] radii={Mathf.Min(style.borderTopLeftRadius,cap),Mathf.Min(style.borderTopRightRadius,cap),Mathf.Min(style.borderBottomRightRadius,cap),Mathf.Min(style.borderBottomLeftRadius,cap)};
                var p=context.painter2D;p.fillColor=color;p.BeginPath();
                RoundedRect(p,reach-spread,reach-spread,reach+w+spread,reach+h+spread,radii,spread);
                RoundedRect(p,reach,reach,reach+w,reach+h,radii,0);
                p.Fill(FillRule.OddEven);
            };
            return (s,c)=>{s=Mathf.Min(s,reach);if(Mathf.Approximately(s,spread)&&c==color)return;spread=s;color=c;ring.MarkDirtyRepaint();};
        }
        private static void RoundedRect(Painter2D p,float x0,float y0,float x1,float y1,float[] radii,float grow)
        {
            const float k=.5523f;float a=radii[0]+grow,b=radii[1]+grow,c=radii[2]+grow,d=radii[3]+grow;
            p.MoveTo(new Vector2(x0+a,y0));p.LineTo(new Vector2(x1-b,y0));p.BezierCurveTo(new Vector2(x1-b+k*b,y0),new Vector2(x1,y0+b-k*b),new Vector2(x1,y0+b));
            p.LineTo(new Vector2(x1,y1-c));p.BezierCurveTo(new Vector2(x1,y1-c+k*c),new Vector2(x1-c+k*c,y1),new Vector2(x1-c,y1));
            p.LineTo(new Vector2(x0+d,y1));p.BezierCurveTo(new Vector2(x0+d-k*d,y1),new Vector2(x0,y1-d+k*d),new Vector2(x0,y1-d));
            p.LineTo(new Vector2(x0,y0+a));p.BezierCurveTo(new Vector2(x0,y0+a-k*a),new Vector2(x0+a-k*a,y0),new Vector2(x0+a,y0));p.ClosePath();
        }
        /// <summary>
        /// The design's style-active="transform: translateY(3px)" on raised controls: while pressed, an element with a
        /// ledge sinks by up to 3 px (2 px on the shallow ledges of small chips), ledge and all.
        /// </summary>
        public static void PressSink(VisualElement element)
        {
            Galaxy101.Audio.CursorRules.Mark(element,"pointer"); // v4 cursor: a pressable control shows the pointer and plays ui.tap
            StyleTranslate rest=default;bool down=false;
            element.RegisterCallback<PointerDownEvent>(_=>
            {
                float depth=LedgeDepth(element);if(down||depth<=0||!element.enabledInHierarchy)return;
                down=true;rest=element.style.translate;var now=element.resolvedStyle.translate;
                element.style.translate=new Translate(now.x,now.y+Mathf.Min(3,depth-1));
            },TrickleDown.TrickleDown);
            void Up(){if(!down)return;down=false;element.style.translate=rest;}
            element.RegisterCallback<PointerUpEvent>(_=>Up());element.RegisterCallback<PointerLeaveEvent>(_=>Up());element.RegisterCallback<PointerCancelEvent>(_=>Up());
        }
        public static void HoverBrightness(VisualElement element,float gain)
        {
            List<FilterFunction> previous=null;
            element.RegisterCallback<PointerEnterEvent>(_=>
            {
                previous=element.style.filter.value==null?new List<FilterFunction>():new List<FilterFunction>(element.style.filter.value);
                var filters=new List<FilterFunction>(previous){Function(FilterFunctionType.Tint,new FilterParameter(new Color(gain,gain,gain,1)))};
                Apply(element,filters);
            });
            element.RegisterCallback<PointerLeaveEvent>(_=>{if(previous!=null)Apply(element,previous);});
        }
    }
}
