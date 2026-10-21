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
        public static void ClearSoftShadow(VisualElement element)
        {
            var filters=element.style.filter.value==null?new List<FilterFunction>():new List<FilterFunction>(element.style.filter.value);
            filters.RemoveAll(f=>f.type==FilterFunctionType.DropShadow&&f.parameterCount>=3&&f.GetParameter(2).floatValue>0);element.style.filter=new StyleList<FilterFunction>(filters);
        }
        public static void HardShadow(VisualElement element,float depth,Color color)
        {
            var filters=element.style.filter.value==null?new List<FilterFunction>():new List<FilterFunction>(element.style.filter.value);
            filters.RemoveAll(f=>f.type==FilterFunctionType.DropShadow&&f.parameterCount>=3&&f.GetParameter(2).floatValue==0);
            if(depth>0)filters.Insert(0,Function(FilterFunctionType.DropShadow,new FilterParameter(0f),new FilterParameter(depth),new FilterParameter(0f),new FilterParameter(color)));
            element.style.filter=new StyleList<FilterFunction>(filters);
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
