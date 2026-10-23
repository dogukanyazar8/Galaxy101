using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Galaxy101.Table.TableConstants;

namespace Galaxy101.Table
{
    /// <summary>
    /// Treats and gifts on the table (spec/12 §3–§4, table3d.js sendGift / tickGifts): the model flies 1.35 s from the
    /// sender to a free corner slot of the receiver (three per seat; the oldest makes room), lands with a burst, a gold
    /// ring and a squash, then idles until the hand ends (steam, swaying tulips, flickering candle, spinning cup, rising
    /// bubbles, a breathing rocket). The rocket takes a 5.6 s screen tour on a centripetal Catmull-Rom path with flame,
    /// sparks and smoke and lands with extra bursts and a camera shake.
    /// </summary>
    public sealed class TableGifts : MonoBehaviour
    {
        public static readonly IReadOnlyDictionary<string, float> GiftScale = new Dictionary<string, float>
        { ["tea"] = 1.05f, ["coffee"] = 1.0f, ["cola"] = 0.98f, ["simit"] = 0.85f, ["baklava"] = 0.82f, ["seeds"] = 0.9f, ["flower"] = 0.95f, ["cake"] = 0.82f, ["trophy"] = 1.1f, ["rocket"] = 1.15f };
        private static readonly Vector2[] Slots = { new Vector2(-0.43f, 0.57f), new Vector2(-0.3f, 0.61f), new Vector2(-0.46f, 0.44f) };
        private static readonly Vector2[] MySlots = { new Vector2(-0.58f, 0.42f), new Vector2(-0.6f, 0.27f), new Vector2(-0.47f, 0.5f) };
        private static readonly float[] Yaw = { 0, Mathf.PI / 2, Mathf.PI, -Mathf.PI / 2 };
        private static readonly Dictionary<string, float> SteamHeight = new Dictionary<string, float> { ["tea"] = 0.104f, ["coffee"] = 0.052f };

        private enum Phase { Fly, Land, Idle, Out }

        private sealed class Gift
        {
            public string Kind; public int From, To, Slot; public GameObject Go;
            public Vector3 Start, End; public float Yaw, Scale, T0, Duration, Landed, Left, Seed, NextSteam;
            /// <summary>Flight sparks are authored per 60 Hz frame; this carries the fraction so every frame rate emits as many per second.</summary>
            public float EmitDue;
            public Phase Phase; public RocketPath Path;
            public Transform Sway, Spin, Flame; public List<(Transform T, float A, float R)> Bubbles = new List<(Transform, float, float)>();
            public List<GameObject> FlameParts = new List<GameObject>();
            public bool Rocket => Kind == "rocket";
        }

        private readonly List<Gift>[] gifts = { new List<Gift>(), new List<Gift>(), new List<Gift>(), new List<Gift>() };
        private TableScene scene;
        private TableFx fx;
        private TableAssetCatalog catalog;
        private Transform root;

        /// <summary>A gift reached its slot: kind, sender, receiver (the receiver's notice and thanks wait for this).</summary>
        public event Action<string, int, int> Landed;

        /// <summary>Gifts standing (or flying to) a seat.</summary>
        public int Count(int seat) => gifts[seat].Count(g => g.Phase != Phase.Out);
        public int Flying => gifts.Sum(list => list.Count(g => g.Phase == Phase.Fly));
        public IEnumerable<GameObject> Models(int seat) => gifts[seat].Where(g => g.Phase != Phase.Out).Select(g => g.Go);

        public void Init(TableScene table, TableFx effects)
        {
            scene = table; fx = effects; catalog = TableAssetCatalog.Load();
            root = new GameObject("Hediyeler").transform; root.SetParent(transform, false);
        }

        public void Send(string kind, int from, int to)
        {
            var prefab = TableAssetCatalog.Find(catalog.GiftModels, kind);
            if (prefab == null) return;
            var list = gifts[to];
            int slot = Enumerable.Range(0, 3).Cast<int?>().FirstOrDefault(s => !list.Any(g => g.Slot == s && g.Phase != Phase.Out)) ?? -1;
            if (slot < 0) { var old = list.First(g => g.Phase != Phase.Out); Remove(old); slot = old.Slot; }
            var spot = to == 0 ? MySlots[slot] : Slots[slot];
            var g2 = new Gift
            {
                Kind = kind, From = from, To = to, Slot = slot, Go = Instantiate(prefab, root, false),
                Start = Origin(from), End = scene.SeatPoint(to, spot.x, TY, spot.y),
                Yaw = Yaw[to] + (Hash(kind + slot) - 0.5f) * 0.8f + (kind == "coffee" || kind == "tea" ? -0.6f : 0),
                Scale = GiftScale.TryGetValue(kind, out var s) ? s : 1.4f, T0 = Time.time, Duration = kind == "rocket" ? 5.6f : 1.35f, Seed = UnityEngine.Random.value * 6,
            };
            g2.Go.name = "hediye_" + kind;
            MergeStill(kind, g2.Go);
            foreach (var t in g2.Go.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "demet") g2.Sway = t;
                else if (t.name == "kupa_govde") g2.Spin = t;
                else if (t.name == "alev") g2.Flame = t;
                else if (t.name.StartsWith("kabarcik", StringComparison.Ordinal) && !t.name.EndsWith("_LOD1", StringComparison.Ordinal)) g2.Bubbles.Add((t, Mathf.Atan2(t.localPosition.z, t.localPosition.x), new Vector2(t.localPosition.x, t.localPosition.z).magnitude));
                if (t.name.StartsWith("alev", StringComparison.Ordinal)) g2.FlameParts.Add(t.gameObject);
            }
            if (g2.Rocket)
            {
                var cam = scene.CameraRig.Camera;
                Vector3 C(float nx, float ny, float dist) => cam.transform.position + cam.ViewportPointToRay(new Vector3((nx + 1) / 2, (ny + 1) / 2, 0)).direction * dist;
                g2.Path = new RocketPath(new[] { g2.Start, g2.Start + Vector3.up * 0.32f, C(-0.78f, 0.62f, 1.7f), C(-0.05f, 0.86f, 1.15f), C(0.82f, 0.5f, 1.0f), C(0.62f, -0.5f, 0.78f), C(-0.15f, -0.62f, 0.7f), C(-0.72f, -0.1f, 0.95f), C(-0.25f, 0.42f, 1.6f), g2.End + Vector3.up * 0.42f, g2.End + Vector3.up * 0.07f, g2.End });
                foreach (var part in g2.FlameParts) part.SetActive(true);
            }
            else if (g2.Kind != "cake") foreach (var part in g2.FlameParts) part.SetActive(false);
            g2.Go.transform.position = g2.Start; g2.Go.transform.localScale = Vector3.one * 0.001f;
            list.Add(g2);
        }

        // ---------- merged still parts ----------
        /// <summary>Parts that move on their own (bouquet sway, trophy cup, flames, cola bubbles) keep their renderers.</summary>
        private static bool Moves(string name) => name == "demet" || name == "kupa_govde" || name.StartsWith("alev", StringComparison.Ordinal) || name.StartsWith("kabarcik", StringComparison.Ordinal);

        private sealed class Merged { public int Lod; public Material Material; public Mesh Mesh; public UnityEngine.Rendering.ShadowCastingMode Shadows; public bool Receive; }
        private readonly Dictionary<string, List<Merged>> merged = new Dictionary<string, List<Merged>>();

        /// <summary>
        /// gifts3d.js draws a simit's 240 sesame seeds as one InstancedMesh; the GLB brings each as an object of its own
        /// (486 renderers over two LODs, 12 gifts on the table at most). Every still part of a gift is merged, per LOD and
        /// material, into one mesh made once per kind and shared by all its copies; the LOD group gets those renderers.
        /// </summary>
        private void MergeStill(string kind, GameObject go)
        {
            var group = go.GetComponentInChildren<LODGroup>();
            var lods = group != null ? group.GetLODs() : new[] { new LOD(0, go.GetComponentsInChildren<Renderer>(true)) };
            var basis = go.transform.worldToLocalMatrix;
            bool Still(Renderer r)
            {
                // A mesh the player cannot read (import setting) cannot be combined: it keeps its own renderer.
                var filter = r.GetComponent<MeshFilter>();
                if (!(r is MeshRenderer) || filter == null || filter.sharedMesh == null || !filter.sharedMesh.isReadable) return false;
                for (var t = r.transform; t != null && t != go.transform; t = t.parent) if (Moves(t.name)) return false;
                return true;
            }
            var still = lods.Select(l => l.renderers.Where(r => r != null && Still(r)).ToArray()).ToArray();
            if (still.Sum(r => r.Length) < 2) return;
            if (!merged.TryGetValue(kind, out var parts))
            {
                parts = new List<Merged>();
                for (int lod = 0; lod < still.Length; lod++)
                {
                    var pieces = still[lod].SelectMany(r => Enumerable.Range(0, Mathf.Min(r.sharedMaterials.Length, r.GetComponent<MeshFilter>().sharedMesh.subMeshCount))
                        .Select(sub => (Renderer: r, Sub: sub, Material: r.sharedMaterials[sub]))).Where(x => x.Material != null);
                    foreach (var byMaterial in pieces.GroupBy(x => x.Material))
                    {
                        var instances = byMaterial.Select(x => new CombineInstance { mesh = x.Renderer.GetComponent<MeshFilter>().sharedMesh, subMeshIndex = x.Sub, transform = basis * x.Renderer.transform.localToWorldMatrix }).ToArray();
                        var mesh = new Mesh { name = "hediye_" + kind + "_" + lod + "_" + byMaterial.Key.name };
                        if (instances.Sum(c => c.mesh.vertexCount) > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                        mesh.CombineMeshes(instances, true, true);
                        var first = byMaterial.First().Renderer;
                        parts.Add(new Merged { Lod = lod, Material = byMaterial.Key, Mesh = mesh, Shadows = first.shadowCastingMode, Receive = first.receiveShadows });
                    }
                }
                merged[kind] = parts;
            }
            foreach (var r in still.SelectMany(x => x))
            {
                if (r.transform.childCount == 0) { r.gameObject.SetActive(false); Destroy(r.gameObject); }
                else { r.enabled = false; Destroy(r.GetComponent<MeshFilter>()); Destroy(r); }
            }
            var added = parts.Select(part =>
            {
                var piece = new GameObject(part.Mesh.name); piece.transform.SetParent(go.transform, false);
                piece.AddComponent<MeshFilter>().sharedMesh = part.Mesh;
                var r = piece.AddComponent<MeshRenderer>(); r.sharedMaterial = part.Material; r.shadowCastingMode = part.Shadows; r.receiveShadows = part.Receive;
                return (part.Lod, Renderer: (Renderer)r);
            }).ToList();
            if (group == null) return;
            for (int lod = 0; lod < lods.Length; lod++)
                lods[lod].renderers = lods[lod].renderers.Where(r => r != null && !still[lod].Contains(r)).Concat(added.Where(a => a.Lod == lod).Select(a => a.Renderer)).ToArray();
            group.SetLODs(lods); group.RecalculateBounds();
        }

        private void OnDestroy()
        {
            foreach (var part in merged.Values.SelectMany(x => x)) if (part.Mesh != null) Destroy(part.Mesh);
            merged.Clear();
        }

        /// <summary>clearGifts(): every gift shrinks away (new hand).</summary>
        public void Clear() { foreach (var list in gifts) foreach (var g in list) if (g.Phase != Phase.Out) Remove(g); }

        private void Remove(Gift g) { g.Phase = Phase.Out; g.Left = Time.time; }

        /// <summary>originOf(): my hand at the lower left (−0.6, 1.1, 1.25), an opponent above their rack (0, 1.15, 1.1).</summary>
        private Vector3 Origin(int seat) => seat == 0 ? scene.SeatPoint(0, -0.6f, 1.1f, 1.25f) : scene.SeatPoint(seat, 0, 1.15f, 1.1f);

        private static float Hash(string s) { unchecked { int h = 0; foreach (char c in s) h = h * 31 + c; return (h & 0xffff) / 65535f; } }
        private static float EaseIO(float t) => EaseInOutCubic(t);
        private static Quaternion ThreeRotation(float x, float y, float z) => ThreeSpace.ToUnity(ThreeSpace.Euler(x, y, z));

        private void Update()
        {
            float now = Time.time, dt = Time.deltaTime;
            for (int k = 0; k < 4; k++)
            {
                var list = gifts[k];
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    var g = list[i]; var tr = g.Go.transform;
                    if (g.Phase == Phase.Fly)
                    {
                        float u = (now - g.T0) / g.Duration;
                        tr.position = Position(g, u);
                        g.EmitDue += dt * 60; int emits = Mathf.Min((int)g.EmitDue, 4); g.EmitDue -= emits;
                        if (g.Rocket)
                        {
                            var ahead = Position(g, Mathf.Min(1, u + 0.008f)) - tr.position;
                            float up = Mathf.Clamp01((u - 0.9f) / 0.1f);
                            if (ahead.sqrMagnitude > 1e-10f)
                            {
                                var along = Quaternion.FromToRotation(Vector3.up, ahead.normalized) * Quaternion.AngleAxis(u * 16 * Mathf.Rad2Deg, Vector3.up);
                                tr.rotation = Quaternion.Slerp(along, ThreeRotation(0, g.Yaw, 0), EaseOutCubic(up));
                            }
                            for (int e = 0; e < emits; e++)
                            {
                                fx.SparkAt(tr.position, UnityEngine.Random.value < 0.5f ? TableFx.Hex("#FFB347") : TableFx.Hex("#FF6A3D"));
                                if (UnityEngine.Random.value < 0.5f) fx.SparkAt(tr.position, TableFx.Hex("#FFF1C0"));
                                if (UnityEngine.Random.value < 0.55f) fx.Smoke(tr.position);
                            }
                            tr.localScale = Vector3.one * g.Scale * Mathf.Min(1, 0.4f + u * 6);
                        }
                        else
                        {
                            tr.rotation = ThreeRotation(0, g.Yaw + (1 - EaseIO(Mathf.Clamp01(u))) * Mathf.PI * 3, Mathf.Sin(u * Mathf.PI) * 0.25f);
                            tr.localScale = Vector3.one * g.Scale * (0.35f + 0.65f * Mathf.Min(1, u * 2.2f));
                            for (int e = 0; e < emits; e++) if (UnityEngine.Random.value < 0.35f) fx.SparkAt(tr.position, TableFx.Spark);
                        }
                        if (u >= 1) Land(g);
                    }
                    else if (g.Phase == Phase.Land)
                    {
                        float v = (now - g.Landed) / 0.5f, sq = Mathf.Sin(Mathf.PI * Mathf.Min(1, v)) * (1 - Mathf.Min(1, v));
                        tr.localScale = new Vector3(g.Scale * (1 + 0.18f * sq), g.Scale * (1 - 0.3f * sq), g.Scale * (1 + 0.18f * sq));
                        if (v >= 1) { g.Phase = Phase.Idle; tr.localScale = Vector3.one * g.Scale; }
                    }
                    else if (g.Phase == Phase.Out)
                    {
                        float v = (now - g.Left) / 0.35f;
                        tr.localScale = Vector3.one * g.Scale * Mathf.Max(0.001f, 1 - v) * (1 + Mathf.Sin(v * Mathf.PI) * 0.2f);
                        if (v >= 1) { fx.Burst(new Vector3(tr.position.x, TY + 0.03f, tr.position.z), Color.white, 14, 0.3f); Destroy(g.Go); list.RemoveAt(i); continue; }
                    }
                    if (g.Phase == Phase.Idle || g.Phase == Phase.Land) Idle(g, now, dt);
                    if (g.Rocket && g.Phase == Phase.Fly && g.Flame != null) { float f = 1 + Mathf.Sin(now * 40) * 0.2f; g.Flame.localScale = new Vector3(f, 1 + Mathf.Sin(now * 31) * 0.25f, f); }
                }
            }
        }

        private Vector3 Position(Gift g, float u)
        {
            if (g.Path != null) { float uu = Mathf.Clamp01(u), k = uu * uu * (3 - 2 * uu) * 0.35f + uu * 0.65f; return g.Path.At(Mathf.Clamp01(k)); }
            float e = EaseIO(Mathf.Clamp01(u));
            return Vector3.Lerp(g.Start, g.End, e) + Vector3.up * Mathf.Sin(Mathf.PI * e) * 0.38f;
        }

        private void Land(Gift g)
        {
            g.Phase = Phase.Land; g.Landed = Time.time;
            var tr = g.Go.transform; tr.position = g.End; tr.rotation = ThreeRotation(0, g.Yaw, 0);
            fx.Burst(new Vector3(g.End.x, TY + 0.04f, g.End.z), g.Rocket ? TableFx.Hex("#FFB347") : TableFx.Spark);
            fx.Wave(g.End, TableFx.Gold);
            if (g.Rocket)
            {
                foreach (var part in g.FlameParts) part.SetActive(false);
                fx.Burst(new Vector3(g.End.x, TY + 0.06f, g.End.z), TableFx.Hex("#7FE3FF"), 40, 0.9f);
                fx.Burst(new Vector3(g.End.x, TY + 0.06f, g.End.z), TableFx.Hex("#FF7AD0"), 30, 0.8f);
                fx.Wave(g.End, TableFx.Hex("#7FE3FF"), 0.14f);
                scene.CameraRig.Shake(0.4f);
            }
            Landed?.Invoke(g.Kind, g.From, g.To);
        }

        /// <summary>Idle loops until the hand ends (spec/12 §3.3).</summary>
        private void Idle(Gift g, float t, float dt)
        {
            if (SteamHeight.TryGetValue(g.Kind, out float y) && t >= g.NextSteam)
            {
                // 4 puffs per 1/0.32 s cycle, each rising 7.5 cm with opacity 0.2·sin(π·phase).
                g.NextSteam = t + 1 / 0.32f / 4;
                var at = g.Go.transform.TransformPoint(new Vector3(UnityEngine.Random.Range(-0.004f, 0.004f), y, 0));
                fx.Steam(at);
            }
            if (g.Sway != null) g.Sway.localRotation = ThreeRotation(Mathf.Sin(t * 1.1f + g.Seed) * 0.04f, 0, Mathf.Sin(t * 1.4f + g.Seed) * 0.06f);
            if (g.Flame != null && !g.Rocket) { float f = 1 + Mathf.Sin(t * 23 + g.Seed) * 0.12f + Mathf.Sin(t * 37) * 0.08f; g.Flame.localScale = new Vector3(1, f, 1); }
            if (g.Spin != null) g.Spin.Rotate(Vector3.up, dt * 0.7f * Mathf.Rad2Deg, Space.Self);
            for (int j = 0; j < g.Bubbles.Count; j++)
            {
                var (b, a, r) = g.Bubbles[j]; float ph = Mathf.Repeat(t * 0.25f + j * 0.1f + g.Seed, 1);
                b.localPosition = new Vector3(Mathf.Cos(a + t) * r, 0.018f + ph * 0.078f, Mathf.Sin(a + t) * r); b.localScale = Vector3.one * (0.6f + ph * 0.8f);
            }
            if (g.Rocket) { var p = g.Go.transform.position; p.y = g.End.y + 0.004f + Mathf.Sin(t * 2 + g.Seed) * 0.003f; g.Go.transform.position = p; }
        }

        /// <summary>THREE.CatmullRomCurve3(points, false, 'centripetal').getPointAt(u): arc-length parameterised.</summary>
        private sealed class RocketPath
        {
            private readonly Vector3[] samples; private readonly float[] lengths;

            public RocketPath(Vector3[] p)
            {
                const int perSegment = 60; int segments = p.Length - 1;
                samples = new Vector3[segments * perSegment + 1]; lengths = new float[samples.Length];
                for (int s = 0; s < segments; s++)
                {
                    Vector3 p0 = s == 0 ? 2 * p[0] - p[1] : p[s - 1], p1 = p[s], p2 = p[s + 1], p3 = s + 2 < p.Length ? p[s + 2] : 2 * p[p.Length - 1] - p[p.Length - 2];
                    for (int i = 0; i < perSegment; i++) samples[s * perSegment + i] = Centripetal(p0, p1, p2, p3, i / (float)perSegment);
                }
                samples[samples.Length - 1] = p[p.Length - 1];
                for (int i = 1; i < samples.Length; i++) lengths[i] = lengths[i - 1] + Vector3.Distance(samples[i - 1], samples[i]);
            }

            public Vector3 At(float u)
            {
                float target = u * lengths[lengths.Length - 1];
                int i = Array.BinarySearch(lengths, target); if (i < 0) i = ~i;
                if (i <= 0) return samples[0]; if (i >= samples.Length) return samples[samples.Length - 1];
                float span = lengths[i] - lengths[i - 1];
                return Vector3.Lerp(samples[i - 1], samples[i], span > 0 ? (target - lengths[i - 1]) / span : 0);
            }

            /// <summary>Barry–Goldman evaluation with knot spacing |Δp|^0.5.</summary>
            private static Vector3 Centripetal(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
            {
                float t0 = 0, t1 = t0 + Knot(p0, p1), t2 = t1 + Knot(p1, p2), t3 = t2 + Knot(p2, p3);
                float tt = Mathf.Lerp(t1, t2, t);
                Vector3 a1 = Mix(p0, p1, t0, t1, tt), a2 = Mix(p1, p2, t1, t2, tt), a3 = Mix(p2, p3, t2, t3, tt);
                Vector3 b1 = Mix(a1, a2, t0, t2, tt), b2 = Mix(a2, a3, t1, t3, tt);
                return Mix(b1, b2, t1, t2, tt);
            }

            private static float Knot(Vector3 a, Vector3 b) => Mathf.Max(1e-4f, Mathf.Sqrt(Vector3.Distance(a, b)));
            private static Vector3 Mix(Vector3 a, Vector3 b, float ta, float tb, float t) => tb - ta < 1e-6f ? a : (tb - t) / (tb - ta) * a + (t - ta) / (tb - ta) * b;
        }
    }
}
