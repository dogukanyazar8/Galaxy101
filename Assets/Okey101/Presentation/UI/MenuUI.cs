using System;
using System.IO;
using Okey101.Application;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Okey101.Presentation
{
    internal static class MenuUI
    {
        internal static string SettingsPath
        {
            get
            {
#if UNITY_EDITOR || OKEY_DIAGNOSTICS
                if(BackgroundAcceptance.FlowOutput!=null)return Path.Combine(BackgroundAcceptance.FlowOutput,"settings.json");
                if(BackgroundAcceptance.Requested)return Path.Combine(BackgroundAcceptance.Output,"settings.json");
#endif
                return Path.Combine(UnityEngine.Application.persistentDataPath,"settings.json");
            }
        }
        internal static LocalSettings LoadSettings(string legacyPath=null,string galaxyPath=null)
        {
            var settings=LocalSettings.Load(legacyPath??SettingsPath,message=>Debug.LogWarning(message));
            if(Galaxy101.GalaxyPreferencesBridge.TryLoadMasterVolume(out var volume,out var error,galaxyPath))settings.MasterVolume=volume;
            else if(error!=null)Debug.LogWarning("Galaxy menu volume could not be loaded; table settings retained. "+error);
            return settings;
        }
        internal static void SaveSettings(LocalSettings settings,string legacyPath=null,string galaxyPath=null)
        {
            bool native=Galaxy101.GalaxyPreferencesBridge.TryLoadMasterVolume(out var previous,out var error,galaxyPath);
            if(error!=null)throw new IOException("Galaxy menu volume could not be loaded: "+error);
            Galaxy101.GalaxyPreferencesBridge.SaveMasterVolume(settings.MasterVolume,galaxyPath);
            try{settings.Save(legacyPath??SettingsPath);}
            catch(Exception failure) when(failure is IOException||failure is UnauthorizedAccessException)
            {
                if(native)
                {
                    try{Galaxy101.GalaxyPreferencesBridge.SaveMasterVolume(previous,galaxyPath);}
                    catch(Exception rollback) when(rollback is IOException||rollback is UnauthorizedAccessException)
                    {throw new IOException("Table settings could not be saved and the previous menu volume could not be restored.",new AggregateException(failure,rollback));}
                }
                throw;
            }
        }
        internal static void Apply(LocalSettings settings)
        {
#if UNITY_EDITOR || OKEY_DIAGNOSTICS
            if(BackgroundAcceptance.Requested){AudioListener.volume=0;return;}
#endif
            AudioListener.volume=settings.MasterVolume;
            if(!UnityEngine.Application.isEditor)
                Screen.fullScreenMode=settings.Fullscreen?FullScreenMode.FullScreenWindow:FullScreenMode.Windowed;
        }
        internal static void EnsureEvents(Transform owner)
        {
            if(EventSystem.current!=null)return;
            var events=new GameObject("Menu input",typeof(EventSystem),typeof(InputSystemUIInputModule));events.transform.SetParent(owner,false);
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
        internal static RectTransform Canvas(Transform parent,string name)
        {
            var go=new GameObject(name,typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));go.transform.SetParent(parent,false);
            go.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            return go.GetComponent<RectTransform>();
        }
        internal static Text Text(Transform parent,string value,Vector2 min,Vector2 max,int size=28)
        { var text=TableUI.Label(parent,value,min,max,size);text.text=value;return text; }
        internal static void Stretch(RectTransform rect)
        {rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero;}
        internal static Slider VolumeSlider(Transform parent,Vector2 min,Vector2 max)
        {
            var root=TableUI.Rect(parent,"Master volume",min,max);var slider=root.gameObject.AddComponent<Slider>();
            var track=TableUI.Panel(root,"Track",new Vector2(0,.35f),new Vector2(1,.65f),new Color(.18f,.29f,.25f));
            var fill=TableUI.Panel(track,"Volume",Vector2.zero,Vector2.one,new Color(.76f,.58f,.29f));slider.fillRect=fill;
            var area=TableUI.Rect(root,"Handle area",new Vector2(.01f,0),new Vector2(.99f,1));
            var handle=TableUI.Panel(area,"Handle",Vector2.zero,Vector2.one,new Color(.94f,.85f,.65f));handle.sizeDelta=new Vector2(24,0);
            slider.handleRect=handle;slider.targetGraphic=handle.GetComponent<Image>();slider.minValue=0;slider.maxValue=1;return slider;
        }
        internal static Toggle Toggle(Transform parent,string name,Vector2 min,Vector2 max)
        {
            var root=TableUI.Rect(parent,name,min,max);var toggle=root.gameObject.AddComponent<Toggle>();
            var box=TableUI.Panel(root,"Box",new Vector2(0,.12f),new Vector2(.08f,.88f),new Color(.2f,.29f,.25f));
            var mark=TableUI.Panel(box,"Checked",new Vector2(.18f,.18f),new Vector2(.82f,.82f),new Color(.9f,.73f,.43f));
            toggle.targetGraphic=box.GetComponent<Image>();toggle.graphic=mark.GetComponent<Image>();
            Text(root,name,new Vector2(.13f,0),Vector2.one,28).alignment=TextAnchor.MiddleLeft;return toggle;
        }
    }
}
