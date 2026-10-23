using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Galaxy101
{
    /// <summary>Native offscreen effects. RenderTexture is the bottom UI Toolkit background layer.</summary>
    [DisallowMultipleComponent]
    [ExecuteAlways]
    public sealed class GalaxyBackground : MonoBehaviour
    {
        private VisualElement target;
        private Material planetMaterial, gradientMaterial, starMaterial;
        private Mesh screenQuad,planetQuad,ringQuad;
        private Texture2D surface;
        private RenderTexture output;
        private CommandBuffer commands;
        private Starfield starfield;
        private PlanetDefinition planet=PlanetDefinition.For("Satürn");
        private readonly BackgroundTimeState timeState=new BackgroundTimeState();
        private bool motion=true,focused=true,dirty=true,wasRunning;
        private string quality="high";
        private double renderedAt=-1;
        private float starDelta;
        private int outputWidth,outputHeight;
        private bool tableLayout;
        private Color tableHalo;
        private Vector4 layoutShape, layoutHalo, layoutPlacement;
        private static readonly int BackdropId=Shader.PropertyToID("_Galaxy101CanvasBackdropCopy");
        public BackgroundTimeState TimeState => timeState;
        public RenderTexture Output => output;

        public void Initialize(VisualElement backgroundTarget)
        {
            if(backgroundTarget==null) throw new ArgumentNullException(nameof(backgroundTarget));
            if(target!=null && target!=backgroundTarget) target.style.backgroundImage=StyleKeyword.None;
            target=backgroundTarget;target.pickingMode=PickingMode.Ignore;
            EnsureResources();dirty=true;
        }
        public void Configure(string name,bool animate,string effectQuality)
        {
            var next=PlanetDefinition.For(name);
            string nextQuality=NormalizeQuality(effectQuality);
            bool changed=next!=planet||nextQuality!=quality;
            if(motion!=animate) wasRunning=false;
            motion=animate;quality=nextQuality;planet=next;
            if(changed) {surface=null;dirty=true;}
        }
        /// <summary>
        /// Game-table composition (Galaxy101_OyunMasasi_3D): gradient ellipse 90% 75% at 28% 12%, var(--pg) halo
        /// (closest-side, transparent at 70 %) and the planet static at (300,118) × .82, −16°, one turn per 40 s.
        /// No planet glow, rim or menu vignette.
        /// </summary>
        public void UseTableLayout(Color halo) => UseStaticLayout(new Vector4(358.4f,86.4f,1152,540),new Vector4(300,118,210,1),new Vector4(300,118,.82f,0),halo);
        /// <summary>
        /// A static composition (no menu glow, rim or vignette; planet at a fixed −16°, one turn per 40 s): gradient centre +
        /// radii, a pg halo (centre, radius) and the planet (centre, scale) in 1280×720 canvas pixels. Used by the table and
        /// the table loading screen (T50).
        /// </summary>
        public void UseStaticLayout(Vector4 gradient,Vector4 halo,Vector4 placement,Color haloColor)
        {
            // Same composition again (a screen redrawing itself): nothing to re-render.
            if(tableLayout&&tableHalo==haloColor&&layoutShape==gradient&&layoutHalo==new Vector4(halo.x,halo.y,halo.z,1)&&layoutPlacement==placement)return;
            tableLayout=true;tableHalo=haloColor;layoutShape=gradient;layoutHalo=new Vector4(halo.x,halo.y,halo.z,1);layoutPlacement=placement;dirty=true;}
        /// <summary>Back to the menu composition (glow, rim, vignette, the planet behind the logo).</summary>
        public void UseMenuLayout() {if(!tableLayout)return;tableLayout=false;dirty=true;}
        public void SetFocused(bool value) {if(focused!=value) wasRunning=false;focused=value;}
        // A batch player deliberately renders into its own targets without an OS foreground window.
        private void OnApplicationFocus(bool value) => SetFocused(value||Application.isBatchMode);
        private void OnApplicationPause(bool value) => SetFocused(!value||Application.isBatchMode);
        private void OnEnable() {wasRunning=false;dirty=true;}
        private void OnDisable() {wasRunning=false;}
        private static string NormalizeQuality(string value)
        {
            if(string.Equals(value,"low",StringComparison.OrdinalIgnoreCase)||value=="Düşük"||value=="düşük") return "low";
            if(string.Equals(value,"medium",StringComparison.OrdinalIgnoreCase)||value=="Orta"||value=="orta") return "medium";
            return "high";
        }
        private bool IsVisible()
        {
            if(target==null || target.panel==null || target.worldBound.width<=0 || target.worldBound.height<=0) return false;
            for(var element=target;element!=null;element=element.parent)
                if(element.resolvedStyle.display==DisplayStyle.None || element.resolvedStyle.visibility==Visibility.Hidden || element.resolvedStyle.opacity<=0) return false;
            return target.worldBound.Overlaps(target.panel.visualTree.worldBound);
        }
        private void Update()
        {
            // Native rendering belongs to the running menu. ExecuteAlways keeps editor-side
            // destruction callbacks reliable for resources explicitly initialized by tooling.
            if(!Application.isPlaying) return;
            bool visible=IsVisible();bool running=visible&&focused&&motion;
            float delta=running&&wasRunning?Time.unscaledDeltaTime:0;
            timeState.Advance(delta,running);wasRunning=running;starDelta+=delta;
            if(!visible || !focused) return;
            float scale=Mathf.Clamp(Mathf.Min(Screen.width/1280f,Screen.height/720f),1,2.2f);
            int width=Mathf.RoundToInt(1280*scale),height=Mathf.RoundToInt(720*scale);
            if(width!=outputWidth||height!=outputHeight) dirty=true;
            // Efekt kalitesi: Düşük 20, Orta 30 updates per second; Yüksek follows the game's own frame rate (Kare hızı), so a
            // 144 Hz display gets a 144 Hz planet instead of a 60 Hz one.
            int fps=quality=="low"?20:quality=="medium"?30:0;
            if(!dirty && (!running||fps>0&&timeState.Seconds-renderedAt<1.0/fps)) return;
            EnsureResources();ResizeOutput(width,height);
            RenderFrame(scale);renderedAt=timeState.Seconds;starDelta=0;dirty=false;
        }
        private static Material CreateMaterial(string path)
        {
            var shader=Resources.Load<Shader>(path);
            if(shader==null) throw new InvalidOperationException("Required Galaxy101 shader is missing from Resources: "+path);
            return new Material(shader) {name=path,hideFlags=HideFlags.DontSave};
        }
        private void EnsureResources()
        {
            if(planetMaterial!=null) return;
            planetMaterial=CreateMaterial("Galaxy101Planet");gradientMaterial=CreateMaterial("Galaxy101Gradient");starMaterial=CreateMaterial("Galaxy101Stars");
            screenQuad=Quad(0,0,1280,720);planetQuad=Quad(-150,-150,150,150);ringQuad=Quad(-360,-150,360,150);
            commands=new CommandBuffer {name="Galaxy101 background composite"};starfield=new Starfield();
        }
        private static Mesh Quad(float left,float top,float right,float bottom)
        {
            var mesh=new Mesh {name="Galaxy101 background quad",hideFlags=HideFlags.DontSave};
            mesh.vertices=new[] {new Vector3(left,top),new Vector3(right,top),new Vector3(right,bottom),new Vector3(left,bottom)};
            mesh.uv=new[] {Vector2.zero,Vector2.right,Vector2.one,Vector2.up};mesh.triangles=new[] {0,1,2,0,2,3};return mesh;
        }
        private void ResizeOutput(int width,int height)
        {
            if(output!=null&&outputWidth==width&&outputHeight==height) return;
            if(output!=null) {output.Release();Release(output);}
            outputWidth=width;outputHeight=height;
            output=new RenderTexture(width,height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB) {name="Galaxy101 live background",hideFlags=HideFlags.DontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            if(!output.Create()) throw new InvalidOperationException("Galaxy101 background render target could not be created.");
            target.style.backgroundImage=Background.FromRenderTexture(output);
        }
        private void RenderFrame(float scale)
        {
            float phase=(float)(timeState.Seconds%14/7);phase=phase>1?2-phase:phase;
            // CSS ease-in-out is cubic-bezier(.42,0,.58,1), not a sine wave.
            float angle=(tableLayout?-16:Mathf.Lerp(-16,-14,EaseInOut(phase)))*Mathf.Deg2Rad;
            gradientMaterial.SetColor("_Bg1",planet.Bg1);gradientMaterial.SetColor("_Bg2",planet.Bg2);gradientMaterial.SetColor("_Bg3",planet.Bg3);
            gradientMaterial.SetColor("_Halo",tableLayout?tableHalo:planet.Halo);
            gradientMaterial.SetVector("_Shape",tableLayout?layoutShape:new Vector4(640,252,1024,504));
            gradientMaterial.SetVector("_HaloShape",tableLayout?layoutHalo:new Vector4(640,300,231,0));
            planetMaterial.SetVector("_Placement",tableLayout?layoutPlacement:new Vector4(640,300,1,0));gradientMaterial.SetColor("_GlowAtmosphere",planet.GlowAtmosphere);
            gradientMaterial.SetFloat("_Flattening",planet.Flattening);gradientMaterial.SetFloat("_GroupAngle",angle);
            if(surface==null) surface=Resources.Load<Texture2D>("Galaxy101/Planets/"+planet.Resource);
            if(surface==null) throw new InvalidOperationException("Missing prebaked Galaxy101 planet texture: "+planet.Resource);
            planetMaterial.SetTexture("_Surface",surface);
            planetMaterial.SetVector("_Planet",new Vector4(planet.Tilt*Mathf.Deg2Rad,planet.Flattening,planet.Limb,planet.Ambient));
            planetMaterial.SetVector("_Atmosphere",new Vector4(planet.Atmosphere.r,planet.Atmosphere.g,planet.Atmosphere.b,planet.AtmosphereStrength));
            planetMaterial.SetFloat("_Rings",planet.Rings?1:0);planetMaterial.SetFloat("_Rotation",(float)(timeState.Seconds/(tableLayout?40:16)%1));
            planetMaterial.SetFloat("_GroupAngle",angle);planetMaterial.SetFloat("_PixelScale",scale);
            planetMaterial.SetFloat("_SurfaceLod",quality=="low"?2:quality=="medium"?1:0);
            starfield.Update(starDelta,timeState.Seconds,planet.Stars,quality=="low");
            commands.Clear();commands.SetRenderTarget(output);commands.SetViewport(new Rect(0,0,output.width,output.height));commands.ClearRenderTarget(false,true,Color.black);
            commands.GetTemporaryRT(BackdropId,output.width,output.height,0,FilterMode.Bilinear,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            commands.DrawMesh(screenQuad,Matrix4x4.identity,gradientMaterial,0,0);
            commands.DrawMesh(starfield.Mesh,Matrix4x4.identity,starMaterial,0,0);
            CopyBackdrop();
            commands.DrawMesh(screenQuad,Matrix4x4.identity,gradientMaterial,0,1);
            CopyBackdrop();
            commands.DrawMesh(planet.Rings?ringQuad:planetQuad,Matrix4x4.identity,planetMaterial,0,0);
            if(!tableLayout)
            {
                CopyBackdrop();
                commands.DrawMesh(screenQuad,Matrix4x4.identity,gradientMaterial,0,2);
            }
            commands.ReleaseTemporaryRT(BackdropId);
            Graphics.ExecuteCommandBuffer(commands);
            target.MarkDirtyRepaint();
        }
        private void CopyBackdrop()
        {
            commands.Blit(output,BackdropId);commands.SetGlobalTexture("_CanvasBackdrop",BackdropId);
            commands.SetRenderTarget(output);commands.SetViewport(new Rect(0,0,output.width,output.height));
        }
        /// <summary>Render one owned framebuffer for offscreen capture without advancing animation.</summary>
        public void RenderOnce(int width,int height)
        {
            if(target==null) throw new InvalidOperationException("Initialize the background before rendering a capture.");
            if(width<=0||height<=0||width>4096||height>4096) throw new ArgumentOutOfRangeException(nameof(width),"Capture dimensions must be between 1 and 4096 pixels.");
            EnsureResources();ResizeOutput(width,height);
            RenderFrame(Mathf.Min(width/1280f,height/720f));renderedAt=timeState.Seconds;dirty=false;starDelta=0;
        }
        private static float EaseInOut(float x)
        {
            float lo=0,hi=1,t=.5f;
            for(int i=0;i<12;i++) {t=(lo+hi)*.5f;float q=1-t;float bx=3*q*q*t*.42f+3*q*t*t*.58f+t*t*t;if(bx<x) lo=t;else hi=t;}
            return t*t*(3-2*t);
        }
        private static void Release(UnityEngine.Object value) {if(value==null) return;if(Application.isPlaying) Destroy(value);else DestroyImmediate(value);}
        private void OnDestroy()
        {
            if(target!=null) target.style.backgroundImage=StyleKeyword.None;
            if(output!=null) {output.Release();Release(output);output=null;}
            commands?.Release();commands=null;starfield?.Dispose();starfield=null;
            Release(screenQuad);Release(planetQuad);Release(ringQuad);Release(planetMaterial);Release(gradientMaterial);Release(starMaterial);
        }
    }
}
