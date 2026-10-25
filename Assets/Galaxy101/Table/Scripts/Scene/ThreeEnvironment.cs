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

        // Parsed once: Radiance runs millions of times while the reflection cube is prefiltered.
        private static readonly Color WallRadiance = Hex("#10131f");

        /// <summary>Radiance seen from the room's centre along a three-space direction (nearest panel, else the box).</summary>
        private static Color Radiance(Vector3 d, Panel[] panels)
        {
            float wall = 6f / Mathf.Max(Mathf.Abs(d.x), Mathf.Max(Mathf.Abs(d.y), Mathf.Abs(d.z)));
            float best = wall; var color = WallRadiance;
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
        /// Lights the table like the design: the ambient probe, and the studio room as a GGX-prefiltered reflection cube
        /// (three.js PMREM) behind a probe covering the table. Box-filtered mips would lose the bright panels at mid
        /// roughness, and gold rims, chrome and clear-coated tiles would read dark.
        /// </summary>
        public static void Apply(Transform parent, Color tint, Color sky, Color ground, float hemiIntensity)
        {
            var panels = Panels(tint);
            RenderSettings.ambientMode = AmbientMode.Custom;
            RenderSettings.ambientProbe = AmbientProbe(panels, sky.linear * hemiIntensity, ground.linear * hemiIntensity);
            var cube = Prefiltered(tint);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = cube;
            RenderSettings.reflectionIntensity = EnvironmentIntensity;
            var probeGo = new GameObject("three_env_probe"); probeGo.transform.SetParent(parent, false);
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Custom; probe.customBakedTexture = cube; probe.size = new Vector3(40, 40, 40);
            probe.intensity = EnvironmentIntensity; probe.importance = 100; probe.boxProjection = false; probe.blendDistance = 0;
        }

        private static Cubemap cached; private static Color cachedTint;

        /// <summary>
        /// The room's radiance prefiltered per mip like URP reads it (mip m ↔ perceptual roughness r with
        /// r·(1.7 − 0.7r) = m/6): GGX importance sampling (Hammersley, 96 samples, N = V = R), weighted by N·L. The room
        /// is analytic, so this needs no GPU work and is made once per tint.
        /// </summary>
        /// <summary>The warm studio tint the table lights its room with (TableScene).</summary>
        public static readonly Color TableTint = new Color32(0xFF, 0xC9, 0x4A, 255);
        private static System.Threading.Tasks.Task<Color[][]> pending; private static Color pendingTint;

        /// <summary>
        /// Starts prefiltering the reflection cube on worker threads (the menu calls it at start-up): the first table then
        /// only uploads the result instead of waiting for ~3 million samples.
        /// </summary>
        public static void Prewarm() => Prewarm(TableTint);

        public static void Prewarm(Color tint)
        {
            if (cached != null && cachedTint == tint || pending != null && pendingTint == tint) return;
            var panels = Panels(tint); // Unity colour APIs: on this thread
            pendingTint = tint; pending = System.Threading.Tasks.Task.Run(() => Prefilter(panels));
        }

        public static Cubemap Prefiltered(Color tint)
        {
            if (cached != null && cachedTint == tint) return cached;
            // A prewarm in flight (or done) for this tint is awaited; otherwise the work runs here.
            var faces = pending != null && pendingTint == tint ? pending.Result : Prefilter(Panels(tint));
            pending = null;
            var cube = new Cubemap(Size, TextureFormat.RGBAHalf, true) { name = "Galaxy101 three.js environment", hideFlags = HideFlags.DontSave };
            for (int mip = 0; mip < cube.mipmapCount; mip++)
                for (int face = 0; face < 6; face++) cube.SetPixels(faces[mip * 6 + face], (CubemapFace)face, mip);
            cube.Apply(false, false);
            cached = cube; cachedTint = tint;
            return cube;
        }

        /// <summary>Every mip's six faces, mip-major; pure math on the immutable panels, so it runs on any thread.</summary>
        private static Color[][] Prefilter(Panel[] panels)
        {
            int mips = 1; while ((Size >> mips) > 0) mips++; // a full chain: 128 → 1
            var faces = new Color[mips * 6][];
            const int samples = 96;
            var hammersley = new Vector2[samples];
            for (int i = 0; i < samples; i++) { uint b = (uint)i; b = (b << 16) | (b >> 16); b = ((b & 0x55555555u) << 1) | ((b & 0xAAAAAAAAu) >> 1); b = ((b & 0x33333333u) << 2) | ((b & 0xCCCCCCCCu) >> 2); b = ((b & 0x0F0F0F0Fu) << 4) | ((b & 0xF0F0F0F0u) >> 4); b = ((b & 0x00FF00FFu) << 8) | ((b & 0xFF00FF00u) >> 8); hammersley[i] = new Vector2((i + .5f) / samples, b * 2.3283064365386963e-10f); }
            for (int mip = 0; mip < mips; mip++)
            {
                int n = Mathf.Max(1, Size >> mip); float m = Mathf.Min(mip, 6) / 6f;
                float pr = (1.7f - Mathf.Sqrt(Mathf.Max(0, 2.89f - 2.8f * m))) / 1.4f, a = Mathf.Max(pr * pr, 1e-4f);
                for (int face = 0; face < 6; face++)
                {
                    var pixels = new Color[n * n]; int f = face, level = mip;
                    // Rows in parallel.
                    System.Threading.Tasks.Parallel.For(0, n, y =>
                    {
                        for (int x = 0; x < n; x++)
                        {
                            var dir = FaceDirection(f, x, y, n);
                            Color sum = Color.black;
                            if (level == 0) sum = Radiance(ToThree(dir), panels);
                            else
                            {
                                // Tangent frame around N.
                                var up = Mathf.Abs(dir.y) < .999f ? Vector3.up : Vector3.right;
                                var tx = Vector3.Normalize(Vector3.Cross(up, dir)); var ty = Vector3.Cross(dir, tx); float w = 0;
                                for (int i = 0; i < samples; i++)
                                {
                                    var h = hammersley[i]; float phi = 2 * Mathf.PI * h.x, cos = Mathf.Sqrt((1 - h.y) / (1 + (a * a - 1) * h.y)), sin = Mathf.Sqrt(1 - cos * cos);
                                    var half = tx * (sin * Mathf.Cos(phi)) + ty * (sin * Mathf.Sin(phi)) + dir * cos;
                                    var l = 2 * Vector3.Dot(dir, half) * half - dir; float nl = Vector3.Dot(dir, l);
                                    if (nl <= 0) continue;
                                    sum += Radiance(ToThree(l), panels) * nl; w += nl;
                                }
                                if (w > 0) sum /= w;
                            }
                            sum.a = 1; pixels[y * n + x] = sum;
                        }
                    });
                    faces[mip * 6 + face] = pixels;
                }
            }
            return faces;
        }

        private static Vector3 ToThree(Vector3 unity) => new Vector3(-unity.x, unity.y, unity.z);

        /// <summary>World direction of a cubemap texel (Unity/D3D face layout, row 0 at the top of the face).</summary>
        internal static Vector3 FaceDirection(int face, int x, int y, int n)
        {
            float u = 2 * (x + .5f) / n - 1, v = 2 * (y + .5f) / n - 1;
            Vector3 d;
            switch (face)
            {
                case 0: d = new Vector3(1, -v, -u); break;
                case 1: d = new Vector3(-1, -v, u); break;
                case 2: d = new Vector3(u, 1, v); break;
                case 3: d = new Vector3(u, -1, -v); break;
                case 4: d = new Vector3(u, -v, 1); break;
                default: d = new Vector3(-u, -v, -1); break;
            }
            return d.normalized;
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

        // The probe depends only on the planet tint and the hemisphere colours: one integration (14k texels) per look, reused
        // every time a table opens with it.
        private static readonly System.Collections.Generic.Dictionary<(Color, Color, Color, Color), SphericalHarmonicsL2> probes =
            new System.Collections.Generic.Dictionary<(Color, Color, Color, Color), SphericalHarmonicsL2>();

        internal static SphericalHarmonicsL2 AmbientProbe(Panel[] panels, Color sky, Color ground)
        {
            var key = (panels[2].Radiance, sky, ground, panels[0].Radiance);
            if (probes.TryGetValue(key, out var cached)) return cached;
            var sh = Integrate(panels, sky, ground);
            if (probes.Count > 16) probes.Clear();
            return probes[key] = sh;
        }

        private static SphericalHarmonicsL2 Integrate(Panel[] panels, Color sky, Color ground)
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

    }
}
