using System;
using Newtonsoft.Json.Linq;
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
        private Material planetMaterial, gradientMaterial, starMaterial, outgoingMaterial, pulseMaterial;
        private Mesh screenQuad,planetQuad,ringQuad,transitionQuad;
        private Texture2D surface,outgoingSurface,ringProfile;
        private RenderTexture output;
        private CommandBuffer commands;
        private Starfield starfield;
        private PlanetDefinition planet=PlanetDefinition.For("Satürn");
        private PlanetDefinition outgoingPlanet;
        private readonly PlanetTransition planetTransition=new PlanetTransition();
        private float outgoingRotation;
        private Color pulseAccent;
        private JObject themes;
        private readonly BackgroundTimeState timeState=new BackgroundTimeState();
        private bool motion=true,focused=true,dirty=true,wasRunning;
        private string quality="high";
        private double renderedAt=-1;
        private float starDelta;
        private int outputWidth,outputHeight;
        private bool tableLayout,authLayout;
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
            planetTransition.Configure(next.Resource,animate&&!tableLayout,timeState.Seconds);
            if(motion!=animate) {wasRunning=false;dirty=true;}
            if(next!=planet)
            {
                if(planetTransition.Previous!=null)
                {
                    outgoingPlanet=planet;outgoingSurface=surface;
                    outgoingRotation=(float)(timeState.Seconds/16%1);
                    pulseAccent=LoadAccent(next);
                }
                else ClearOutgoing();
            }
            if(!animate||tableLayout) ClearOutgoing();
            motion=animate;quality=nextQuality;planet=next;
            if(changed) {surface=null;dirty=true;}
        }
        private Color LoadAccent(PlanetDefinition definition)
        {
            if(themes==null)
            {
                var data=Resources.Load<TextAsset>("Galaxy101/themes");
                if(data==null)throw new InvalidOperationException("Missing Galaxy101 theme resource for the planet transition.");
                themes=JObject.Parse(data.text);
            }
            string value=(string)themes[definition.Name]?["cssVars"]?["a1"];
            if(!ColorUtility.TryParseHtmlString(value,out var color))throw new InvalidOperationException("Missing or invalid planet transition accent for "+definition.Name);
            return color;
        }
        private void ClearOutgoing() {outgoingPlanet=null;outgoingSurface=null;}
        private void CancelTransition() {planetTransition.Cancel();ClearOutgoing();}
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
            CancelTransition();
            // Same composition again (a screen redrawing itself): nothing to re-render.
            if(tableLayout&&!authLayout&&tableHalo==haloColor&&layoutShape==gradient&&layoutHalo==new Vector4(halo.x,halo.y,halo.z,1)&&layoutPlacement==placement)return;
            tableLayout=true;authLayout=false;tableHalo=haloColor;layoutShape=gradient;layoutHalo=new Vector4(halo.x,halo.y,halo.z,1);layoutPlacement=placement;dirty=true;}
        /// <summary>The opening and sign-in composition: reference gradient, moving planet glow and edge vignette.</summary>
        public void UseAuthLayout(Vector4 placement,Color haloColor)
        {
            CancelTransition();
            if(authLayout&&layoutPlacement==placement&&tableHalo==haloColor)return;
            tableLayout=true;authLayout=true;tableHalo=haloColor;
            layoutShape=new Vector4(640,252,1024,504);layoutHalo=new Vector4(placement.x,placement.y,231,0);layoutPlacement=placement;dirty=true;
        }
        /// <summary>Back to the menu composition (glow, rim, vignette, the planet behind the logo).</summary>
        public void UseMenuLayout() {if(!tableLayout)return;tableLayout=false;authLayout=false;dirty=true;}
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
            // A window may first open without OS focus. Draw a dirty static image once; focus only pauses animation.
            if(!visible || !focused&&!dirty) return;
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
            outgoingMaterial=CreateMaterial("Galaxy101Planet");pulseMaterial=CreateMaterial("Galaxy101PlanetTransition");
            ringProfile=RingProfileTexture.Acquire();planetMaterial.SetTexture("_RingProfile",ringProfile);outgoingMaterial.SetTexture("_RingProfile",ringProfile);
            screenQuad=Quad(0,0,1280,720);planetQuad=Quad(-150,-150,150,150);ringQuad=Quad(-360,-150,360,150);
            transitionQuad=Quad(-400,-200,400,200);
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
            float angle=(tableLayout&&!authLayout?-16:Mathf.Lerp(-16,-14,EaseInOut(phase)))*Mathf.Deg2Rad;
            var swap=planetTransition.Sample(timeState.Seconds);
            if(!swap.Active)ClearOutgoing();
            gradientMaterial.SetColor("_Bg1",planet.Bg1);gradientMaterial.SetColor("_Bg2",planet.Bg2);gradientMaterial.SetColor("_Bg3",planet.Bg3);
            gradientMaterial.SetColor("_Halo",tableLayout?tableHalo:planet.Halo);
            gradientMaterial.SetVector("_Shape",tableLayout?layoutShape:new Vector4(640,252,1024,504));
            gradientMaterial.SetVector("_HaloShape",tableLayout?layoutHalo:new Vector4(640,300,231,0));
            gradientMaterial.SetFloat("_AuthLayout",authLayout?1:0);gradientMaterial.SetVector("_AuthPlacement",layoutPlacement);
            gradientMaterial.SetFloat("_HaloOpacity",swap.HaloOpacity);gradientMaterial.SetColor("_GlowAtmosphere",planet.GlowAtmosphere);
            gradientMaterial.SetFloat("_Flattening",planet.Flattening);gradientMaterial.SetFloat("_GroupAngle",angle);
            if(surface==null) surface=LoadSurface(planet);
            ConfigurePlanet(planetMaterial,planet,surface,tableLayout?layoutPlacement:Placement(swap.Incoming,angle),angle+swap.Incoming.Rotation*Mathf.Deg2Rad,
                (float)(timeState.Seconds/(tableLayout?40:16)%1),swap.Incoming,scale);
            if(swap.Active&&outgoingPlanet!=null&&swap.Outgoing.Opacity>0)
            {
                if(outgoingSurface==null)outgoingSurface=LoadSurface(outgoingPlanet);
                ConfigurePlanet(outgoingMaterial,outgoingPlanet,outgoingSurface,Placement(swap.Outgoing,angle),angle+swap.Outgoing.Rotation*Mathf.Deg2Rad,
                    outgoingRotation,swap.Outgoing,scale);
            }
            pulseMaterial.SetVector("_Pulse",new Vector4(640,300,swap.RingScale,swap.RingOpacity));
            pulseMaterial.SetColor("_Accent",pulseAccent);pulseMaterial.SetColor("_Glow",planet.Halo);pulseMaterial.SetFloat("_PixelScale",scale);
            starfield.Update(starDelta,timeState.Seconds,planet.Stars,quality=="low");
            commands.Clear();commands.SetRenderTarget(output);commands.SetViewport(new Rect(0,0,output.width,output.height));commands.ClearRenderTarget(false,true,Color.black);
            commands.GetTemporaryRT(BackdropId,output.width,output.height,0,FilterMode.Bilinear,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
            commands.DrawMesh(screenQuad,Matrix4x4.identity,gradientMaterial,0,0);
            commands.DrawMesh(starfield.Mesh,Matrix4x4.identity,starMaterial,0,0);
            CopyBackdrop();
            commands.DrawMesh(screenQuad,Matrix4x4.identity,gradientMaterial,0,1);
            CopyBackdrop();
            commands.DrawMesh(swap.Active?transitionQuad:planet.Rings?ringQuad:planetQuad,Matrix4x4.identity,planetMaterial,0,0);
            if(swap.Active&&outgoingPlanet!=null&&swap.Outgoing.Opacity>0)
            {
                CopyBackdrop();commands.DrawMesh(transitionQuad,Matrix4x4.identity,outgoingMaterial,0,0);
            }
            if(swap.RingOpacity>0)
            {
                CopyBackdrop();commands.DrawMesh(screenQuad,Matrix4x4.identity,pulseMaterial,0,0);
            }
            if(!tableLayout||authLayout)
            {
                CopyBackdrop();
                commands.DrawMesh(screenQuad,Matrix4x4.identity,gradientMaterial,0,2);
            }
            commands.ReleaseTemporaryRT(BackdropId);
            Graphics.ExecuteCommandBuffer(commands);
            target.MarkDirtyRepaint();
        }
        private static Texture2D LoadSurface(PlanetDefinition definition)
        {
            var texture=Resources.Load<Texture2D>("Galaxy101/Planets/"+definition.Resource);
            if(texture==null)throw new InvalidOperationException("Missing prebaked Galaxy101 planet texture: "+definition.Resource);
            return texture;
        }
        private static Vector4 Placement(PlanetTransition.Pose pose,float angle)
        {
            float c=Mathf.Cos(angle),s=Mathf.Sin(angle);
            return new Vector4(640+c*pose.X-s*pose.Y,300+s*pose.X+c*pose.Y,pose.Scale,1-pose.Opacity);
        }
        private void ConfigurePlanet(Material material,PlanetDefinition definition,Texture2D texture,Vector4 placement,float angle,float rotation,PlanetTransition.Pose pose,float scale)
        {
            material.SetVector("_Placement",placement);material.SetTexture("_Surface",texture);
            material.SetVector("_Planet",new Vector4(definition.Tilt*Mathf.Deg2Rad,definition.Flattening,definition.Limb,definition.Ambient));
            material.SetVector("_Atmosphere",new Vector4(definition.Atmosphere.r,definition.Atmosphere.g,definition.Atmosphere.b,definition.AtmosphereStrength));
            material.SetFloat("_Rings",definition.Rings?1:0);material.SetFloat("_Rotation",rotation);
            material.SetFloat("_GroupAngle",angle);material.SetFloat("_PixelScale",scale);material.SetFloat("_AuthLighting",authLayout?1:0);
            material.SetFloat("_SurfaceLod",quality=="low"?2:quality=="medium"?1:0);
            material.SetVector("_Transition",new Vector4(pose.Blur,pose.Brightness,0,0));
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
            CancelTransition();
            Release(screenQuad);Release(planetQuad);Release(ringQuad);Release(transitionQuad);
            Release(planetMaterial);Release(gradientMaterial);Release(starMaterial);Release(outgoingMaterial);Release(pulseMaterial);
            if(ringProfile!=null) {RingProfileTexture.Release(ringProfile);ringProfile=null;}
        }
    }
}
