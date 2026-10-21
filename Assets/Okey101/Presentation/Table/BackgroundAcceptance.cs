#if UNITY_EDITOR || OKEY_DIAGNOSTICS
using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

namespace Okey101.Presentation
{
    internal static class BackgroundAcceptance
    {
        internal static string FlowOutput {get;set;}
        internal static bool Requested => UnityEngine.Application.isBatchMode&&Array.IndexOf(Environment.GetCommandLineArgs(),"-okeyA4Background")>=0;
        internal static string Argument(string key)
        {var args=Environment.GetCommandLineArgs();var index=Array.IndexOf(args,key);return index>=0&&index+1<args.Length?args[index+1]:null;}
        internal static string Output => Path.GetFullPath(Argument("-okeyA4FlowOutput")??Argument("-okeyA4Output")??throw new InvalidOperationException("Background acceptance requires an output directory."));
        internal static void Configure(GameBootstrap game)
        {
            game.EnableBackgroundAcceptance();AudioListener.volume=0;
            var view=game.GetComponent<OffscreenAcceptanceView>();
            if(view==null){view=game.gameObject.AddComponent<OffscreenAcceptanceView>();view.Initialize(game);}
        }
    }

    internal sealed class OffscreenAcceptanceView : MonoBehaviour
    {
        private RenderTexture target;
        private Camera tableCamera;
        private RenderPipeline.StandardRequest request;
        internal int RenderedFrames {get;private set;}
        internal double LastRenderCpuMs {get;private set;}
        internal void Initialize(GameBootstrap game)
        {
            target=new RenderTexture(1920,1080,24){name="A4 offscreen 1080p"};target.Create();
            var camera=game.Presenter.TableCamera;tableCamera=camera;camera.targetTexture=target;camera.enabled=false;
            request=new RenderPipeline.StandardRequest {destination=target};
            foreach(var canvas in game.GetComponentsInChildren<Canvas>(true))
            {canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=camera.nearClipPlane+.1f;}
        }
        private void LateUpdate()
        {
            if(tableCamera==null||target==null)return;
            var start=System.Diagnostics.Stopwatch.GetTimestamp();
            Canvas.ForceUpdateCanvases();RenderPipeline.SubmitRenderRequest(tableCamera,request);
            LastRenderCpuMs=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency;
            RenderedFrames++;
        }
        private void OnDestroy(){if(target!=null){target.Release();Destroy(target);}}
    }
}
#endif
