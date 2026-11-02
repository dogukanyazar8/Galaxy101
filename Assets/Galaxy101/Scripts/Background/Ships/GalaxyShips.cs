using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace Galaxy101
{
    /// <summary>Native transparent ship layer. Geometry is the original GLB; motion and transient effects are native.</summary>
    [DisallowMultipleComponent]
    [ExecuteAlways] // Explicit editor initialization must receive destruction callbacks; Update still requires PlayMode.
    public sealed class GalaxyShips : MonoBehaviour
    {
        private const int Layer = 30;
        private static readonly string[] Models = { "kervan", "ok", "halka", "avci_kacak", "avci_kovalayan" };
        private readonly ShipModelCache.Lease[] leases = new ShipModelCache.Lease[5];
        private readonly ShipVisual[] visuals = new ShipVisual[5];
        private VisualElement target;
        private CancellationTokenSource lifetime;
        private GameObject world;
        private Camera renderCamera;
        private Light key, rim;
        private ShipEffects effects;
        private ShipEnvironment environment;
        private RenderTexture hdr, output;
        private Material resolve, depth, glass;
        private Transform occluder, body, ring;
        private Mesh ringMesh;
        private ShipFlightSimulation simulation;
        private readonly RenderPipeline.StandardRequest request = new RenderPipeline.StandardRequest();
        private string planet = "Satürn", quality = "high";
        private bool motion = true, focused = true, table, wasRunning, dirty = true, destroyed;
        private double lastRender = -1;
        public RenderTexture Output => output;
        public Task Initialization { get; private set; } = Task.CompletedTask;
        public bool IsReady { get; private set; }
        public ShipFlightSimulation Simulation => simulation;

        public void Initialize(VisualElement backgroundTarget)
        {
            if (backgroundTarget == null) throw new ArgumentNullException(nameof(backgroundTarget));
            if (destroyed) throw new ObjectDisposedException(nameof(GalaxyShips));
            if (target != null && target != backgroundTarget) target.style.backgroundImage = StyleKeyword.None;
            target = backgroundTarget; target.pickingMode = PickingMode.Ignore;
            if (output != null) target.style.backgroundImage = Background.FromRenderTexture(output);
            if (lifetime != null) return;
            lifetime = new CancellationTokenSource();
            Initialization = LoadModels(lifetime.Token);
        }
        private async Task LoadModels(CancellationToken token)
        {
            try
            {
                CreateResources();
                for (int i = 0; i < Models.Length; i++)
                {
                    try
                    {
                        var lease = await ShipModelCache.Acquire(Models[i], token);
                        if (destroyed || token.IsCancellationRequested) { lease.Dispose(); token.ThrowIfCancellationRequested(); return; }
                        leases[i] = lease;
                        var model = lease.Instantiate(world.transform);
                        visuals[i] = new ShipVisual(model, i, glass); model.SetActive(false); effects.SetModelReady(i);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                    catch (Exception e) { Debug.LogError("Galaxy101 ship '" + Models[i] + "' could not load: " + e, this); }
                }
                token.ThrowIfCancellationRequested(); IsReady = true; dirty = true;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
            catch (Exception e) { Debug.LogError("Galaxy101 native ship layer could not initialize: " + e, this); }
        }
        public void Configure(string name, bool animate, string effectQuality)
        {
            name = ShipFlightSimulation.NormalizePlanet(name);
            string nextQuality = effectQuality == "Düşük" || string.Equals(effectQuality, "low", StringComparison.OrdinalIgnoreCase) ? "low" : effectQuality == "Orta" || string.Equals(effectQuality, "medium", StringComparison.OrdinalIgnoreCase) ? "medium" : "high";
            if (motion != animate) wasRunning = false;
            motion = animate;
            if (planet != name || quality != nextQuality)
            {
                planet = name ?? "Satürn"; quality = nextQuality;
                simulation?.Configure(planet, table); ConfigureOcclusion(); dirty = true;
            }
        }
        public void UseMenuLayout() => SetLayout(false);
        public void UseTableLayout() => SetLayout(true);
        private void SetLayout(bool value)
        {
            if (table == value) return;
            table = value; simulation?.Configure(planet, table); effects?.ClearTrails(); ConfigureOcclusion(); dirty = true;
        }
        public void SetFocused(bool value) { if (focused != value) wasRunning = false; focused = value; }
        private void OnApplicationFocus(bool value) => SetFocused(value || Application.isBatchMode);
        private void OnApplicationPause(bool value) => SetFocused(!value || Application.isBatchMode);
        private void OnEnable() { wasRunning = false; dirty = true; }
        private void OnDisable() { wasRunning = false; }
        private bool Visible()
        {
            if (target == null || target.panel == null || target.worldBound.width <= 0 || target.worldBound.height <= 0) return false;
            for (var element = target; element != null; element = element.parent)
                if (element.resolvedStyle.display == DisplayStyle.None || element.resolvedStyle.visibility == Visibility.Hidden || element.resolvedStyle.opacity <= 0) return false;
            return target.worldBound.Overlaps(target.panel.visualTree.worldBound);
        }
        private void Update()
        {
            if (!IsReady || !Application.isPlaying) return;
            bool visible = Visible(), running = visible && focused && motion;
            simulation.Advance(running && wasRunning ? Math.Min(.05, Time.unscaledDeltaTime) : 0, running); wasRunning = running;
            if (!visible || !focused) return;
            int rate = quality == "low" ? 20 : quality == "medium" ? 30 : 0;
            float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), 1, 2.2f);
            int width = Mathf.RoundToInt(1280 * scale), height = Mathf.RoundToInt(720 * scale);
            if (output == null || output.width != width || output.height != height) dirty = true;
            if (!dirty && (!running || rate > 0 && simulation.Seconds - lastRender < 1.0 / rate)) return;
            RenderOnce(width, height);
        }
        public void RenderOnce(int width, int height)
        {
            if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            if (!IsReady || destroyed) return;
            Resize(width, height);
            for (int i = 0; i < visuals.Length; i++) visuals[i]?.Apply(simulation.GetShip(i), (float)simulation.Seconds, simulation.GetPhase(i));
            effects.Build();
            // Nothing remains visible to unrelated cameras. SubmitRenderRequest completes synchronously.
            var ambient = RenderSettings.ambientMode; var probe = RenderSettings.ambientProbe;
            try
            {
                RenderSettings.ambientMode = AmbientMode.Custom; RenderSettings.ambientProbe = environment.Ambient;
                world.SetActive(true);
                request.destination = hdr;
                if (GraphicsSettings.currentRenderPipeline != null) RenderPipeline.SubmitRenderRequest(renderCamera, request);
                else { renderCamera.targetTexture = hdr; renderCamera.Render(); }
                Graphics.Blit(hdr, output, resolve);
            }
            finally { world.SetActive(false); RenderSettings.ambientMode = ambient; RenderSettings.ambientProbe = probe; }
            lastRender = simulation.Seconds; dirty = false;
        }
        private void CreateResources()
        {
            simulation = new ShipFlightSimulation(); simulation.Configure(planet, table);
            resolve = Material("Galaxy101ShipsResolve"); depth = Material("Galaxy101ShipsOcclusion");
            world = new GameObject("Galaxy101 owned ship world") { hideFlags = HideFlags.HideAndDontSave, layer = Layer };
            world.SetActive(false);
            var cameraObject = new GameObject("Galaxy101 ship camera") { hideFlags = HideFlags.HideAndDontSave, layer = Layer };
            cameraObject.transform.SetParent(world.transform, false); cameraObject.transform.localPosition = new Vector3(0, 0, 30); cameraObject.transform.localRotation = Quaternion.Euler(0, 180, 0);
            renderCamera = cameraObject.AddComponent<Camera>(); renderCamera.enabled = false; renderCamera.fieldOfView = 28; renderCamera.aspect = 16f / 9; renderCamera.nearClipPlane = .5f; renderCamera.farClipPlane = 140;
            renderCamera.cullingMask = 1 << Layer; renderCamera.clearFlags = CameraClearFlags.SolidColor; renderCamera.backgroundColor = Color.clear;
            renderCamera.allowHDR = true; renderCamera.allowMSAA = false; renderCamera.useOcclusionCulling = false;
            var data = cameraObject.AddComponent<UniversalAdditionalCameraData>(); data.renderPostProcessing = false; data.renderShadows = false; data.volumeLayerMask = 0; data.requiresColorOption = CameraOverrideOption.Off; data.requiresDepthOption = CameraOverrideOption.Off;
            // three.js lights in physical units: its Lambert divides by π, URP's does not, so the source's 3.0 / 1.6 are 3/π / 1.6/π here.
            key = Directional("key", new Color32(255, 240, 216, 255), 3 / Mathf.PI, new Vector3(9, 8, 10));
            rim = Directional("rim", new Color32(134, 174, 255, 255), 1.6f / Mathf.PI, new Vector3(-10, -3, -8));
            environment = new ShipEnvironment(world.transform, Layer);
            glass = Material("Galaxy101ShipsGlass"); glass.SetColor("_BaseColor", new Color32(207, 232, 255, 51)); glass.SetTexture("_EnvCube", environment.Cube); glass.SetFloat("_EnvIntensity", 2.4f);
            effects = new ShipEffects(world.transform, simulation, Layer);
            var group = new GameObject("Galaxy101 ship planet depth") { layer = Layer, hideFlags = HideFlags.HideAndDontSave }; group.transform.SetParent(world.transform, false); occluder = group.transform;
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere); sphere.name = "Galaxy101 ship planet body depth"; sphere.hideFlags = HideFlags.HideAndDontSave; sphere.layer = Layer; sphere.transform.SetParent(occluder, false); body = sphere.transform;
            DestroyOwned(sphere.GetComponent<Collider>()); sphere.GetComponent<Renderer>().sharedMaterial = depth;
            ringMesh = MakeRing();
            var ringObject = new GameObject("Galaxy101 ship Saturn ring depth", typeof(MeshFilter), typeof(MeshRenderer)) { layer = Layer, hideFlags = HideFlags.HideAndDontSave };
            ringObject.transform.SetParent(occluder, false); ringObject.GetComponent<MeshFilter>().sharedMesh = ringMesh; ringObject.GetComponent<MeshRenderer>().sharedMaterial = depth; ring = ringObject.transform;
            ConfigureOcclusion();
        }
        private Light Directional(string label, Color color, float intensity, Vector3 position)
        {
            var go = new GameObject("Galaxy101 ship " + label) { layer = Layer, hideFlags = HideFlags.HideAndDontSave }; go.transform.SetParent(world.transform, false); go.transform.localPosition = position; go.transform.localRotation = Quaternion.LookRotation(-position.normalized);
            var light = go.AddComponent<Light>(); light.type = LightType.Directional; light.color = color; light.intensity = intensity; light.shadows = LightShadows.None; light.cullingMask = 1 << Layer; return light;
        }
        private void ConfigureOcclusion()
        {
            if (occluder == null) return;
            float scale = table ? .82f : 1, unit = 2 * Mathf.Tan(14 * Mathf.Deg2Rad) * 30 / 720;
            occluder.localPosition = new Vector3(-(table ? 300 - 640 : 0) * unit, (360 - (table ? 118 : 300)) * unit, 0);
            occluder.localRotation = Quaternion.Euler(0, 0, 16);
            float radius = 149 * scale * unit, flat = planet == "Satürn" || planet == "saturn" ? 1.1f : planet == "Jüpiter" ? 1.07f : planet == "Neptün" ? 1.02f : 1;
            body.localScale = new Vector3(radius * 2, radius * 2 / flat, radius * 2);
            ring.localScale = Vector3.one * radius; ring.localRotation = Quaternion.Euler(-68, 0, 0); ring.gameObject.SetActive(planet == "Satürn" || planet == "saturn");
        }
        private static Mesh MakeRing()
        {
            const int n = 96; var vertices = new Vector3[(n + 1) * 2]; var indices = new int[n * 6];
            for (int i = 0; i <= n; i++) { float a = i * Mathf.PI * 2 / n; var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0); vertices[i * 2] = d * 1.53f; vertices[i * 2 + 1] = d * 2.27f; if (i == n) continue; int k = i * 6, v = i * 2; indices[k] = v; indices[k + 1] = v + 2; indices[k + 2] = v + 1; indices[k + 3] = v + 1; indices[k + 4] = v + 2; indices[k + 5] = v + 3; }
            var mesh = new Mesh { name = "Galaxy101 ship Saturn depth ring", hideFlags = HideFlags.DontSave }; mesh.vertices = vertices; mesh.triangles = indices; mesh.RecalculateBounds(); return mesh;
        }
        private void Resize(int width, int height)
        {
            if (output != null && output.width == width && output.height == height) return;
            ReleaseTexture(hdr); ReleaseTexture(output);
            hdr = Texture(width, height, 24, RenderTextureFormat.ARGBHalf, "Galaxy101 ship HDR");
            output = Texture(width, height, 0, RenderTextureFormat.ARGB32, "Galaxy101 ships");
            target.style.backgroundImage = Background.FromRenderTexture(output);
        }
        private static RenderTexture Texture(int width, int height, int depthBits, RenderTextureFormat format, string label)
        {
            var value = new RenderTexture(width, height, depthBits, format, RenderTextureReadWrite.sRGB) { name = label, hideFlags = HideFlags.DontSave, filterMode = FilterMode.Bilinear };
            if (!value.Create()) { DestroyOwned(value); throw new InvalidOperationException(label + " render target could not be created."); } return value;
        }
        internal static Material Material(string path)
        {
            var shader = Resources.Load<Shader>(path);
            if (shader == null) throw new InvalidOperationException("Required ship shader Resources/" + path + " is missing.");
            return new Material(shader) { name = "Galaxy101 ship " + path, hideFlags = HideFlags.DontSave };
        }
        internal static Vector3 Convert(System.Numerics.Vector3 value) => new Vector3(-value.X, value.Y, value.Z);
        internal static void DestroyOwned(UnityEngine.Object value) { if (value == null) return; if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
        private static void ReleaseTexture(RenderTexture texture) { if (texture == null) return; texture.Release(); DestroyOwned(texture); }
        private void OnDestroy()
        {
            destroyed = true; IsReady = false; lifetime?.Cancel(); lifetime?.Dispose(); lifetime = null;
            if (target != null) target.style.backgroundImage = StyleKeyword.None;
            // Destroy instances before releasing imports: cached meshes/textures are shared by their leases.
            if (world != null) world.SetActive(false);
            for (int i = 0; i < visuals.Length; i++) { visuals[i]?.Dispose(); visuals[i] = null; leases[i]?.Dispose(); leases[i] = null; }
            effects?.Dispose(); environment?.Dispose(); effects = null; environment = null;
            DestroyOwned(world); world = null; DestroyOwned(resolve); DestroyOwned(depth); DestroyOwned(glass); DestroyOwned(ringMesh);
            ReleaseTexture(hdr); ReleaseTexture(output); hdr = output = null;
        }
    }
}
