using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static Galaxy101.Table.TableConstants;

namespace Galaxy101.Table
{
    /// <summary>
    /// İşleme göstergeleri (spec/23; table3d.js markers / tickMarkers): while a rack tile is selected (and I have opened) or
    /// dragged, each valid target shows three flat planes — an additive halo (glowTex, 0.15 × 0.17 m, gold or green), a
    /// dashed frame (markerTex, tile-sized + 1.4 cm, ends only) and a 3 cm button (badgeTex: gold "+", or green arrows
    /// turning at 1.4 rad/s over an okey). Each marker fades in (dt·10, smoothstep), breathes at 3.2 rad/s with its own
    /// phase, and the one under the dragged tile grows and brightens (dt·14). The textures are the design's canvases
    /// (tools/marker_textures.py); frame and button are not tone mapped (Galaxy101/FlatFace), as MeshBasicMaterial
    /// toneMapped:false.
    /// </summary>
    public sealed class TableTargets : MonoBehaviour
    {
        private const int Capacity = 48;

        private sealed class Marker
        {
            public Transform Root, Halo, Frame, Badge;
            public MeshRenderer HaloR, FrameR, BadgeR;
            public float A, H, Phase;
            public string Key;
            public bool Okey;
        }

        private TableGame game;
        private readonly List<Marker> pool = new List<Marker>();
        private readonly List<Object> owned = new List<Object>();
        private Material haloMaterial, frameGold, frameOkey, badgeGold, badgeOkey;
        private Mesh haloMesh, frameMesh, badgeMesh;
        private MaterialPropertyBlock block;
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly Color Gold = new Color32(0xFF, 0xC9, 0x4A, 255), Green = new Color32(0x5C, 0xF2, 0x9A, 255);

        /// <summary>The target the dragged tile hovers (RackInput nearTarget), or −1.</summary>
        public int Hot { get; set; } = -1;
        public int Visible { get; private set; }

        public void Init(TableGame owner) => game = owner;

        /// <summary>
        /// A flat plane of the design's size, readable from above (three PlaneGeometry.rotateX(−π/2): +v away from me),
        /// in Unity space.
        /// </summary>
        private Mesh Plane(string name, float w, float h)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = new[] { ThreeSpace.ToUnity(-w / 2, 0, h / 2), ThreeSpace.ToUnity(w / 2, 0, h / 2), ThreeSpace.ToUnity(w / 2, 0, -h / 2), ThreeSpace.ToUnity(-w / 2, 0, -h / 2) };
            mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals(); if (mesh.normals[0].y < 0) mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); owned.Add(mesh); return mesh;
        }

        private Texture2D Texture(string name)
        {
            var t = Resources.Load<Texture2D>("Galaxy101/Table/Markers/" + name);
            if (t == null) throw new System.InvalidOperationException("Missing marker texture " + name);
            t.wrapMode = TextureWrapMode.Clamp; t.anisoLevel = 8; return t;
        }

        private Material Face(string name, Texture2D map)
        {
            var m = new Material(Shader.Find("Galaxy101/FlatFace")) { name = name };
            m.SetTexture("_BaseMap", map); m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Galaxy101Exposure", ThreeToneMapping.DesignExposure); m.SetFloat("_Galaxy101ToneMapped", 1);
            m.renderQueue = (int)RenderQueue.Transparent + 5; owned.Add(m); return m;
        }

        private void Build()
        {
            block = new MaterialPropertyBlock();
            haloMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "isleme_hale" };
            haloMaterial.SetTexture("_BaseMap", Texture("glow")); haloMaterial.SetColor("_BaseColor", Color.white);
            haloMaterial.SetFloat("_Surface", 1); haloMaterial.SetFloat("_Blend", 2); haloMaterial.SetFloat("_ZWrite", 0); haloMaterial.SetFloat("_Cull", 0);
            haloMaterial.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); haloMaterial.SetInt("_DstBlend", (int)BlendMode.One);
            haloMaterial.SetInt("_SrcBlendAlpha", (int)BlendMode.Zero); haloMaterial.SetInt("_DstBlendAlpha", (int)BlendMode.One);
            haloMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); haloMaterial.renderQueue = (int)RenderQueue.Transparent + 4; owned.Add(haloMaterial);
            frameGold = Face("isleme_cerceve", Texture("frame_gold")); frameOkey = Face("okey_cerceve", Texture("frame_okey"));
            badgeGold = Face("isleme_dugme", Texture("badge_gold")); badgeOkey = Face("okey_dugme", Texture("badge_okey"));
            frameGold.renderQueue = frameOkey.renderQueue = (int)RenderQueue.Transparent + 5;
            badgeGold.renderQueue = badgeOkey.renderQueue = (int)RenderQueue.Transparent + 6;   // renderOrder 2 / 3
            float fw = TW + 0.014f;
            haloMesh = Plane("isleme_hale", 0.15f, 0.17f); frameMesh = Plane("isleme_cerceve", fw, fw * 264 / 200); badgeMesh = Plane("isleme_dugme", 0.03f, 0.03f);
        }

        private (Transform, MeshRenderer) Piece(string name, Transform parent, Mesh mesh, Material material, float y)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = new Vector3(0, y, 0);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = material; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return (go.transform, r);
        }

        private Marker Make()
        {
            if (block == null) Build();
            var m = new Marker { Root = new GameObject("isleme_hedef").transform, Phase = Random.value * 6 };
            m.Root.SetParent(transform, false);
            (m.Halo, m.HaloR) = Piece("hale", m.Root, haloMesh, haloMaterial, -0.0004f);
            (m.Frame, m.FrameR) = Piece("cerceve", m.Root, frameMesh, frameGold, 0);
            (m.Badge, m.BadgeR) = Piece("dugme", m.Root, badgeMesh, badgeGold, 0.004f);
            m.Root.gameObject.SetActive(false);
            pool.Add(m); return m;
        }

        private void Tint(MeshRenderer r, Color c, float opacity)
        {
            r.GetPropertyBlock(block); c.a = opacity; block.SetColor(BaseColor, c); r.SetPropertyBlock(block);
        }

        private void LateUpdate()
        {
            if (game == null) return;
            var targets = game.Targets;
            while (pool.Count < Mathf.Min(Capacity, targets.Count)) Make();
            float now = Time.time, dt = Time.deltaTime; int shown = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                var m = pool[i]; bool on = i < targets.Count; var target = on ? targets[i] : null;
                if (on)
                {
                    string key = (target.Okey >= 0 ? "okey" : "end") + target.Okey + ":" + target.Meld + ":" + target.Prepend + ":" + target.Pose.position;
                    if (m.Key != key)
                    {
                        m.Key = key; m.Okey = target.Okey >= 0;
                        m.FrameR.sharedMaterial = m.Okey ? frameOkey : frameGold; m.BadgeR.sharedMaterial = m.Okey ? badgeOkey : badgeGold;
                        m.Frame.gameObject.SetActive(!m.Okey);
                        var p = target.Pose.position; p.y = m.Okey ? TY + TD + 0.0012f : TY + 0.0014f;
                        m.Root.position = p; m.A = Mathf.Min(m.A, 0.01f);
                    }
                }
                bool isHot = on && i == Hot;
                m.A += ((on ? 1 : 0) - m.A) * Mathf.Min(1, dt * 10); m.H += ((isHot ? 1 : 0) - m.H) * Mathf.Min(1, dt * 14);
                bool visible = m.A > 0.01f; if (m.Root.gameObject.activeSelf != visible) m.Root.gameObject.SetActive(visible);
                if (!visible) { if (!on) m.Key = null; continue; }
                shown++;
                float e = m.A * m.A * (3 - 2 * m.A), br = 0.5f + 0.5f * Mathf.Sin(now * 3.2f + m.Phase);
                Tint(m.FrameR, Color.white, ThreeBlend.Normal(Mathf.Clamp01(e * (0.78f + 0.22f * br + m.H * 0.2f))));
                Tint(m.HaloR, m.Okey ? Green : Gold, ThreeBlend.Additive(Mathf.Clamp01(e * (0.16f + 0.12f * br + m.H * 0.45f))));
                Tint(m.BadgeR, Color.white, ThreeBlend.Normal(Mathf.Clamp01(e)));
                m.Badge.localPosition = new Vector3(0, 0.004f + (m.Okey ? 0.006f : 0) + 0.0025f * br + m.H * 0.006f, 0);
                m.Badge.localScale = Vector3.one * ((0.6f + 0.4f * e) * (1 + m.H * 0.25f));
                m.Badge.localRotation = m.Okey ? Quaternion.AngleAxis(-now * 1.4f * Mathf.Rad2Deg, Vector3.up) : Quaternion.identity;
                m.Root.localScale = Vector3.one * (0.92f + 0.08f * e + m.H * 0.1f);
            }
            Visible = shown;
        }

        /// <summary>The target nearest a felt point (within <paramref name="radius"/> m), or −1.</summary>
        public int Nearest(Vector3 point, float radius)
        {
            var targets = game.Targets; int best = -1; float distance = radius;
            for (int i = 0; i < targets.Count; i++)
            {
                var p = targets[i].Pose.position; float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(point.x, point.z));
                if (d < distance) { distance = d; best = i; }
            }
            return best;
        }

        private void OnDestroy()
        {
            foreach (var o in owned) if (o != null) Destroy(o);
            owned.Clear();
        }
    }
}
