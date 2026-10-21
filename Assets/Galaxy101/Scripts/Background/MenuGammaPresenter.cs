using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;

namespace Galaxy101
{
    /// <summary>Composites this menu in CSS color space without changing the game's linear rendering.</summary>
    [DisallowMultipleComponent]
    public sealed class MenuGammaPresenter : MonoBehaviour
    {
        private const int PresentationLayer = 31;
        private UIDocument document;
        private PanelSettings originalPanel;
        private PanelSettings ownedPanel;
        private RenderTexture output;
        private GameObject presentation;
        private Camera presentationCamera;
        private Mesh mesh;
        private Material material;

        public bool Initialized { get; private set; }
        public RenderTexture Output => output;

        public void Initialize(UIDocument target)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (Initialized)
            {
                if (document != target) throw new InvalidOperationException("A menu presenter cannot own two UI Documents.");
                return;
            }
            if (target.panelSettings == null) throw new InvalidOperationException("The menu requires authored PanelSettings before gamma presentation is initialized.");
            var shader = Resources.Load<Shader>("Galaxy101MenuComposite");
            if (shader == null) throw new InvalidOperationException("Missing Galaxy101MenuComposite shader resource.");

            document = target;
            originalPanel = target.panelSettings;
            try
            {
                ownedPanel = Instantiate(originalPanel);
                ownedPanel.name = "Galaxy101 owned gamma panel";
                ownedPanel.hideFlags = HideFlags.DontSave;
                ownedPanel.renderMode = PanelRenderMode.ScreenSpaceOverlay;
                ownedPanel.forceGammaRendering = true;
                ownedPanel.clearColor = true;
                ownedPanel.colorClearValue = Color.clear;
                // UITK already changes input to top-left screen pixels and applies panel scaling afterwards.
                ownedPanel.SetScreenToPanelSpaceFunction(position => position);

                material = new Material(shader) { name = "Galaxy101 owned gamma presentation", hideFlags = HideFlags.DontSave };
                mesh = new Mesh { name = "Galaxy101 owned presentation quad", hideFlags = HideFlags.DontSave };

                presentation = new GameObject("Galaxy101 gamma presentation") { layer = PresentationLayer, hideFlags = HideFlags.DontSave };
                presentation.transform.SetParent(transform, false);
                presentationCamera = presentation.AddComponent<Camera>();
                presentationCamera.transform.localPosition = new Vector3(0, 0, -2);
                presentationCamera.orthographic = true;
                presentationCamera.orthographicSize = 1;
                presentationCamera.nearClipPlane = .1f;
                presentationCamera.farClipPlane = 10;
                presentationCamera.cullingMask = 1 << PresentationLayer;
                presentationCamera.clearFlags = CameraClearFlags.SolidColor;
                presentationCamera.backgroundColor = Color.black;
                presentationCamera.allowHDR = false;
                presentationCamera.allowMSAA = false;
                presentationCamera.allowDynamicResolution = false;
                presentationCamera.useOcclusionCulling = false;
                presentationCamera.targetDisplay = originalPanel.targetDisplay;
                var cameraData = presentation.AddComponent<UniversalAdditionalCameraData>();
                cameraData.renderPostProcessing = false;
                cameraData.antialiasing = AntialiasingMode.None;
                cameraData.renderShadows = false;
                cameraData.requiresColorOption = CameraOverrideOption.Off;
                cameraData.requiresDepthOption = CameraOverrideOption.Off;
                cameraData.allowXRRendering = false;

                var quad = new GameObject("Opaque menu quad") { layer = PresentationLayer, hideFlags = HideFlags.DontSave };
                quad.transform.SetParent(presentation.transform, false);
                quad.transform.localPosition = new Vector3(0, 0, 2);
                quad.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = quad.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                renderer.allowOcclusionWhenDynamic = false;

                ResizeOutput();
                document.panelSettings = ownedPanel;
                presentationCamera.enabled = isActiveAndEnabled;
                Initialized = true;
            }
            catch
            {
                ReleaseOwnedResources();
                throw;
            }
        }

        private void LateUpdate()
        {
            // Capture tools may temporarily substitute their own panel. Never replace or resize that panel.
            if (!Initialized || document == null || document.panelSettings != ownedPanel) return;
            if (output.width != Mathf.Max(1, Screen.width) || output.height != Mathf.Max(1, Screen.height)) ResizeOutput();
        }

        private void ResizeOutput()
        {
            var width = Mathf.Max(1, Screen.width);
            var height = Mathf.Max(1, Screen.height);
            var replacement = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
            {
                name = "Galaxy101 CSS gamma framebuffer",
                hideFlags = HideFlags.DontSave,
                antiAliasing = 1,
                useMipMap = false,
                autoGenerateMips = false,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            if (!replacement.Create())
            {
                DestroyOwned(replacement);
                throw new InvalidOperationException("Could not allocate the Galaxy101 menu framebuffer.");
            }

            var previous = output;
            output = replacement;
            ownedPanel.targetTexture = output;
            material.mainTexture = output;
            var aspect = (float)width / height;
            presentationCamera.aspect = aspect;
            mesh.vertices = new[] { new Vector3(-aspect, -1, 0), new Vector3(-aspect, 1, 0), new Vector3(aspect, 1, 0), new Vector3(aspect, -1, 0) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            if (previous != null) { previous.Release(); DestroyOwned(previous); }
        }

        private void OnEnable() { if (presentationCamera != null) presentationCamera.enabled = true; }
        private void OnDisable() { if (presentationCamera != null) presentationCamera.enabled = false; }
        private void OnDestroy() { ReleaseOwnedResources(); }

        private void ReleaseOwnedResources()
        {
            Initialized = false;
            if (presentationCamera != null) presentationCamera.enabled = false;
            // A later owner (for example an offscreen capture) retains its own assignment.
            if (document != null && document.panelSettings == ownedPanel) document.panelSettings = originalPanel;
            if (ownedPanel != null) ownedPanel.targetTexture = null;
            if (material != null) material.mainTexture = null;
            DestroyOwned(presentation);
            if (output != null) { output.Release(); DestroyOwned(output); }
            DestroyOwned(mesh);
            DestroyOwned(material);
            DestroyOwned(ownedPanel);
            presentation = null;
            presentationCamera = null;
            output = null;
            mesh = null;
            material = null;
            ownedPanel = null;
            originalPanel = null;
            document = null;
        }

        private static void DestroyOwned(UnityEngine.Object resource)
        {
            if (resource == null) return;
            if (Application.isPlaying) Destroy(resource); else DestroyImmediate(resource);
        }
    }
}
