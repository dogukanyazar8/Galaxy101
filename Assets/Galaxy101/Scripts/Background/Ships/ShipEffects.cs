using System;
using UnityEngine;
using NVector = System.Numerics.Vector3;

namespace Galaxy101
{
    /// <summary>Original engine, navigation, trail and combat effects, batched into one reusable dynamic mesh.</summary>
    internal sealed class ShipEffects : IDisposable
    {
        private const int Quads = 4096;
        private readonly ShipFlightSimulation simulation;
        private readonly GameObject owner;
        private readonly Mesh mesh;
        private readonly Material material;
        private readonly Texture2D atlas;
        private readonly Vector3[] vertices = new Vector3[Quads * 4];
        private readonly Vector2[] uvs = new Vector2[Quads * 4];
        private readonly Color[] colors = new Color[Quads * 4];
        private readonly int[] indices = new int[Quads * 6];
        private readonly Trail[] trails = { new Trail(140), new Trail(140), new Trail(140), new Trail(110), new Trail(110) };
        private readonly bool[] modelReady = new bool[5];
        private int count;
        private readonly struct Engine
        {
            public readonly NVector Position;
            public readonly float Radius, Length;
            public readonly Color Color;
            public Engine(float x, float y, float z, float radius, float length, uint color) { Position = new NVector(x, y, z); Radius = radius; Length = length; Color = Hex(color); }
        }
        private static readonly Engine[][] Engines = {
            new[] { new Engine(-.3f,-.02f,-1.02f,.3f,2.1f,0x8fd8ff), new Engine(-.3f,-.02f,1.02f,.3f,2.1f,0x8fd8ff), new Engine(-1.85f,.02f,-.36f,.14f,.2f,0x8fd8ff), new Engine(-1.85f,.02f,0,.14f,.2f,0x8fd8ff), new Engine(-1.85f,.02f,.36f,.14f,.2f,0x8fd8ff) },
            new[] { new Engine(-.75f,-.02f,-1.2f,.16f,1,0xff8a5c), new Engine(-.75f,-.02f,1.2f,.16f,1,0xff8a5c), new Engine(-1.3f,.1f,0,.2f,.4f,0xff8a5c) },
            new[] { new Engine(-1.85f,.14142136f,.14142136f,.13f,.2f,0xb9a0ff), new Engine(-1.85f,-.14142136f,.14142136f,.13f,.2f,0xb9a0ff), new Engine(-1.85f,-.14142136f,-.14142136f,.13f,.2f,0xb9a0ff), new Engine(-1.85f,.14142136f,-.14142136f,.13f,.2f,0xb9a0ff) },
            new[] { new Engine(-.62f,-.005f,-.13f,.075f,.3f,0x8fd8ff), new Engine(-.62f,-.005f,.13f,.075f,.3f,0x8fd8ff) },
            new[] { new Engine(-.62f,-.005f,-.13f,.075f,.3f,0xff7a4a), new Engine(-.62f,-.005f,.13f,.075f,.3f,0xff7a4a) }
        };
        private static readonly NVector[][] Nav = {
            new[] { new NVector(.75f,-.02f,-1.32f),new NVector(.75f,-.02f,1.32f),new NVector(-1.1f,1.06f,-.3f) },
            new[] { new NVector(-.8f,0,-1.4f),new NVector(-.8f,0,1.4f),new NVector(-1.2f,1.08f,0) },
            new[] { new NVector(-1.2f,0,-1.08f),new NVector(-1.2f,0,1.08f),new NVector(-1.65f,.45f,0) },
            new[] { new NVector(-.5f,-.012f,-1.02f),new NVector(-.5f,-.012f,1.02f),new NVector(-.62f,.38f,0) },
            new[] { new NVector(-.5f,-.012f,-1.02f),new NVector(-.5f,-.012f,1.02f),new NVector(-.62f,.38f,0) }
        };
        private sealed class Trail
        {
            public readonly NVector[] Position;
            public readonly Color[] Color;
            public readonly float[] Age;
            public int Cursor; public float Accumulator;
            public Trail(int size) { Position = new NVector[size]; Color = new Color[size]; Age = new float[size]; for (int i = 0; i < size; i++) Age[i] = 9; }
        }
        public ShipEffects(Transform parent, ShipFlightSimulation flight, int layer)
        {
            simulation = flight;
            mesh = new Mesh { name = "Galaxy101 ship transient effects", hideFlags = HideFlags.DontSave }; mesh.MarkDynamic();
            material = GalaxyShips.Material("Galaxy101ShipsFx"); atlas = CreateAtlas(); material.SetTexture("_MainTex", atlas); material.SetColor("_Color", Color.white);
            owner = new GameObject("Galaxy101 ship transient effects", typeof(MeshFilter), typeof(MeshRenderer)) { hideFlags = HideFlags.HideAndDontSave, layer = layer }; owner.transform.SetParent(parent, false);
            owner.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = owner.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false; renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off; renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            for (int i = 0; i < Quads; i++) { int k = i * 6, v = i * 4; indices[k] = v; indices[k + 1] = v + 1; indices[k + 2] = v + 2; indices[k + 3] = v; indices[k + 4] = v + 2; indices[k + 5] = v + 3; }
            simulation.Stepped += Tick;
        }
        public void ClearTrails()
        {
            foreach (var trail in trails) { trail.Cursor = 0; trail.Accumulator = 0; for (int i = 0; i < trail.Age.Length; i++) trail.Age[i] = 9; }
        }
        public void SetModelReady(int index) => modelReady[index] = true;
        private void Tick(float delta)
        {
            for (int ship = 0; ship < 5; ship++)
            {
                var trail = trails[ship]; for (int i = 0; i < trail.Age.Length; i++) trail.Age[i] += delta;
                var pose = simulation.GetShip(ship); if (!modelReady[ship] || !pose.Visible) continue;
                trail.Accumulator += delta; float period = ship < 3 ? 1f / 40 : 1f / 60;
                while (trail.Accumulator >= period)
                {
                    trail.Accumulator -= period;
                    for (int i = 0; i < Engines[ship].Length && (ship >= 3 || i < 4); i++)
                    {
                        var engine = Engines[ship][i]; var local = engine.Position; local.X -= engine.Length / 2 + engine.Radius * 1.5f;
                        int k = trail.Cursor; trail.Position[k] = Point(pose, local); trail.Color[k] = engine.Color; trail.Age[k] = 0; trail.Cursor = (k + 1) % trail.Age.Length;
                    }
                }
            }
        }
        public void Build()
        {
            count = 0; float time = (float)simulation.Seconds;
            for (int ship = 0; ship < 5; ship++)
            {
                if (!modelReady[ship]) continue;
                var trail = trails[ship];
                for (int i = 0; i < trail.Age.Length; i++)
                {
                    float q = Mathf.Max(0, 1 - trail.Age[i] / 1.4f); if (q <= 0) continue;
                    var color = trail.Color[i]; color *= q * q * .6f; color.a = q * q * .6f;
                    Billboard(trail.Position[i], ship < 3 ? .22f : .12f, color);
                }
                var pose = simulation.GetShip(ship); if (!pose.Visible) continue;
                for (int i = 0; i < Engines[ship].Length; i++)
                {
                    var engine = Engines[ship][i]; float flame = ship < 3 ? .85f + .1f * Mathf.Sin(time * 31 + i * 2.1f) + .06f * Mathf.Sin(time * 53 + i) : .85f + .12f * Mathf.Sin(time * 37 + i * 2.1f) + .07f * Mathf.Sin(time * 61 + i);
                    var local = engine.Position; local.X -= engine.Length / 2 + engine.Radius * .5f;
                    var color = engine.Color; color.a = .75f + .25f * flame; Billboard(Point(pose, local), engine.Radius * 5.5f, color);
                    local = engine.Position; local.X -= engine.Length / 2;
                    Cone(pose, local, engine.Radius * .78f, engine.Radius * 7 * flame, color);
                }
                for (int nav = 0; nav < 3; nav++)
                {
                    float phase = simulation.GetPhase(ship), opacity = nav == 2 ? Mathf.Pow(Mathf.Max(0, Mathf.Sin(time * (ship < 3 ? 2.6f : 3.4f) + phase)), ship < 3 ? 18 : 16) : .55f + .45f * Mathf.Pow(.5f + .5f * Mathf.Sin(time * (ship < 3 ? 1.6f : 2.2f) + nav * 3), 4);
                    var color = nav == 0 ? Hex(0xff3b30) : nav == 1 ? Hex(0x34e27a) : Color.white; color.a = opacity;
                    Billboard(Point(pose, Nav[ship][nav]), ship < 3 ? (nav == 2 ? .45f : .3f) : (nav == 2 ? .26f : .22f), color);
                }
            }
            bool combatReady = modelReady[3] && modelReady[4];
            for (int i = 0; combatReady && i < simulation.BoltCount; i++)
            {
                var bolt = simulation.GetBolt(i); var axis = NVector.Normalize(bolt.Velocity); var normal = NVector.Normalize(NVector.Cross(new NVector(0, 0, 30) - bolt.Position, axis));
                Beam(bolt.Position, axis, normal, .7f, .09f, WithAlpha(Hex(0xff3a2e), .75f)); Beam(bolt.Position, axis, normal, .62f, .028f, Hex(0xfff1e8));
                if (bolt.Age < .08f)
                {
                    var shooter = simulation.GetShip(4); var muzzle = Point(shooter, new NVector(.6f, -.012f, bolt.Serial % 2 == 1 ? -1 : 1));
                    float q = bolt.Age / .08f; Billboard(muzzle, Mathf.Lerp(.55f, .2f, q), WithAlpha(Hex(0xff7a5a), (1 - q) * (1 - q)));
                }
            }
            float shield = simulation.ShieldOpacity;
            if (combatReady && shield > .001f) Shield(simulation.GetShip(3), WithAlpha(Hex(0x8fe3ff), shield));
            for (int i = 0; combatReady && i < simulation.HitCount; i++)
            {
                var hit = simulation.GetHit(i); float u = hit.Age / .5f;
                if (hit.Age < .28f) Billboard(hit.Position, Mathf.Lerp(.5f, 1.4f, hit.Age / .28f), WithAlpha(Hex(0xbff1ff), Mathf.Pow(1 - hit.Age / .28f, 2)));
                for (int k = 0; k < 18; k++)
                {
                    uint seed = unchecked((uint)(hit.Serial * 733 + k * 7919));
                    var velocity = NVector.Normalize(new NVector(Hash(seed) - .5f, Hash(seed + 1) - .5f, Hash(seed + 2) - .5f)) * (4 + Hash(seed + 3) * 6);
                    float distance = (1 - Mathf.Exp(-2.9957323f * hit.Age)) / 2.9957323f;
                    Billboard(hit.Position + velocity * distance, .09f, WithAlpha(Hex(0xffd2a0), 1 - u));
                }
            }
            // A smaller vertex buffer cannot coexist with the previous frame's larger triangle range.
            // Clear that range first; retain the mesh and all managed position/color/index buffers.
            if (count * 4 < mesh.vertexCount) mesh.SetIndices(indices, 0, 0, MeshTopology.Triangles, 0, false);
            mesh.SetVertices(vertices, 0, count * 4); mesh.SetUVs(0, uvs, 0, count * 4); mesh.SetColors(colors, 0, count * 4); mesh.SetIndices(indices, 0, count * 6, MeshTopology.Triangles, 0, false);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 300);
        }
        private void Billboard(NVector p, float size, Color color, float height = 1)
        {
            var x = new NVector(size * .5f, 0, 0); var y = new NVector(0, size * height * .5f, 0);
            Quad(p - x - y, p + x - y, p + x + y, p - x + y, color, false);
        }
        private void Cone(ShipPose pose, NVector head, float radius, float length, Color color)
        {
            var tip = head; tip.X -= length; tip = Point(pose, tip);
            const int sides = 32;
            for (int i = 0; i < sides; i++)
            {
                if (count == Quads) return;
                float a = i * Mathf.PI * 2 / sides, b = (i + 1) * Mathf.PI * 2 / sides;
                var left = head + new NVector(0, Mathf.Cos(a) * radius, Mathf.Sin(a) * radius);
                var right = head + new NVector(0, Mathf.Cos(b) * radius, Mathf.Sin(b) * radius);
                int k = count * 4; Quad(tip, tip, Point(pose, right), Point(pose, left), color, true);
                float u0 = .502f + .496f * i / sides, u1 = .502f + .496f * (i + 1) / sides;
                uvs[k] = uvs[k + 1] = new Vector2((u0 + u1) / 2, .002f); uvs[k + 2] = new Vector2(u1, .998f); uvs[k + 3] = new Vector2(u0, .998f);
            }
        }
        private void Shield(ShipPose pose, Color color)
        {
            const int rows = 20, sides = 32;
            for (int y = 0; y < rows; y++) for (int x = 0; x < sides; x++)
            {
                if (count == Quads) return;
                float p0 = y * Mathf.PI / rows, p1 = (y + 1) * Mathf.PI / rows, a0 = x * Mathf.PI * 2 / sides, a1 = (x + 1) * Mathf.PI * 2 / sides;
                var a = Point(pose, Sphere(p0, a0)); var b = Point(pose, Sphere(p0, a1)); var c = Point(pose, Sphere(p1, a1)); var d = Point(pose, Sphere(p1, a0));
                var center = (a + b + c + d) * .25f;
                // BasicMaterial in the source draws only the facing hemisphere; the plume pass is double-sided.
                if (NVector.Dot(center - pose.Position, new NVector(0, 0, 30) - center) <= 0) continue;
                int k = count * 4; Quad(a, b, c, d, color, false);
                uvs[k] = uvs[k + 1] = uvs[k + 2] = uvs[k + 3] = new Vector2(.25f, .5f);
            }
        }
        private static NVector Sphere(float latitude, float longitude) => new NVector(Mathf.Sin(latitude) * Mathf.Cos(longitude) * 1.05f, Mathf.Cos(latitude) * .45f, Mathf.Sin(latitude) * Mathf.Sin(longitude) * 1.15f);
        private void Beam(NVector p, NVector axis, NVector across, float length, float width, Color color)
        {
            var x = axis * (length * .5f); var y = across * (width * .5f); Quad(p - x - y, p + x - y, p + x + y, p - x + y, color, false);
        }
        private void Quad(NVector a, NVector b, NVector c, NVector d, Color color, bool plume)
        {
            if (count == Quads) return;
            int k = count++ * 4; vertices[k] = GalaxyShips.Convert(a); vertices[k + 1] = GalaxyShips.Convert(b); vertices[k + 2] = GalaxyShips.Convert(c); vertices[k + 3] = GalaxyShips.Convert(d);
            float left = plume ? .502f : .002f, right = plume ? .998f : .498f;
            uvs[k] = new Vector2(left, .002f); uvs[k + 1] = new Vector2(right, .002f); uvs[k + 2] = new Vector2(right, .998f); uvs[k + 3] = new Vector2(left, .998f);
            colors[k] = colors[k + 1] = colors[k + 2] = colors[k + 3] = color;
        }
        private static NVector Point(ShipPose pose, NVector local)
        {
            float sin = Mathf.Sin(pose.Roll), cos = Mathf.Cos(pose.Roll); local = new NVector(local.X, local.Y * cos - local.Z * sin, local.Y * sin + local.Z * cos);
            var forward = NVector.Normalize(pose.Velocity); var side = NVector.Normalize(NVector.Cross(forward, pose.Up));
            return pose.Position + forward * local.X + pose.Up * local.Y + side * local.Z;
        }
        private static float Hash(uint x) { x ^= x >> 16; x *= 0x7feb352d; x ^= x >> 15; x *= 0x846ca68b; x ^= x >> 16; return (x & 0x00ffffff) / 16777216f; }
        private static Color Hex(uint rgb) => ((Color)new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255)).linear;
        private static Color WithAlpha(Color c, float a) { c.a = a; return c; }
        private static Texture2D CreateAtlas()
        {
            var texture = new Texture2D(256, 128, TextureFormat.RGBA32, false, true) { name = "Galaxy101 ship effect atlas", hideFlags = HideFlags.DontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[256 * 128];
            for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++)
            {
                float radius = Mathf.Sqrt(Mathf.Pow((x + .5f - 64) / 64, 2) + Mathf.Pow((y + .5f - 64) / 64, 2));
                float alpha = radius < .18f ? Mathf.Lerp(1, .75f, radius / .18f) : radius < .5f ? Mathf.Lerp(.75f, .14f, (radius - .18f) / .32f) : Mathf.Lerp(.14f, 0, Mathf.Clamp01((radius - .5f) / .5f));
                pixels[y * 256 + x] = new Color(1, 1, 1, alpha);
                float vertical = (y + .5f) / 128, fade = vertical < .45f ? Mathf.Lerp(0, .35f, vertical / .45f) : Mathf.Lerp(.35f, 1, (vertical - .45f) / .55f);
                pixels[y * 256 + x + 128] = new Color(1, 1, 1, fade * (1 - Mathf.Abs((x + .5f - 64) / 64)));
            }
            texture.SetPixels32(pixels); texture.Apply(false, true); return texture;
        }
        public void Dispose() { simulation.Stepped -= Tick; GalaxyShips.DestroyOwned(owner); GalaxyShips.DestroyOwned(mesh); GalaxyShips.DestroyOwned(material); GalaxyShips.DestroyOwned(atlas); }
    }
}
