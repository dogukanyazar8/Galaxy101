using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static Galaxy101.Table.TableConstants;
using Random = UnityEngine.Random;

namespace Galaxy101.Table
{
    public sealed partial class TableFx
    {
        private enum FinishKind { Fire, Plume, BlackRing, Spiral, Ray, Ring, Flare, Shell, Beam, Core, Implode, Nebula, Roll, Rim, Rock, Ember, Scorch, Meteor, Trail, Spark, Smoke, Shock }
        private sealed class FinishNode
        {
            public FinishKind Kind;
            public Vector3 P, V, C, Direction, Spin, Scale;
            public Color Color;
            public float T0, Life, S0, S1, Op = 1, Max, H, R, A, Len, Rotation, FadeLife = 1;
            public bool Cap, Done;
            public Transform Object;
            public MeshRenderer Renderer;
            public Material Material;
            public FinishBatch Batch;
        }
        private sealed class FinishFlight
        {
            public Transform Tile;
            public Vector3 Scale;
            public FinishFlightMotion Motion;
            public float Start, Emit;
            public string Style;
            public TableCameraRig Rig;
            public Action Flash;
        }
        private readonly struct FinishHop
        {
            public readonly TileView Tile;
            public readonly Vector3 Position;
            public readonly float Start, Amplitude, Duration;
            public readonly bool Owned;
            public FinishHop(TileView tile, float start, float amplitude, float duration, bool owned)
            { Tile = tile; Position = tile.transform.position; Start = start; Amplitude = amplitude; Duration = duration; Owned = owned; }
        }

        private readonly List<FinishNode> finishNodes = new List<FinishNode>(1024);
        private const int FinishCapacity = 4096;
        private readonly Stack<FinishNode> finishPool = new Stack<FinishNode>(FinishCapacity);
        private readonly List<FinishHop> finishHops = new List<FinishHop>(128);
        private readonly List<UnityEngine.Object> finishAssets = new List<UnityEngine.Object>();
        private FinishFlight finishFlight;
        private Transform finishRestingTile;
        private TileView finishPopTile;
        private Vector3 finishPopScale;
        private float finishPopStart;
        private FinishNode steppingFinishNode;
        private FinishBatch finishAdd, finishNormal, finishUntoned, finishStreak;
        private GlowPoints finishBursts;
        private Material finishTemplate, rockTemplate;
        private Texture2D finishRingTexture, finishStreakTexture, finishBeamTexture;
        private Mesh finishPlane, finishBlackRing, finishHemisphere, finishBeam, finishRock, finishDisc, finishRay, finishWideRay, finishShock;
        private Light finishLight;
        private float finishLightStart = -9;
        public int LiveFinishNodes => finishNodes.Count;
        private int LiveFinishShocks
        {
            get
            {
                int count = 0;
                foreach (var node in finishNodes) if (node.Kind == FinishKind.Shock) count++;
                return count;
            }
        }
        private static readonly string[] PurpleColors = { "#B79CFF", "#7FE3FF", "#FFFFFF" }, NovaColors = { "#FFFFFF", "#BFE6FF", "#7FB8FF", "#FFD27A" }, MeteorColors = { "#FFF1C0", "#FFB347", "#FF6A2E", "#FF3B1F" }, SecondaryMeteorColors = { "#FFF1C0", "#FFB347", "#FF6A2E" };
        private static readonly Vector3 FinishCenter = new Vector3(0, TY, 0);
        private static readonly Color PlumeHot = FinishColor("#FFB066"), PlumeCold = FinishColor("#4a4d5e"), RockHot = FinishColor("#FF7A1E");
        private static readonly int FinishTintId = Shader.PropertyToID("_BaseColor"), FinishEmissionId = Shader.PropertyToID("_EmissionColor");

        /// <summary>Launches the selected source finish. The target is independent of the stock position.</summary>
        public float Finish(string style, Transform tile, TableCameraRig rig, Action onImpact)
        {
            if (tile == null) throw new ArgumentNullException(nameof(tile));
            EnsureFinish();
            if (finishFlight != null && finishFlight.Tile != null) finishFlight.Tile.localScale = finishFlight.Scale;
            finishFlight = new FinishFlight { Tile = tile, Scale = tile.localScale, Motion = new FinishFlightMotion(new Pose(tile.position, tile.rotation), style), Start = Time.time, Style = FinishFlightMotion.Style(style), Rig = rig, Flash = onImpact };
            return finishFlight.Motion.Duration;
        }

        /// <summary>Compatibility with existing callers; all finishes now use the source's fixed-centre flight.</summary>
        public float Nuclear(Transform tile, Vector3 impact, TableCameraRig rig, Action onImpact) => Finish("nuke", tile, rig, onImpact);

        /// <summary>Source FIN_FX dispatch, also used by the effect previews and runtime verification.</summary>
        public void FinishImpact(string style, TableCameraRig rig = null, Action onFlash = null)
        {
            EnsureFinish();
            switch (FinishFlightMotion.Style(style))
            {
                case "blackhole": BlackholeFinish(rig, onFlash); break;
                case "supernova": SupernovaFinish(rig, onFlash); break;
                case "meteor": MeteorFinish(rig, onFlash); break;
                default: NukeFinish(rig, onFlash); break;
            }
        }

        private static Color FinishColor(string hex) => Hex(hex).linear;
        private static Vector3 FinishAt(float y) => new Vector3(0, TY + y, 0);
        private static Vector3 Offset(Vector3 p, float x, float y, float z) => p + ThreeSpace.ToUnity(x, y, z);
        private T FinishOwn<T>(T asset) where T : UnityEngine.Object { finishAssets.Add(asset); return asset; }

        private FinishNode Sprite(FinishKind kind, Vector3 at, string color, float life, FinishBatch batch = null, float delay = 0)
        {
            var node = TakeFinishNode(); node.Kind = kind; node.P = at; node.C = FinishCenter; node.Color = FinishColor(color); node.Life = life; node.T0 = Time.time + delay; node.Batch = batch ?? finishAdd;
            finishNodes.Add(node); return node;
        }

        private FinishNode Geometry(FinishKind kind, Vector3 at, Mesh mesh, string color, float life, Texture texture = null, bool normal = false, bool untoned = true, float delay = 0)
        {
            var go = new GameObject("finish_" + kind.ToString().ToLowerInvariant()); go.transform.SetParent(transform, false); go.transform.position = at;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>(); renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
            var material = FinishMaterial(normal, untoned, texture); renderer.sharedMaterial = material; renderer.enabled = false;
            var node = TakeFinishNode(); node.Kind = kind; node.P = at; node.C = FinishCenter; node.Color = FinishColor(color); node.Life = life; node.T0 = Time.time + delay; node.Object = go.transform; node.Renderer = renderer; node.Material = material;
            finishNodes.Add(node); return node;
        }

        private FinishNode TakeFinishNode()
        {
            // One authored finish peaks below this bound; repeated preview requests recycle the oldest transient.
            if (finishNodes.Count == FinishCapacity)
            {
                int oldest = ReferenceEquals(finishNodes[0], steppingFinishNode) ? 1 : 0;
                ReleaseFinishNode(finishNodes[oldest]); finishNodes.RemoveAt(oldest);
            }
            var n = finishPool.Pop();
            n.P = n.V = n.C = n.Direction = n.Spin = n.Scale = Vector3.zero; n.Color = Color.white;
            n.T0 = n.Life = n.S0 = n.S1 = n.Max = n.H = n.R = n.A = n.Len = n.Rotation = 0;
            n.Op = n.FadeLife = 1; n.Cap = n.Done = false;
            return n;
        }

        private void Fire(float y, string color, float life, float max, float opacity)
        { var n = Sprite(FinishKind.Fire, FinishAt(y), color, life); n.Max = max; n.Op = opacity; }
        private void Ring2(Vector3 center, string color, float life, float max, float opacity = 1, float delay = 0, float height = .004f)
        { var at = center; at.y = TY + height; var n = Geometry(FinishKind.Ring, at, finishPlane, color, life, finishRingTexture, delay: delay); n.Max = max; n.Op = opacity; }
        private void Flare(float y, string color, float life, float width, float height, float opacity = 1)
        { var n = Sprite(FinishKind.Flare, FinishAt(y), color, life, finishStreak); n.S0 = width; n.H = height; n.Op = opacity; }
        private void Shell(string color, float life, float max, float opacity)
        { var n = Geometry(FinishKind.Shell, FinishCenter, finishHemisphere, color, life); n.Max = max; n.Op = opacity; n.Material.SetFloat("_Cull", (float)CullMode.Front); }
        private void FinishTrail(Vector3 at, string color, float size, float opacity)
        { var n = Sprite(FinishKind.Trail, at, color, .38f); n.S0 = size; n.Op = opacity; }
        private void FinishSpark(Vector3 at, string color) => Sprite(FinishKind.Spark, at, color, .5f);
        private void FinishSmoke(Vector3 at)
        { Sprite(FinishKind.Smoke, at + FinishJitter(.02f), "#C9D0E6", 1.3f, finishNormal); }

        private void FinishBurst(Vector3 position, Color color, int count, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = Random.value * 6.283f, elevation = Random.value * 1.2f + .2f;
                var velocity = ThreeSpace.ToUnity(new Vector3(Mathf.Cos(angle) * Mathf.Cos(elevation), Mathf.Sin(elevation) * 1.3f, Mathf.Sin(angle) * Mathf.Cos(elevation)) * (speed * (.5f + Random.value)));
                finishBursts.Emit(position, velocity, color.linear, .022f, .9f);
            }
        }

        private void FinishShock(Vector3 position, Color color, float life, float maxScale)
        {
            var node = Geometry(FinishKind.Shock, position + Vector3.up * .003f, finishShock, "#FFFFFF", life, untoned: false);
            node.Color = color.linear; node.Max = maxScale; node.Object.localScale = Vector3.one * .02f;
        }

        private void SetFinishLight(string color, float height, bool flash, TableCameraRig rig, float shake, Action onFlash)
        {
            finishLight.color = Hex(color); finishLight.transform.position = FinishAt(height);
            if (flash) { finishLightStart = Time.time; onFlash?.Invoke(); }
            rig?.Shake(shake);
        }

        private void NukeFinish(TableCameraRig rig, Action flash)
        {
            SetFinishLight("#FFB25A", .3f, true, rig, 1.2f, flash);
            Fire(.05f, "#FFFFFF", .55f, .9f, 1); Fire(.06f, "#FFD58A", 1.2f, 1.4f, 1); Fire(.08f, "#FF6A2E", 1.8f, 1.1f, .85f);
            FinishShock(FinishCenter, Color.white, .7f, 1.5f); After(.11f, () => FinishShock(FinishCenter, Gold, 1, 1.25f)); After(.24f, () => FinishShock(FinishCenter, Accent, 1.3f, 1));
            FinishBurst(FinishAt(.04f), Hex("#FFE08A"), 140, 1.4f); FinishBurst(FinishAt(.04f), Hex("#FF7A3D"), 90, 1); FinishBurst(FinishAt(.05f), Color.white, 50, 1.8f);
            for (int i = 0; i < 34; i++)
            {
                bool cap = i >= 14; float a = Random.value * 6.283f, r = cap ? .03f + Random.value * .05f : Random.value * .025f;
                var n = Sprite(FinishKind.Plume, Offset(FinishCenter, Mathf.Cos(a) * r, .02f + Random.value * .02f, Mathf.Sin(a) * r), "#FFB066", 2.6f + Random.value * .9f, finishNormal, (cap ? .08f : 0) + Random.value * .15f);
                n.V = ThreeSpace.ToUnity(Mathf.Cos(a) * (cap ? .1f : .02f), cap ? .34f + Random.value * .12f : .12f + Random.value * .28f, Mathf.Sin(a) * (cap ? .1f : .02f)); n.Cap = cap; n.S0 = .05f; n.S1 = cap ? .24f + Random.value * .1f : .12f + Random.value * .06f;
            }
            HopFinish(.55f); deckSpinT = Time.time; holoBoostUntil = Time.time + 3.2f;
        }

        private void BlackholeFinish(TableCameraRig rig, Action flash)
        {
            SetFinishLight("#9B7BFF", .25f, false, rig, .6f, null);
            // Source bhcore fx.push is commented out; its opacity stays zero. Do not invent a visible core.
            Geometry(FinishKind.BlackRing, FinishAt(.0018f), finishBlackRing, "#B79CFF", 1.6f, glow, untoned: false);
            for (int i = 0; i < 80; i++)
            {
                var n = Sprite(FinishKind.Spiral, FinishCenter, PurpleColors[i % 3], .9f + Random.value * .4f, delay: Random.value * .5f);
                n.A = Random.value * 6.283f; n.R = .22f + Random.value * .5f; n.H = .01f + Random.value * .07f;
            }
            After(1.15f, () =>
            {
                SetFinishLight("#9B7BFF", .25f, true, rig, .9f, flash);
                Fire(.05f, "#EDE6FF", .55f, 1, 1); Fire(.06f, "#7F6BFF", 1.3f, 1.3f, .8f);
                FinishShock(FinishCenter, Color.white, .6f, 1.4f); FinishShock(FinishCenter, Hex("#B79CFF"), 1, 1.15f); After(.12f, () => FinishShock(FinishCenter, Hex("#7FE3FF"), 1.2f, .95f));
                FinishBurst(FinishAt(.04f), Hex("#B79CFF"), 110, 1.2f); FinishBurst(FinishAt(.04f), Hex("#7FE3FF"), 60, .9f);
                HopFinish(.6f); holoBoostUntil = Time.time + 3;
            });
            deckSpinT = Time.time;
        }

        private void SupernovaFinish(TableCameraRig rig, Action flash)
        {
            SetFinishLight("#CFEBFF", .35f, false, rig, .5f, null);
            Sprite(FinishKind.Core, FinishAt(.07f), "#DDF2FF", .62f, finishUntoned);
            for (int i = 0; i < 90; i++)
            {
                var n = Sprite(FinishKind.Implode, FinishAt(.07f), i % 3 == 0 ? "#FFFFFF" : i % 3 == 1 ? "#BFE6FF" : "#8FB8FF", .6f * (.55f + Random.value * .3f), finishUntoned, Random.value * .6f * .35f);
                float a = Random.value * 6.283f, e = Random.value * 1.2f;
                n.C = FinishAt(.07f); n.Direction = ThreeSpace.ToUnity(Mathf.Cos(a) * Mathf.Cos(e), Mathf.Sin(e) * .8f, Mathf.Sin(a) * Mathf.Cos(e)); n.R = .3f + Random.value * .45f;
            }
            After(.56f, () =>
            {
                SetFinishLight("#CFEBFF", .35f, true, rig, 1.2f, flash);
                Fire(.08f, "#FFFFFF", .55f, 2.4f, 1); Fire(.08f, "#9FD8FF", 1.5f, 3, .75f); Fire(.1f, "#FFD27A", 1.1f, 1.5f, .45f);
                Flare(.09f, "#CFEBFF", 1.1f, 3, .14f); Flare(.09f, "#FFFFFF", .6f, 1.8f, .045f);
                Shell("#9FD8FF", 1, .95f, .5f); Shell("#FFFFFF", .55f, .55f, .7f);
                Ring2(FinishCenter, "#FFFFFF", .85f, 2); Ring2(FinishCenter, "#7FD0FF", 1.25f, 1.55f, .95f, .07f); Ring2(FinishCenter, "#FFD27A", 1.6f, 1.15f, .8f, .16f); Ring2(FinishCenter, "#B79CFF", 3.2f, .85f, .42f, .3f);
                for (int i = 0; i < 28; i++)
                {
                    var n = Geometry(FinishKind.Ray, FinishAt(.008f), i % 3 == 0 ? finishWideRay : finishRay, i % 2 == 0 ? "#FFFFFF" : "#9FD8FF", 1.3f, finishStreakTexture, delay: i * .008f);
                    n.Object.rotation = Quaternion.AngleAxis((i / 28f * 6.283f + Random.value * .15f) * Mathf.Rad2Deg, Vector3.up); n.Len = .7f + Random.value * .7f; n.Rotation = -(Random.value - .5f) * .25f;
                }
                var beam = Geometry(FinishKind.Beam, FinishCenter, finishBeam, "#BFE6FF", 1.5f, finishBeamTexture); beam.H = 1.7f;
                Embers(FinishCenter, NovaColors, 280, 1.6f, 1.7f, .017f, .9f);
                for (int i = 0; i < 10; i++)
                {
                    float a = Random.value * 6.283f, r = Random.value * .12f;
                    var n = Sprite(FinishKind.Nebula, Offset(FinishCenter, Mathf.Cos(a) * r, .06f + Random.value * .08f, Mathf.Sin(a) * r), i % 3 == 0 ? "#5B7BFF" : i % 3 == 1 ? "#B06BFF" : "#4FD8FF", 2.4f + Random.value, delay: .1f + Random.value * .25f);
                    n.S0 = .15f; n.S1 = .5f + Random.value * .4f; n.Op = .16f + Random.value * .1f; n.V = ThreeSpace.ToUnity(Mathf.Cos(a) * .05f, .02f, Mathf.Sin(a) * .05f);
                }
                HopFinish(.6f); deckSpinT = Time.time; holoBoostUntil = Time.time + 3.4f;
            });
        }

        private void MeteorFinish(TableCameraRig rig, Action flash)
        {
            SetFinishLight("#FF8A3A", .25f, true, rig, 1.35f, flash);
            Fire(.05f, "#FFFFFF", .35f, 1, 1); Fire(.06f, "#FFB347", 1, 1.6f, .9f); Fire(.07f, "#FF5A1F", 1.8f, 1.15f, .6f);
            Flare(.05f, "#FFC27A", .8f, 2, .11f, .95f);
            Ring2(FinishCenter, "#FFF1C0", .6f, 1.35f); Ring2(FinishCenter, "#FF8A3D", 1.1f, 1.05f, .9f, .06f); Ring2(FinishCenter, "#FF4A1A", 1.6f, .8f, .7f, .14f);
            Rocks(FinishCenter, 26); Embers(FinishCenter, MeteorColors, 260, 1.25f, 2.2f, .018f);
            for (int i = 0; i < 28; i++)
            {
                float a = i / 28f * 6.283f + Random.value * .3f;
                var n = Sprite(FinishKind.Roll, Offset(FinishCenter, Mathf.Cos(a) * .04f, .015f, Mathf.Sin(a) * .04f), "#D8C2AE", 1.6f + Random.value * .7f, finishNormal, Random.value * .08f);
                n.V = ThreeSpace.ToUnity(Mathf.Cos(a), .04f, Mathf.Sin(a)) * (.55f + Random.value * .35f); n.S1 = .12f + Random.value * .08f;
            }
            Fire(.03f, "#FF7A2E", 2.6f, .45f, .7f);
            var scorch = Geometry(FinishKind.Scorch, FinishAt(.0015f), finishDisc, "#120604", 3.4f, glow, true, false); scorch.Op = .85f; scorch.Object.localScale = Vector3.one * .24f;
            Geometry(FinishKind.Rim, FinishAt(.0025f), finishPlane, "#FF6A1A", 2.6f, finishRingTexture);
            for (int i = 0; i < 7; i++)
            {
                float a = Random.value * 6.283f, r = .22f + Random.value * .42f;
                var end = Offset(FinishCenter, Mathf.Cos(a) * r, .004f, Mathf.Sin(a) * r);
                var n = Sprite(FinishKind.Meteor, Offset(end, -.4f, 1, -.3f), "#FFD27A", .5f, finishUntoned, .2f + i * .13f); n.C = end; n.Direction = n.P; n.S0 = .07f;
            }
            HopFinish(.6f); deckSpinT = Time.time; holoBoostUntil = Time.time + 2.8f;
        }

        private void Embers(Vector3 center, string[] colors, int count, float speed, float life, float size, float up = 1)
        {
            for (int i = 0; i < count; i++)
            {
                float a = Random.value * 6.283f, e = Random.value * 1.25f + .1f, k = speed * (.25f + Random.value * .9f);
                var n = Sprite(FinishKind.Ember, new Vector3(center.x, TY + .02f, center.z), colors[i % colors.Length], life, finishUntoned);
                n.V = ThreeSpace.ToUnity(Mathf.Cos(a) * Mathf.Cos(e) * k, Mathf.Sin(e) * k * up, Mathf.Sin(a) * Mathf.Cos(e) * k); n.FadeLife = .45f + Random.value * .55f; n.S0 = size;
            }
        }

        private void Rocks(Vector3 center, int count, float speed = 1)
        {
            for (int i = 0; i < count; i++)
            {
                float r = .006f + Random.value * .012f, a = Random.value * 6.283f, k = (.35f + Random.value * .7f) * speed;
                var n = Geometry(FinishKind.Rock, Offset(new Vector3(center.x, TY, center.z), Mathf.Cos(a) * .02f, .02f, Mathf.Sin(a) * .02f), finishRock, "#2b1d16", 2.2f + Random.value * .8f);
                Destroy(n.Material); n.Material = new Material(rockTemplate); n.Renderer.sharedMaterial = n.Material; n.R = r;
                n.V = ThreeSpace.ToUnity(Mathf.Cos(a) * k, .5f + Random.value * speed, Mathf.Sin(a) * k);
                n.Spin = new Vector3(Random.value * 14 - 7, Random.value * 14 - 7, Random.value * 14 - 7);
            }
        }

        private void HopFinish(float duration)
        {
            finishHops.Clear();
            foreach (var tile in FindObjectsByType<TileView>())
            {
                if (tile == null) continue;
                bool owned = tile.transform == finishRestingTile;
                if (!owned && (tile.Tweening || !tile.enabled)) continue;
                float d = Vector3.Distance(tile.transform.position, FinishCenter);
                finishHops.Add(new FinishHop(tile, Time.time + d * .35f, .012f + .05f * Mathf.Max(0, 1 - d / 1.1f), duration, owned));
            }
        }

        private void LateUpdate()
        {
            for (int i = finishHops.Count - 1; i >= 0; i--)
            {
                var h = finishHops[i];
                if (h.Tile == null) { finishHops.RemoveAt(i); continue; }
                bool owned = h.Owned && !h.Tile.enabled;
                if (!owned && (h.Tile.Tweening || !h.Tile.enabled || (h.Tile.HasTarget && (h.Tile.Target.position - h.Position).sqrMagnitude > 1e-8f))) { finishHops.RemoveAt(i); continue; }
                float u = (Time.time - h.Start) / h.Duration;
                if (u < 0) continue;
                h.Tile.transform.position = h.Position + Vector3.up * (u >= 1 ? 0 : Mathf.Sin(Mathf.PI * u) * h.Amplitude);
                if (u >= 1) finishHops.RemoveAt(i);
            }
            if (finishPopTile != null)
            {
                float u = (Time.time - finishPopStart) / .34f;
                finishPopTile.transform.localScale = finishPopScale * (u >= 1 ? 1 : 1 + Mathf.Sin(Mathf.PI * u) * (1 - u) * .05f);
                if (u >= 1) finishPopTile = null;
            }
        }

        private void StepFinish(float now, float dt, Camera camera)
        {
            if (finishAdd == null) return;
            finishBursts.Step(dt, camera);
            if (finishFlight != null)
            {
                var flight = finishFlight;
                if (flight.Tile == null) finishFlight = null;
                else
                {
                    float elapsed = now - flight.Start, u = Mathf.Clamp01(elapsed / flight.Motion.Duration);
                    var frame = flight.Motion.Sample(elapsed, new Vector2(Random.value - .5f, Random.value - .5f));
                    flight.Tile.SetPositionAndRotation(frame.Pose.position, frame.Pose.rotation); flight.Tile.localScale = flight.Scale * frame.Scale;
                    flight.Emit += dt * 60;
                    int emit = Mathf.Min(12, (int)flight.Emit); flight.Emit -= emit;
                    for (int i = 0; i < emit && u < 1; i++) EmitFinishFlight(flight.Style, u, frame.Pose.position);
                    if (u >= 1)
                    {
                        finishFlight = null; finishRestingTile = flight.Tile;
                        finishPopTile = flight.Tile.GetComponent<TileView>(); finishPopStart = now; finishPopScale = flight.Scale;
                        FinishImpact(flight.Style, flight.Rig, flight.Flash);
                    }
                }
            }
            finishAdd.Begin(); finishNormal.Begin(); finishUntoned.Begin(); finishStreak.Begin();
            for (int i = finishNodes.Count - 1; i >= 0; i--)
            {
                var n = finishNodes[i]; float u = (now - n.T0) / n.Life;
                if (u >= 1) { ReleaseFinishNode(n); finishNodes.RemoveAt(i); continue; }
                if (u < 0) continue;
                steppingFinishNode = n;
                StepFinishNode(n, u, now, dt, camera);
                steppingFinishNode = null;
            }
            finishAdd.Flush(camera); finishNormal.Flush(camera); finishUntoned.Flush(camera); finishStreak.Flush(camera);
            if (camera.TryGetComponent<ThreeToneMapping>(out var tone)) tone.EncodedFinishBlending = finishNodes.Count > 0 || finishBursts.Count > 0;
            float lightAge = now - finishLightStart;
            finishLight.intensity = lightAge >= 0 && lightAge < 2 ? TableScene.ThreeLocalIntensity(90, 1.4f, finishLight.transform.position.y - TY) * Mathf.Exp(-3 * lightAge) : 0;
        }

        private void EmitFinishFlight(string style, float u, Vector3 p)
        {
            if (style == "meteor")
            {
                if (u < .32f) { if (Random.value < .6f) FinishSpark(p, "#FFE08A"); }
                else if (u >= .5f)
                {
                    for (int i = 0; i < 2; i++) FinishSpark(p + FinishJitter(.02f), Random.value < .5f ? "#FF7A3D" : "#FFD27A");
                    FinishTrail(p, "#FF9A4A", .16f, .55f); FinishTrail(p, "#FFF1C0", .06f, .9f);
                    if (Random.value < .9f) FinishSmoke(p);
                }
            }
            else if (u < .46f)
            {
                if (Random.value < .5f) FinishSpark(p, style == "blackhole" ? "#B79CFF" : style == "supernova" ? "#FFFFFF" : "#FFE08A");
            }
            else if (u < .62f) FinishSpark(p + FinishJitter(.08f), Random.value < .5f ? "#FFF1C0" : "#FF8A3D");
            else
            {
                FinishSpark(p, style == "blackhole" ? "#7FE3FF" : style == "supernova" ? "#9FD8FF" : "#FFB347"); FinishSpark(p, "#FFF1C0");
                if (style == "nuke" && Random.value < .8f) FinishSmoke(p);
            }
        }

        private static Vector3 FinishJitter(float size) => ThreeSpace.ToUnity(Random.value - .5f, Random.value - .5f, Random.value - .5f) * size;
        private static float FinishSmoother(float t) { t = Mathf.Clamp01(t); return t * t * t * (t * (t * 6 - 15) + 10); }

        private void StepFinishNode(FinishNode n, float u, float now, float dt, Camera camera)
        {
            float e = EaseOutCubic(u), alpha = 1; var scale = Vector3.one; var color = n.Color;
            switch (n.Kind)
            {
                case FinishKind.Shock: scale *= .02f + e * n.Max; alpha = .95f * Mathf.Pow(1 - u, 1.4f); break;
                case FinishKind.Fire: scale *= .04f + (1 - Mathf.Pow(1 - u, 4)) * n.Max; alpha = n.Op * Mathf.Pow(1 - u, 1.6f); break;
                case FinishKind.Plume:
                    n.P += n.V * dt; n.V *= n.Cap ? .982f : .975f; if (n.Cap) { n.V.x *= .99f; n.V.z *= .99f; }
                    scale *= n.S0 + (1 - (1 - u) * (1 - u)) * n.S1;
                    color = Color.Lerp(PlumeHot, PlumeCold, Mathf.Min(1, u * 2.2f));
                    alpha = .62f * Mathf.Pow(Mathf.Max(0, Mathf.Sin(Mathf.PI * Mathf.Min(1, u * 1.05f))), .7f); break;
                case FinishKind.BlackRing:
                    float g = u < .3f ? FinishSmoother(u / .3f) : u < .68f ? 1 : 1 - FinishSmoother((u - .68f) / .12f);
                    scale *= Mathf.Max(.001f, .26f * g * (1 + .05f * Mathf.Sin(now * 9))); n.Object.Rotate(Vector3.up, dt * 6 * Mathf.Rad2Deg, Space.World); alpha = .85f * g; break;
                case FinishKind.Spiral:
                    float rr = n.R * (1 - e), angle = n.A + e * 7;
                    n.P = Offset(n.C, Mathf.Cos(angle) * rr, .012f + n.H * (1 - e), Mathf.Sin(angle) * rr); scale *= .022f * (1 - e * .6f); alpha = Mathf.Sin(Mathf.PI * u) * .9f; break;
                case FinishKind.Ray:
                    scale = new Vector3(n.Len * EaseOutCubic(Mathf.Min(1, u * 1.6f)), 1, 1 + u * 1.5f); alpha = .9f * Mathf.Pow(1 - u, 1.5f);
                    n.Object.Rotate(Vector3.up, n.Rotation * dt * Mathf.Rad2Deg, Space.World); break;
                case FinishKind.Ring: scale *= .01f + e * n.Max; alpha = n.Op * Mathf.Pow(1 - u, 1.3f); break;
                case FinishKind.Flare: scale = new Vector3(.05f + n.S0 * e, n.H * (1 - u * .5f), 1); alpha = n.Op * (1 - u) * (1 - u); break;
                case FinishKind.Shell: float shell = n.Max * (1 - Mathf.Pow(1 - u, 2.4f)); scale = new Vector3(shell, shell * .55f, shell); alpha = n.Op * Mathf.Pow(1 - u, 1.8f); break;
                case FinishKind.Beam:
                    float width = .02f + .05f * Mathf.Sin(Mathf.PI * Mathf.Min(1, u * 1.2f)); scale = new Vector3(width, .01f + n.H * EaseOutCubic(Mathf.Min(1, u * 2.5f)), width); alpha = .85f * Mathf.Pow(1 - u, 1.4f); break;
                case FinishKind.Core: scale *= .03f + Mathf.Pow(u, 1.6f) * .16f + Mathf.Sin(now * 60) * .008f * u; alpha = Mathf.Min(1, u * 1.6f); break;
                case FinishKind.Implode: float collapse = Mathf.Pow(u, 2.2f); n.P = n.C + n.Direction * (n.R * (1 - collapse)); scale *= .012f + .012f * collapse; alpha = Mathf.Sin(Mathf.PI * u) * .95f; break;
                case FinishKind.Nebula: n.P += n.V * dt; scale *= n.S0 + e * n.S1; alpha = n.Op * Mathf.Pow(Mathf.Max(0, Mathf.Sin(Mathf.PI * u)), .8f); break;
                case FinishKind.Roll:
                    n.P += n.V * dt; n.V *= Mathf.Pow(.12f, dt); scale *= .04f + e * n.S1; alpha = .55f * Mathf.Sin(Mathf.PI * Mathf.Min(1, u * 1.3f + .08f)) * (1 - u); break;
                case FinishKind.Rim: scale *= .12f + .05f * EaseOutCubic(Mathf.Min(1, u * 3)); alpha = 1.2f * Mathf.Pow(1 - u, 1.2f) * (.85f + .15f * Mathf.Sin(now * 23)); break;
                case FinishKind.Rock:
                    n.V.y -= 3.2f * dt; n.P += n.V * dt; n.Direction += n.Spin * dt;
                    if (n.P.y < TY + n.R) { n.P.y = TY + n.R; n.V.y = Mathf.Abs(n.V.y) * .32f; n.V.x *= .62f; n.V.z *= .62f; n.Spin *= .6f; }
                    n.Object.rotation = ThreeSpace.ToUnity(ThreeSpace.Euler(n.Direction.x, n.Direction.y, n.Direction.z));
                    n.Material.SetColor(FinishEmissionId, Color.Lerp(RockHot, Color.black, Mathf.Min(1, u * .9f)) * (3 * (1 - Mathf.Min(1, u * .95f))));
                    scale *= n.R * (u > .8f ? 1 - (u - .8f) / .2f : 1); break;
                case FinishKind.Ember:
                    n.V *= Mathf.Pow(.35f, dt); n.V.y -= .9f * dt; n.P += n.V * dt;
                    if (n.P.y < TY + .003f) { n.P.y = TY + .003f; n.V.y = Mathf.Abs(n.V.y) * .25f; }
                    float cool = Mathf.Max(0, 1 - u / n.FadeLife); color = new Color(color.r * cool, color.g * cool * cool, color.b * cool * cool * cool, 1);
                    // THREE.PointsMaterial scales by viewportHeight/2, while world-space sprites also include fov.
                    scale *= n.S0 * (camera != null ? Mathf.Tan(camera.fieldOfView * .5f * Mathf.Deg2Rad) : Mathf.Tan(20 * Mathf.Deg2Rad)); break;
                case FinishKind.Scorch: scale *= .24f; alpha = n.Op * Mathf.Min(1, u * 8) * (1 - FinishSmoother(Mathf.Max(0, (u - .5f) / .5f))); break;
                case FinishKind.Meteor:
                    n.P = Vector3.Lerp(n.Direction, n.C, u * u); scale *= .07f;
                    n.A += dt * 60; int trails = Mathf.Min(12, (int)n.A); n.A -= trails;
                    for (int i = 0; i < trails; i++) { FinishTrail(n.P, "#FF8A3D", .09f, .55f); FinishTrail(n.P, "#FFF1C0", .035f, .85f); }
                    if (u > .92f && !n.Done)
                    {
                        n.Done = true; Embers(n.C, SecondaryMeteorColors, 40, .55f, .9f, .012f); Ring2(n.C, "#FF8A3D", .6f, .3f, .9f); Rocks(n.C, 3, .5f); DiceDust(n.C, 5, .1f);
                    }
                    break;
                case FinishKind.Trail: scale *= n.S0 * (.35f + .65f * (1 - u)); alpha = n.Op * (1 - u) * (1 - u); break;
                case FinishKind.Smoke: n.P.y += dt * .03f; scale *= .02f + e * .11f; alpha = .32f * (1 - u); break;
                default: scale *= .03f * (1 - u * .6f); alpha = .8f * (1 - u); break;
            }
            color.a = Mathf.Clamp01(alpha);
            if (n.Object != null)
            {
                n.Object.position = n.P; n.Object.localScale = scale; n.Renderer.enabled = true;
                if (n.Kind != FinishKind.Rock) n.Material.SetVector(FinishTintId, color);
            }
            else n.Batch.Add(n.P, new Vector2(scale.x, scale.y), color);
        }

        private void ReleaseFinishNode(FinishNode n)
        {
            if (n.Object != null) Destroy(n.Object.gameObject);
            if (n.Material != null) Destroy(n.Material);
            n.Object = null; n.Renderer = null; n.Material = null; n.Batch = null;
            finishPool.Push(n);
        }

        private void ReleaseFinish()
        {
            if (scene != null && scene.CameraRig != null && scene.CameraRig.Camera.TryGetComponent<ThreeToneMapping>(out var tone)) tone.EncodedFinishBlending = false;
            if (finishFlight?.Tile != null) finishFlight.Tile.localScale = finishFlight.Scale;
            if (finishPopTile != null) finishPopTile.transform.localScale = finishPopScale;
            finishPopTile = null; finishRestingTile = null;
            finishFlight = null;
            foreach (var hop in finishHops) if (hop.Tile != null && !hop.Tile.Tweening && (!hop.Tile.HasTarget || (hop.Tile.Target.position - hop.Position).sqrMagnitude < 1e-8f)) hop.Tile.transform.position = hop.Position;
            finishHops.Clear();
            foreach (var node in finishNodes) ReleaseFinishNode(node); finishNodes.Clear();
            finishAdd?.Release(); finishNormal?.Release(); finishUntoned?.Release(); finishStreak?.Release();
            finishBursts?.Release();
            foreach (var asset in finishAssets) if (asset != null) Destroy(asset); finishAssets.Clear();
            finishPool.Clear();
            if (finishLight != null) Destroy(finishLight.gameObject);
        }

        private Material FinishMaterial(bool normal, bool untoned, Texture texture)
        {
            var material = new Material(finishTemplate) { name = normal ? "finish_normal" : "finish_additive", renderQueue = (int)RenderQueue.Transparent };
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); material.SetFloat("_DstBlend", (float)(normal ? BlendMode.OneMinusSrcAlpha : BlendMode.One));
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); material.SetFloat("_DstBlendAlpha", (float)(normal ? BlendMode.OneMinusSrcAlpha : BlendMode.One));
            material.SetFloat("_BypassToneMapping", untoned ? 1 : 0); material.SetFloat("_Galaxy101Exposure", ThreeToneMapping.DesignExposure);
            material.SetTexture("_BaseMap", texture != null ? texture : Texture2D.whiteTexture); return material;
        }

        private void EnsureFinish()
        {
            if (finishAdd != null) return;
            finishTemplate = Resources.Load<Material>("Galaxy101/Table/Fx/fx_finish");
            rockTemplate = Resources.Load<Material>("Galaxy101/Table/Fx/fx_rock");
            if (finishTemplate == null || rockTemplate == null) throw new InvalidOperationException("Missing authored Galaxy101 finish material resources.");
            for (int i = 0; i < FinishCapacity; i++) finishPool.Push(new FinishNode());
            finishRingTexture = FinishOwn(FinishTexture("finish_ring", 256, 256, (x, y) =>
            {
                float r = Mathf.Sqrt((x - .5f) * (x - .5f) + (y - .5f) * (y - .5f)) * 2;
                return r < .6f ? 0 : r < .8f ? (r - .6f) / .2f : r < .88f ? Mathf.Lerp(1, .4f, (r - .8f) / .08f) : r < 1 ? .4f * (1 - (r - .88f) / .12f) : 0;
            }));
            finishStreakTexture = FinishOwn(FinishTexture("finish_streak", 512, 64, (x, y) => (1 - Mathf.Abs(x * 2 - 1)) * (1 - Mathf.Abs(y * 2 - 1))));
            finishBeamTexture = FinishOwn(FinishTexture("finish_beam", 8, 256, (x, y) => y < .35f ? Mathf.Lerp(1, .5f, y / .35f) : Mathf.Lerp(.5f, 0, (y - .35f) / .65f)));
            finishPlane = FinishOwn(FinishQuad(-1, 1, 2)); finishRay = FinishOwn(FinishQuad(0, 1, .035f)); finishWideRay = FinishOwn(FinishQuad(0, 1, .07f));
            finishShock = FinishOwn(Ring(.9f, 1, 72)); FinishWhite(finishShock);
            finishBlackRing = FinishOwn(Ring(.72f, 1, 72));
            var ringUv = new Vector2[finishBlackRing.vertexCount]; var ringVertices = finishBlackRing.vertices;
            for (int i = 0; i < ringUv.Length; i++) ringUv[i] = new Vector2(ringVertices[i].x * .5f + .5f, ringVertices[i].z * .5f + .5f);
            finishBlackRing.uv = ringUv; FinishWhite(finishBlackRing);
            finishDisc = FinishOwn(Disc(1, 48)); var discUv = new Vector2[finishDisc.vertexCount]; var discVertices = finishDisc.vertices;
            for (int i = 0; i < discUv.Length; i++) discUv[i] = new Vector2(discVertices[i].x * .5f + .5f, discVertices[i].z * .5f + .5f);
            finishDisc.uv = discUv; FinishWhite(finishDisc);
            finishHemisphere = FinishOwn(FinishDome()); finishBeam = FinishOwn(FinishCylinder()); finishRock = FinishOwn(FinishIcosahedron());
            finishAdd = new FinishBatch(transform, "finish_sprites", FinishMaterial(false, false, glow), false);
            var burstObject = FinishOwn(new GameObject("finish_bursts")); burstObject.transform.SetParent(transform, false);
            var burstFilter = burstObject.AddComponent<MeshFilter>(); var burstRenderer = burstObject.AddComponent<MeshRenderer>();
            burstRenderer.sharedMaterial = FinishMaterial(false, false, glow); burstRenderer.shadowCastingMode = ShadowCastingMode.Off; burstRenderer.receiveShadows = false;
            finishBursts = new GlowPoints(burstFilter, 1.4f, false, pointSized: true);
            finishNormal = new FinishBatch(transform, "finish_dust", FinishMaterial(true, false, glow), true);
            finishUntoned = new FinishBatch(transform, "finish_untoned", FinishMaterial(false, true, glow), false);
            finishStreak = new FinishBatch(transform, "finish_flares", FinishMaterial(false, true, finishStreakTexture), false);
            var lightGo = new GameObject("finish_light"); lightGo.transform.SetParent(transform, false);
            finishLight = lightGo.AddComponent<Light>(); finishLight.type = LightType.Point; finishLight.range = 4.5f; finishLight.intensity = 0; finishLight.shadows = LightShadows.None;
        }

        private static Texture2D FinishTexture(string name, int width, int height, Func<float, float, float> alpha)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++) pixels[y * width + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha((x + .5f) / width, (y + .5f) / height)) * 255));
            texture.SetPixels32(pixels); texture.Apply(false, true); return texture;
        }

        private static void FinishWhite(Mesh mesh)
        {
            var colors = new Color32[mesh.vertexCount]; for (int i = 0; i < colors.Length; i++) colors[i] = new Color32(255, 255, 255, 255); mesh.colors32 = colors;
        }

        private static Mesh FinishMesh(string name, Vector3[] positions, int[] triangles, Vector2[] uv = null)
        {
            for (int i = 0; i < positions.Length; i++) positions[i] = ThreeSpace.ToUnity(positions[i]);
            for (int i = 0; i < triangles.Length; i += 3) { int t = triangles[i + 1]; triangles[i + 1] = triangles[i + 2]; triangles[i + 2] = t; }
            var mesh = new Mesh { name = name, vertices = positions, triangles = triangles }; if (uv != null) mesh.uv = uv;
            mesh.RecalculateNormals(); FinishWhite(mesh); return mesh;
        }

        private static Mesh FinishQuad(float from, float to, float width) => FinishMesh("finish_plane", new[] { new Vector3(from, 0, -width / 2), new Vector3(from, 0, width / 2), new Vector3(to, 0, width / 2), new Vector3(to, 0, -width / 2) }, new[] { 0, 1, 2, 0, 2, 3 }, new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right });

        private static Mesh FinishDome()
        {
            const int width = 48, height = 24; var vertices = new Vector3[(width + 1) * (height + 1)]; var triangles = new List<int>(width * height * 6);
            for (int y = 0; y <= height; y++) for (int x = 0; x <= width; x++)
            {
                float theta = y / (float)height * Mathf.PI / 2, phi = x / (float)width * Mathf.PI * 2;
                vertices[y * (width + 1) + x] = new Vector3(-Mathf.Cos(phi) * Mathf.Sin(theta), Mathf.Cos(theta), Mathf.Sin(phi) * Mathf.Sin(theta));
                if (x == width || y == height) continue;
                int a = y * (width + 1) + x, b = a + width + 1;
                if (y != 0) { triangles.Add(a + 1); triangles.Add(a); triangles.Add(b + 1); }
                triangles.Add(a); triangles.Add(b); triangles.Add(b + 1);
            }
            return FinishMesh("finish_shell", vertices, triangles.ToArray());
        }

        private static Mesh FinishCylinder()
        {
            const int sides = 32; var vertices = new Vector3[(sides + 1) * 2]; var uv = new Vector2[vertices.Length]; var triangles = new int[sides * 6];
            for (int i = 0; i <= sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2;
                vertices[i * 2] = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)); vertices[i * 2 + 1] = new Vector3(Mathf.Sin(a), 1, Mathf.Cos(a));
                uv[i * 2] = new Vector2(i / (float)sides, 0); uv[i * 2 + 1] = new Vector2(i / (float)sides, 1);
                if (i == sides) continue;
                int b = i * 2, t = i * 6; triangles[t] = b; triangles[t + 1] = b + 2; triangles[t + 2] = b + 1; triangles[t + 3] = b + 2; triangles[t + 4] = b + 3; triangles[t + 5] = b + 1;
            }
            return FinishMesh("finish_beam", vertices, triangles, uv);
        }

        private static Mesh FinishIcosahedron()
        {
            float t = (1 + Mathf.Sqrt(5)) / 2;
            var v = new[] { new Vector3(-1,t,0),new Vector3(1,t,0),new Vector3(-1,-t,0),new Vector3(1,-t,0),new Vector3(0,-1,t),new Vector3(0,1,t),new Vector3(0,-1,-t),new Vector3(0,1,-t),new Vector3(t,0,-1),new Vector3(t,0,1),new Vector3(-t,0,-1),new Vector3(-t,0,1) };
            var indices = new[] { 0,11,5,0,5,1,0,1,7,0,7,10,0,10,11,1,5,9,5,11,4,11,10,2,10,7,6,7,1,8,3,9,4,3,4,2,3,2,6,3,6,8,3,8,9,4,9,5,2,4,11,6,2,10,8,6,7,9,8,1 };
            var vertices = new Vector3[indices.Length]; var triangles = new int[indices.Length];
            for (int i = 0; i < indices.Length; i++) { vertices[i] = v[indices[i]].normalized; triangles[i] = i; }
            return FinishMesh("finish_rock", vertices, triangles);
        }

        /// <summary>Billboards share four bounded meshes; normal sprites are depth-sorted before drawing.</summary>
        private sealed class FinishBatch
        {
            private struct Draw { public Vector3 P; public Vector2 Size; public Color Color; public float Depth; }
            private const int Capacity = 4096;
            private readonly List<Draw> draws = new List<Draw>(Capacity);
            private readonly List<Vector3> vertices = new List<Vector3>(Capacity * 4);
            private readonly List<Color> colors = new List<Color>(Capacity * 4);
            private readonly List<Vector2> uv = new List<Vector2>(Capacity * 4);
            private readonly List<int> indices = new List<int>(Capacity * 6);
            private readonly Mesh mesh;
            private readonly MeshRenderer renderer;
            private readonly bool sort;
            private int shown = -1;
            private static readonly Comparison<Draw> BackToFront = (a, b) => b.Depth.CompareTo(a.Depth);

            public FinishBatch(Transform parent, string name, Material material, bool sort)
            {
                this.sort = sort; var go = new GameObject(name); go.transform.SetParent(parent, false);
                mesh = new Mesh { name = name }; mesh.MarkDynamic(); mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10);
                go.AddComponent<MeshFilter>().sharedMesh = mesh; renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                for (int i = 0; i < Capacity; i++)
                {
                    int b = i * 4; uv.Add(Vector2.zero); uv.Add(Vector2.up); uv.Add(Vector2.one); uv.Add(Vector2.right);
                    indices.Add(b); indices.Add(b + 1); indices.Add(b + 2); indices.Add(b); indices.Add(b + 2); indices.Add(b + 3);
                }
            }
            public void Begin() => draws.Clear();
            public void Add(Vector3 p, Vector2 size, Color color) { if (draws.Count < Capacity) draws.Add(new Draw { P = p, Size = size, Color = color }); }
            public void Flush(Camera camera)
            {
                Vector3 right = camera != null ? camera.transform.right : Vector3.right, up = camera != null ? camera.transform.up : Vector3.up;
                if (sort && camera != null)
                {
                    var forward = camera.transform.forward; var origin = camera.transform.position;
                    for (int i = 0; i < draws.Count; i++) { var draw = draws[i]; draw.Depth = Vector3.Dot(forward, draw.P - origin); draws[i] = draw; }
                    draws.Sort(BackToFront);
                }
                vertices.Clear(); colors.Clear();
                foreach (var draw in draws)
                {
                    var x = right * (draw.Size.x / 2); var y = up * (draw.Size.y / 2);
                    vertices.Add(draw.P - x - y); vertices.Add(draw.P - x + y); vertices.Add(draw.P + x + y); vertices.Add(draw.P + x - y);
                    colors.Add(draw.Color); colors.Add(draw.Color); colors.Add(draw.Color); colors.Add(draw.Color);
                }
                renderer.enabled = draws.Count > 0;
                if (shown == draws.Count) { if (shown > 0) { mesh.SetVertices(vertices); mesh.SetColors(colors); } return; }
                shown = draws.Count; mesh.Clear(); if (shown == 0) return;
                mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0, uv, 0, shown * 4); mesh.SetTriangles(indices, 0, shown * 6, 0, false); mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 10);
            }
            public void Release() { if (renderer != null) { Destroy(renderer.sharedMaterial); Destroy(renderer.gameObject); } Destroy(mesh); }
        }
    }
}
