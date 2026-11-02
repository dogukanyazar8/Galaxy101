using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace Galaxy101
{
    /// <summary>Renders the native rounded tiles and native text with the HTML's real 900px perspective during entry.</summary>
    internal sealed class LogoTilePresentation : IDisposable
    {
        // Includes the source glow and drop-shadow extents throughout the entry. No full-screen extra framebuffer.
        internal static readonly Rect Crop = new Rect(424, 112, 432, 320);
        internal sealed class Configuration
        {
            public Rect Crop = LogoTilePresentation.Crop;
            public Vector2 HostOrigin, PerspectiveOrigin = LogoTileMotion.PerspectiveOrigin;
            public float Perspective = LogoTileMotion.Perspective, EntryEnd = LogoTileMotion.EntryEnd;
            public string Name = "Galaxy101 logo perspective", ImageName = "logo-perspective";
            public Action<VisualElement, int, float> Apply = ApplyLogo;
        }
        private const int Layer = 28;
        private readonly Configuration configuration;
        private readonly VisualElement host;
        private readonly Image image;
        private readonly VisualElement[] flatTiles, spatialTiles = new VisualElement[3];
        private readonly Func<bool> renderAllowed;
        private readonly IVisualElementScheduledItem visibilityJob;
        private GameObject world;
        private UIDocument document;
        private PanelSettings panel;
        private Camera camera;
        private readonly List<Renderer> renderers = new List<Renderer>(2);
        private RenderTexture output;
        private bool disposed;
        private float time;

        public LogoTilePresentation(MenuView view, VisualElement owner, VisualElement[] originals, Func<int, VisualElement> createTile, Func<bool> renderAllowed = null, Configuration configuration = null)
        {
            this.configuration = configuration ?? new Configuration();
            var crop = this.configuration.Crop; var hostOrigin = this.configuration.HostOrigin;
            host = owner; flatTiles = originals; this.renderAllowed = renderAllowed;
            image = new Image { name = this.configuration.ImageName, pickingMode = PickingMode.Ignore };
            MenuView.Place(image, crop.x - hostOrigin.x, crop.y - hostOrigin.y, crop.width, crop.height); host.Add(image);
            try
            {
                var authored = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
                if (authored == null) throw new InvalidOperationException("The logo requires the authored Galaxy101 PanelSettings.");
                panel = UnityEngine.Object.Instantiate(authored); panel.name = this.configuration.Name + " panel"; panel.hideFlags = HideFlags.DontSave;
                panel.renderMode = PanelRenderMode.WorldSpace; panel.targetTexture = null;
                panel.scaleMode = PanelScaleMode.ConstantPixelSize; panel.scale = 1; panel.forceGammaRendering = true;
                world = new GameObject(this.configuration.Name + " world") { layer = Layer, hideFlags = HideFlags.DontSave };
                world.SetActive(false);
                document = world.AddComponent<UIDocument>(); document.panelSettings = panel;
                document.worldSpaceSizeMode = WorldSpaceSizeMode.Fixed;
                document.worldSpaceSize = new Vector2(1280, 720); document.pivot = Pivot.TopLeft; document.pivotReferenceSize = PivotReferenceSize.Layout;
                world.SetActive(true);
                var root = document.rootVisualElement; root.pickingMode = PickingMode.Ignore;
                for (int i = 0; i < view.Root.styleSheets.count; i++) root.styleSheets.Add(view.Root.styleSheets[i]);
                foreach (string name in view.Root.GetClasses()) if (name.StartsWith("theme-", StringComparison.Ordinal)) root.AddToClassList(name);
                for (int i = 0; i < spatialTiles.Length; i++) { spatialTiles[i] = createTile(i); spatialTiles[i].pickingMode = PickingMode.Ignore; root.Add(spatialTiles[i]); }
                root.Query<VisualElement>().ForEach(element => element.pickingMode = PickingMode.Ignore);
                RefreshRenderers();
                var cameraObject = new GameObject(this.configuration.Name + " camera") { layer = Layer, hideFlags = HideFlags.DontSave };
                cameraObject.transform.SetParent(world.transform, false);
                camera = cameraObject.AddComponent<Camera>(); camera.enabled = false; camera.depth = -100;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.clear; camera.cullingMask = 1 << Layer;
                camera.allowHDR = camera.allowMSAA = camera.allowDynamicResolution = camera.useOcclusionCulling = false;
                var data = cameraObject.AddComponent<UniversalAdditionalCameraData>(); data.renderPostProcessing = data.renderShadows = data.allowXRRendering = false;
                data.antialiasing = AntialiasingMode.None; data.volumeLayerMask = 0;
                data.requiresColorOption = data.requiresDepthOption = CameraOverrideOption.Off;
                RenderPipelineManager.beginCameraRendering += BeginCamera; RenderPipelineManager.endCameraRendering += EndCamera;
                host.RegisterCallback<DetachFromPanelEvent>(Detached);
                visibilityJob = host.schedule.Execute(UpdateVisibility).Every(0);
            }
            catch { Dispose(); throw; }
        }

        public void Step(float value)
        {
            if (disposed) return;
            time = value;
            if (value >= configuration.EntryEnd) { Dispose(); return; }
            for (int i = 0; i < spatialTiles.Length; i++)
            {
                configuration.Apply(spatialTiles[i], i, value);
                flatTiles[i].style.opacity = 0;
            }
            UpdateVisibility();
        }

        private static void ApplyLogo(VisualElement tile, int index, float time)
        {
            var pose = LogoTileMotion.Sample(index, time);
            var translation = pose.Translation + LogoTileMotion.SubpixelOffset(index);
            tile.style.opacity = pose.Opacity; tile.style.translate = new Translate(translation.x, translation.y);
            tile.style.rotate = new Rotate(pose.Rotation); tile.style.scale = new Scale(Vector3.one * pose.Scale);
        }

        private void UpdateVisibility()
        {
            if (disposed || camera == null) return;
            RefreshRenderers();
            bool visible = host.panel != null;
            for (var node = host; visible && node != null; node = node.parent)
                visible = node.resolvedStyle.display != DisplayStyle.None && node.resolvedStyle.visibility != Visibility.Hidden && node.resolvedStyle.opacity > 0;
            image.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
            camera.enabled = visible && time < configuration.EntryEnd && (renderAllowed == null || renderAllowed());
            if (!camera.enabled) return;
            var crop = configuration.Crop;
            float density = Mathf.Max(1, host.scaledPixelsPerPoint);
            density = Mathf.Min(density, SystemInfo.maxTextureSize / Mathf.Max(crop.width, crop.height));
            int width = Mathf.CeilToInt(crop.width * density), height = Mathf.CeilToInt(crop.height * density);
            if (output == null || output.width != width || output.height != height)
            {
                var next = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
                { name = configuration.Name + " framebuffer", hideFlags = HideFlags.DontSave, antiAliasing = 1, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                if (!next.Create()) { Destroy(next); throw new InvalidOperationException("Could not allocate the bounded logo perspective framebuffer."); }
                var old = output; output = next; image.image = output; camera.targetTexture = output; Destroy(old);
            }
            // WorldSpace UI's public worldTransform includes its authored pixels-per-unit and Y/Z flip.
            float unit = document.rootVisualElement.worldTransform.lossyScale.x;
            if (!float.IsFinite(unit) || unit <= 0) return;
            var origin = configuration.PerspectiveOrigin;
            camera.transform.position = new Vector3(origin.x, -origin.y, -configuration.Perspective) * unit;
            camera.transform.rotation = Quaternion.identity;
            float near = unit, distance = configuration.Perspective * unit;
            camera.nearClipPlane = near; camera.farClipPlane = distance * 2;
            camera.projectionMatrix = Matrix4x4.Frustum((crop.xMin - origin.x) * unit * near / distance, (crop.xMax - origin.x) * unit * near / distance,
                (origin.y - crop.yMax) * unit * near / distance, (origin.y - crop.yMin) * unit * near / distance, near, distance * 2);
        }

        private void BeginCamera(ScriptableRenderContext context, Camera value)
        {
            if (disposed) return;
            // UIDocument creates its native UIRenderer lazily. Discover it before every cull, not only at construction.
            RefreshRenderers();
            if (value == camera) foreach (var renderer in renderers) if (renderer != null) renderer.forceRenderingOff = false;
        }
        private void EndCamera(ScriptableRenderContext context, Camera value)
        { if (!disposed) RefreshRenderers(); }
        private void RefreshRenderers()
        {
            if (world == null) return;
            renderers.Clear(); world.GetComponents(renderers);
            foreach (var renderer in renderers) if (renderer != null) renderer.forceRenderingOff = true;
        }
        private void Detached(DetachFromPanelEvent evt) { if (evt.target == host) Dispose(); }

        public void Dispose()
        {
            if (disposed) return; disposed = true;
            visibilityJob?.Pause(); host.UnregisterCallback<DetachFromPanelEvent>(Detached);
            RenderPipelineManager.beginCameraRendering -= BeginCamera; RenderPipelineManager.endCameraRendering -= EndCamera;
            if (camera != null) { camera.enabled = false; camera.targetTexture = null; }
            image.image = null; image.RemoveFromHierarchy();
            foreach (var tile in flatTiles) if (tile != null) tile.style.opacity = 1;
            Destroy(world); Destroy(output); Destroy(panel); world = null; output = null; panel = null;
        }
        private static void Destroy(UnityEngine.Object value)
        { if (value == null) return; if (Application.isPlaying) UnityEngine.Object.Destroy(value); else UnityEngine.Object.DestroyImmediate(value); }
    }
}
