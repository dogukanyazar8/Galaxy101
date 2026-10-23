using UnityEngine;
using UnityEngine.Rendering;

namespace Galaxy101.Table
{
    /// <summary>
    /// The design's image-based lighting (table3d.js buildEnv + scene.environment, environmentIntensity 0.75, plus the
    /// hemisphere light): a dark 12 m room (#10131f) with four glowing panels. Without it URP lights the table with the
    /// default sky — grey reflections that wash the felt and the tiles out. This builds the same room as
    /// <list type="bullet">
    /// <item>a cube render for specular reflections (the default reflection, at intensity 0.75), and</item>
    /// <item>an ambient probe projected analytically from the room × 0.75 and the hemisphere light (diffuse).</item>
    /// </list>
    /// Both are in three.js radiance units; three's IBL uses radiance directly, as URP does.
    /// </summary>
    public static class ThreeEnvironment
    {
        public const float EnvironmentIntensity = 0.75f;
        private const int Layer = 30, Size = 128;

        internal readonly struct Panel
        {
            public readonly Vector3 Center, Normal, AxisU, AxisV; public readonly float HalfW, HalfH; public readonly Color Radiance;
            public Panel(float w, float h, Color radiance, Vector3 center, Quaternion rotation)
            {
                Center = center; HalfW = w / 2; HalfH = h / 2; Radiance = radiance;
                AxisU = rotation * Vector3.right; AxisV = rotation * Vector3.up; Normal = rotation * Vector3.forward;
            }
        }

        private static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c.linear; }

        // buildEnv(): panel(w, h, colour, k, x, y, z, rx, ry) — a double-sided basic plane of colour × k, in three space.
        internal static Panel[] Panels(Color tint) => new[]
        {
            new Panel(5, 1.4f, Hex("#ffffff") * 3.2f, new Vector3(0, 5.5f, 0), ThreeSpace.Euler(Mathf.PI / 2, 0, 0)),
            new Panel(3, 3, Hex("#dfe6ff") * 1.6f, new Vector3(-5.5f, 2, 1), ThreeSpace.Euler(0, Mathf.PI / 2, 0)),
            new Panel(6, 2.5f, tint.linear * 1.4f, new Vector3(0, 1.5f, -5.8f), ThreeSpace.Euler(0, 0, 0)),
            new Panel(2, 4, Hex("#ffffff") * 1.0f, new Vector3(5.5f, 1.5f, -1), ThreeSpace.Euler(0, -Mathf.PI / 2, 0)),
        };

        /// <summary>Radiance seen from the room's centre along a three-space direction (nearest panel, else the box).</summary>
        private static Color Radiance(Vector3 d, Panel[] panels)
        {
            float wall = 6f / Mathf.Max(Mathf.Abs(d.x), Mathf.Max(Mathf.Abs(d.y), Mathf.Abs(d.z)));
            float best = wall; var color = Hex("#10131f");
            foreach (var p in panels)
            {
                float den = Vector3.Dot(p.Normal, d); if (Mathf.Abs(den) < 1e-5f) continue;
                float t = Vector3.Dot(p.Center, p.Normal) / den; if (t <= 0 || t >= best) continue;
                var hit = d * t - p.Center;
                if (Mathf.Abs(Vector3.Dot(hit, p.AxisU)) <= p.HalfW && Mathf.Abs(Vector3.Dot(hit, p.AxisV)) <= p.HalfH) { best = t; color = p.Radiance; }
            }
            return color;
        }

        /// <summary>
        /// Lights the current scene like the design: custom ambient probe and reflection cube. Returns the cube (owned by
        /// the caller; release it with the scene).
        /// </summary>
        public static RenderTexture Apply(Color tint, Color sky, Color ground, float hemiIntensity)
        {
            var panels = Panels(tint);
            RenderSettings.ambientMode = AmbientMode.Custom;
            RenderSettings.ambientProbe = AmbientProbe(panels, sky.linear * hemiIntensity, ground.linear * hemiIntensity);
            var cube = RenderCube(panels);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = cube;
            RenderSettings.reflectionIntensity = EnvironmentIntensity;
            return cube;
        }

        /// <summary>The ambient probe alone (no GPU work), for checks.</summary>
        public static SphericalHarmonicsL2 AmbientProbe(Color tint, Color sky, Color ground, float hemiIntensity) =>
            AmbientProbe(Panels(tint), sky.linear * hemiIntensity, ground.linear * hemiIntensity);

        /// <summary>
        /// Diffuse lighting as an L2 probe: the room's radiance × 0.75 plus the hemisphere light's radiance field
        /// (irradiance a + b·n.y ⇔ radiance a/π + 3b/(2π)·ω.y). Each texel enters as a directional light whose Unity
        /// intensity makes it evaluate to radiance·dω/π on the facing normal (a delta of total radiance X lights a surface by
        /// X·cosθ/π; URP's ambient term is albedo × probe).
        /// </summary>
        /// <summary>What a unit AddDirectionalLight evaluates to on the normal facing it (Unity's normalisation).</summary>
        private static float Facing
        {
            get
            {
                var probe = new SphericalHarmonicsL2(); probe.AddDirectionalLight(Vector3.up, Color.white, 1);
                // The L2 clamped cosine peaks at 1/4 + 1/2 + 5/16 = 1.0625 of the true cosine lobe on the facing normal.
                var result = new Color[1]; probe.Evaluate(new[] { Vector3.up }, result); return result[0].r / 1.0625f;
            }
        }

        internal static SphericalHarmonicsL2 AmbientProbe(Panel[] panels, Color sky, Color ground)
        {
            var sh = new SphericalHarmonicsL2();
            Color a = (sky + ground) * 0.5f, b = (sky - ground) * 0.5f;
            const int n = 48; float facing = Facing;
            for (int face = 0; face < 6; face++)
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float u = 2 * (x + 0.5f) / n - 1, v = 2 * (y + 0.5f) / n - 1;
                        var d = face switch { 0 => new Vector3(1, v, u), 1 => new Vector3(-1, v, u), 2 => new Vector3(u, 1, v), 3 => new Vector3(u, -1, v), 4 => new Vector3(u, v, 1), _ => new Vector3(u, v, -1) };
                        float r2 = d.sqrMagnitude; d /= Mathf.Sqrt(r2);
                        float solid = 4f / (n * n) / (r2 * Mathf.Sqrt(r2)); // texel solid angle on a unit cube face
                        var radiance = Radiance(d, panels) * EnvironmentIntensity + a / Mathf.PI + b * (1.5f / Mathf.PI * d.y);
                        // three space → Unity (mirror x); AddDirectionalLight lights normals facing `direction`.
                        sh.AddDirectionalLight(ThreeSpace.ToUnity(d), radiance, solid / Mathf.PI / facing);
                    }
            return sh;
        }

        private static RenderTexture RenderCube(Panel[] panels)
        {
            var root = new GameObject("three_env") { hideFlags = HideFlags.HideAndDontSave };
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            Material Mat(Color linear, float cull)
            {
                var m = new Material(unlit) { hideFlags = HideFlags.HideAndDontSave };
                m.SetColor("_BaseColor", linear.gamma); m.SetFloat("_Cull", cull); return m;
            }
            var mats = new System.Collections.Generic.List<Material>();
            GameObject Shape(PrimitiveType type, Material m)
            {
                var go = GameObject.CreatePrimitive(type); go.layer = Layer; go.transform.SetParent(root.transform, false);
                Object.DestroyImmediate(go.GetComponent<Collider>()); var r = go.GetComponent<Renderer>(); r.sharedMaterial = m; r.shadowCastingMode = ShadowCastingMode.Off; mats.Add(m);
                return go;
            }
            var box = Shape(PrimitiveType.Cube, Mat(Hex("#10131f"), (float)CullMode.Front)); box.transform.localScale = Vector3.one * 12;
            foreach (var p in panels)
            {
                var q = Shape(PrimitiveType.Quad, Mat(p.Radiance, (float)CullMode.Off));
                q.transform.SetPositionAndRotation(ThreeSpace.ToUnity(p.Center), ThreeSpace.ToUnity(Quaternion.LookRotation(p.Normal, p.AxisV)));
                q.transform.localScale = new Vector3(p.HalfW * 2, p.HalfH * 2, 1);
            }
            var camGo = new GameObject("three_env_camera") { hideFlags = HideFlags.HideAndDontSave }; camGo.transform.SetParent(root.transform, false);
            var cam = camGo.AddComponent<Camera>(); cam.enabled = false; cam.cullingMask = 1 << Layer; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.05f; cam.farClipPlane = 40; cam.allowHDR = true; cam.allowMSAA = false;
            var data = camGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(); data.renderPostProcessing = false; data.renderShadows = false;
            var cube = new RenderTexture(Size, Size, 16, RenderTextureFormat.ARGBHalf) { dimension = TextureDimension.Cube, useMipMap = true, autoGenerateMips = true, name = "Galaxy101 three.js environment", hideFlags = HideFlags.DontSave };
            cube.Create();
            cam.RenderToCubemap(cube);
            foreach (var m in mats) Object.DestroyImmediate(m);
            Object.DestroyImmediate(root);
            return cube;
        }
    }
}
