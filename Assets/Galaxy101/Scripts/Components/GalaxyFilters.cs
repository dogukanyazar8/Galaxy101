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
            element.style.filter=new StyleList<FilterFunction>(filters);
        }
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
            element.style.filter=new StyleList<FilterFunction>(sigma>0?new List<FilterFunction>{Function(FilterFunctionType.Blur,new FilterParameter(sigma))}:new List<FilterFunction>());
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
            element.style.filter=new StyleList<FilterFunction>(filters);
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
            element.style.filter=new StyleList<FilterFunction>(filters);
        }
        public static void ClearSoftShadow(VisualElement element)
        {
            var filters=element.style.filter.value==null?new List<FilterFunction>():new List<FilterFunction>(element.style.filter.value);
            filters.RemoveAll(f=>f.type==FilterFunctionType.DropShadow&&f.parameterCount>=3&&f.GetParameter(2).floatValue>0);element.style.filter=new StyleList<FilterFunction>(filters);
        }
        /// <summary>
        /// CSS "box-shadow: 0 depth 0 color" — the ledge under raised buttons and cards: the face's lower outline and the
        /// same outline shifted down by <paramref name="depth"/>, filled as one anti-aliased vector band in the element's
        /// own content. No layout, no offscreen filter pass, crisp at any scale (a drop-shadow filter clipped the corners
        /// and a bottom border tapered them into a scratch).
        /// </summary>
        public static void HardShadow(VisualElement element,float depth,Color color)
        {
            if(!ledges.TryGetValue(element,out var ledge))
            {
                if(depth<=0)return;
                ledge=new Ledge();ledges.Add(element,ledge);var owner=ledge;
                element.generateVisualContent+=context=>owner.Paint(element,context);
            }
            if(Mathf.Approximately(ledge.Depth,depth)&&ledge.Color==color)return;
            ledge.Depth=Mathf.Max(0,depth);ledge.Color=color;element.MarkDirtyRepaint();
        }
        /// <summary>The ledge depth <see cref="HardShadow"/> gave the element (0 when none).</summary>
        public static float LedgeDepth(VisualElement element)=>ledges.TryGetValue(element,out var ledge)?ledge.Depth:0;
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<VisualElement,Ledge> ledges=new System.Runtime.CompilerServices.ConditionalWeakTable<VisualElement,Ledge>();
        private sealed class Ledge
        {
            public float Depth;public Color Color;
            public void Paint(VisualElement element,MeshGenerationContext context)
            {
                float w=element.layout.width,h=element.layout.height,d=Depth;
                if(d<=0||Color.a<=0||float.IsNaN(w)||w<=0||h<=0)return;
                var style=element.resolvedStyle;
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
        public static void HoverBrightness(VisualElement element,float gain)
        {
            List<FilterFunction> previous=null;
            element.RegisterCallback<PointerEnterEvent>(_=>
            {
                previous=element.style.filter.value==null?new List<FilterFunction>():new List<FilterFunction>(element.style.filter.value);
                var filters=new List<FilterFunction>(previous){Function(FilterFunctionType.Tint,new FilterParameter(new Color(gain,gain,gain,1)))};
                element.style.filter=new StyleList<FilterFunction>(filters);
            });
            element.RegisterCallback<PointerLeaveEvent>(_=>{if(previous!=null)element.style.filter=new StyleList<FilterFunction>(previous);});
        }
    }
}
