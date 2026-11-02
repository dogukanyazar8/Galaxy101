using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Galaxy101
{
    /// <summary>The source ships' five-panel studio environment, isolated to this layer's render interval.</summary>
    internal sealed class ShipEnvironment : IDisposable
    {
        private const int Size = 64;
        private readonly Cubemap cube;
        private readonly GameObject probeObject;
        public SphericalHarmonicsL2 Ambient { get; }
        public Cubemap Cube => cube;
        private readonly struct Panel
        {
            public readonly Vector3 Center, Normal, U, V;
            public readonly float W, H;
            public readonly Color Color;
            public Panel(float w, float h, Color color, Vector3 center)
            {
                Center = center; Normal = -center.normalized; U = Vector3.Cross(Vector3.up, Normal).normalized; V = Vector3.Cross(Normal, U); W = w / 2; H = h / 2; Color = color.linear;
            }
        }
        private static Color Hex(uint rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
        private static readonly Panel[] Panels = {
            new Panel(12,4,Hex(0xffffff),new Vector3(-8,12,6)), new Panel(6,6,Hex(0xffd9a0),new Vector3(12,4,8)),
            new Panel(16,3,Hex(0x6f90e8),new Vector3(0,-12,-4)), new Panel(4,10,Hex(0xffffff),new Vector3(-14,0,-6)),
            new Panel(8,3,Hex(0xc8b4ff),new Vector3(6,10,-12)) };
        private static readonly Color Wall = Hex(0x10152a).linear;
        public ShipEnvironment(Transform parent, int layer)
        {
            cube = new Cubemap(Size, TextureFormat.RGBAHalf, true) { name = "Galaxy101 ship studio environment", hideFlags = HideFlags.DontSave };
            var sequence = new Vector2[48];
            for (int i = 0; i < sequence.Length; i++) { uint b = (uint)i; b = (b << 16) | (b >> 16); b = ((b & 0x55555555) << 1) | ((b & 0xaaaaaaaa) >> 1); b = ((b & 0x33333333) << 2) | ((b & 0xcccccccc) >> 2); b = ((b & 0x0f0f0f0f) << 4) | ((b & 0xf0f0f0f0) >> 4); b = ((b & 0x00ff00ff) << 8) | ((b & 0xff00ff00) >> 8); sequence[i] = new Vector2((i + .5f) / sequence.Length, b * 2.3283064365386963e-10f); }
            for (int mip = 0; mip < cube.mipmapCount; mip++)
            {
                int size = Mathf.Max(1, Size >> mip); float m = Mathf.Min(mip, 6) / 6f;
                float pr = (1.7f - Mathf.Sqrt(Mathf.Max(0, 2.89f - 2.8f * m))) / 1.4f, a = Mathf.Max(pr * pr, 1e-4f);
                for (int face = 0; face < 6; face++)
                {
                    var pixels = new Color[size * size];
                    for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
                    {
                        var normal = Face(face, x, y, size); Color sum = Color.black;
                        if (mip == 0) sum = Radiance(normal);
                        else
                        {
                            var up = Mathf.Abs(normal.y) < .999f ? Vector3.up : Vector3.right;
                            var tx = Vector3.Cross(up, normal).normalized; var ty = Vector3.Cross(normal, tx); float weight = 0;
                            for (int i = 0; i < sequence.Length; i++)
                            {
                                var q = sequence[i]; float angle = 2 * Mathf.PI * q.x, cos = Mathf.Sqrt((1 - q.y) / (1 + (a * a - 1) * q.y)), sin = Mathf.Sqrt(1 - cos * cos);
                                var half = tx * (sin * Mathf.Cos(angle)) + ty * (sin * Mathf.Sin(angle)) + normal * cos;
                                var light = 2 * Vector3.Dot(normal, half) * half - normal; float nl = Vector3.Dot(normal, light);
                                if (nl <= 0) continue; sum += Radiance(light) * nl; weight += nl;
                            }
                            if (weight > 0) sum /= weight;
                        }
                        sum.a = 1; pixels[y * size + x] = sum;
                    }
                    cube.SetPixels(pixels, (CubemapFace)face, mip);
                }
            }
            cube.Apply(false, true);
            Ambient = IntegrateAmbient();
            probeObject = new GameObject("Galaxy101 ship reflection probe") { hideFlags = HideFlags.HideAndDontSave, layer = layer }; probeObject.transform.SetParent(parent, false);
            var probe = probeObject.AddComponent<ReflectionProbe>(); probe.mode = ReflectionProbeMode.Custom; probe.customBakedTexture = cube; probe.size = Vector3.one * 200; probe.intensity = 1; probe.boxProjection = false; probe.importance = 100; probe.blendDistance = 0;
        }
        private static Vector3 Face(int face, int x, int y, int n)
        {
            float u = 2 * (x + .5f) / n - 1, v = 2 * (y + .5f) / n - 1;
            var d = face == 0 ? new Vector3(1, -v, -u) : face == 1 ? new Vector3(-1, -v, u) : face == 2 ? new Vector3(u, 1, v) : face == 3 ? new Vector3(u, -1, -v) : face == 4 ? new Vector3(u, -v, 1) : new Vector3(-u, -v, -1);
            return d.normalized;
        }
        private static Color Radiance(Vector3 unityDirection)
        {
            var direction = new Vector3(-unityDirection.x, unityDirection.y, unityDirection.z); float distance = 20; var color = Wall;
            foreach (var p in Panels)
            {
                float denominator = Vector3.Dot(p.Normal, direction); if (Mathf.Abs(denominator) < 1e-6f) continue;
                float t = Vector3.Dot(p.Center, p.Normal) / denominator; if (t <= 0 || t >= distance) continue;
                var hit = direction * t - p.Center;
                if (Mathf.Abs(Vector3.Dot(hit, p.U)) <= p.W && Mathf.Abs(Vector3.Dot(hit, p.V)) <= p.H) { color = p.Color; distance = t; }
            }
            return color;
        }
        private static SphericalHarmonicsL2 IntegrateAmbient()
        {
            var probe = new SphericalHarmonicsL2(); var test = new SphericalHarmonicsL2(); test.AddDirectionalLight(Vector3.up, Color.white, 1);
            var evaluate = new Color[1]; test.Evaluate(new[] { Vector3.up }, evaluate); float facing = evaluate[0].r / 1.0625f;
            var sky = Hex(0x9db8ff).linear * .45f; var ground = Hex(0x1a1430).linear * .45f; var a = (sky + ground) * .5f; var b = (sky - ground) * .5f;
            const int n = 20;
            for (int face = 0; face < 6; face++) for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float u = 2 * (x + .5f) / n - 1, v = 2 * (y + .5f) / n - 1, r2 = 1 + u * u + v * v;
                var direction = Face(face, x, y, n); float solid = 4f / (n * n * r2 * Mathf.Sqrt(r2));
                var radiance = Radiance(direction) + a / Mathf.PI + b * (1.5f / Mathf.PI * direction.y);
                probe.AddDirectionalLight(direction, radiance, solid / Mathf.PI / facing);
            }
            return probe;
        }
        public void Dispose() { GalaxyShips.DestroyOwned(probeObject); GalaxyShips.DestroyOwned(cube); }
    }
}
