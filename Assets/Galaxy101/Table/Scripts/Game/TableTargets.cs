using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Galaxy101.Table
{
    /// <summary>
    /// İşleme göstergeleri (spec/23): while a rack tile is selected or dragged, every valid target shows on the felt — at a
    /// meld end a soft gold tile-sized glow with a framed outline and a floating gold "+" button; on an okey that tile could
    /// replace, a green button with two curved arrows turning at 1.4 rad/s. Fade in with smoothstep, breathe at 3.2 rad/s;
    /// the target under a dragged tile grows 1.1× and brightens.
    /// </summary>
    public sealed class TableTargets : MonoBehaviour
    {
        private sealed class Marker
        {
            public GameObject Root, Glow, Button, Arrows;
            public float Shown;
            public bool Okey;
        }

        private TableGame game;
        private readonly List<Marker> pool = new List<Marker>();
        private Material glowGold, frameGold, buttonGold, plusDark, buttonGreen, arrowWhite;
        private float shownAt;
        private int lastFocus = -1;

        /// <summary>The target the dragged tile hovers (RackInput), or −1.</summary>
        public int Hot { get; set; } = -1;
        public int Visible { get; private set; }

        public void Init(TableGame owner) => game = owner;

        private static Material Unlit(string name, Color color, bool additive)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
            m.SetColor("_BaseColor", color);
            if (color.a < 1 || additive)
            {
                m.SetFloat("_Surface", 1); m.SetFloat("_ZWrite", 0); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = (int)RenderQueue.Transparent + 5;
                m.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); m.SetInt("_DstBlend", additive ? (int)BlendMode.One : (int)BlendMode.OneMinusSrcAlpha);
                m.SetInt("_SrcBlendAlpha", (int)BlendMode.One); m.SetInt("_DstBlendAlpha", (int)BlendMode.OneMinusSrcAlpha);
            }
            return m;
        }

        private GameObject Part(PrimitiveType type, Transform parent, Material material, Vector3 position, Vector3 scale, Quaternion? rotation = null)
        {
            var go = GameObject.CreatePrimitive(type); Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = scale; go.transform.localRotation = rotation ?? Quaternion.identity;
            var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = material; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return go;
        }

        private Marker Make()
        {
            if (glowGold == null)
            {
                glowGold = Unlit("isleme_parilti", new Color(1, .82f, .35f, .28f), true);
                frameGold = Unlit("isleme_cerceve", new Color(1, .84f, .4f, .9f), false);
                buttonGold = Unlit("isleme_dugme", new Color32(0xFF, 0xC9, 0x4A, 255), false);
                plusDark = Unlit("isleme_arti", new Color32(0x3A, 0x26, 0x04, 255), false);
                buttonGreen = Unlit("okey_dugme", new Color32(0x3C, 0xD8, 0x7E, 255), false);
                arrowWhite = Unlit("okey_ok", Color.white, false);
            }
            var m = new Marker { Root = new GameObject("isleme_hedef") }; m.Root.transform.SetParent(transform, false);
            float w = TableConstants.TW + 0.014f, h = TableConstants.TH + 0.014f;
            m.Glow = new GameObject("parilti"); m.Glow.transform.SetParent(m.Root.transform, false);
            Part(PrimitiveType.Quad, m.Glow.transform, glowGold, Vector3.zero, new Vector3(w * 1.25f, h * 1.25f, 1), Quaternion.Euler(90, 0, 0));
            // The dashed frame: four thin bars broken into dashes.
            for (int side = 0; side < 4; side++)
            {
                bool horizontal = side < 2; float length = horizontal ? w : h; int dashes = horizontal ? 3 : 4;
                for (int d = 0; d < dashes; d++)
                {
                    float along = (d + .5f) / dashes * length - length / 2, dash = length / dashes * .62f;
                    var pos = horizontal ? new Vector3(along, 0.0004f, side == 0 ? h / 2 : -h / 2) : new Vector3(side == 2 ? w / 2 : -w / 2, 0.0004f, along);
                    Part(PrimitiveType.Cube, m.Glow.transform, frameGold, pos, horizontal ? new Vector3(dash, 0.0006f, 0.0018f) : new Vector3(0.0018f, 0.0006f, dash));
                }
            }
            m.Button = new GameObject("dugme"); m.Button.transform.SetParent(m.Root.transform, false);
            Part(PrimitiveType.Cylinder, m.Button.transform, arrowWhite, Vector3.zero, new Vector3(0.033f, 0.0012f, 0.033f));
            Part(PrimitiveType.Cylinder, m.Button.transform, buttonGold, new Vector3(0, 0.0008f, 0), new Vector3(0.030f, 0.0016f, 0.030f));
            Part(PrimitiveType.Cube, m.Button.transform, plusDark, new Vector3(0, 0.0026f, 0), new Vector3(0.016f, 0.0008f, 0.0036f));
            Part(PrimitiveType.Cube, m.Button.transform, plusDark, new Vector3(0, 0.0026f, 0), new Vector3(0.0036f, 0.0008f, 0.016f));
            m.Arrows = new GameObject("okey_al"); m.Arrows.transform.SetParent(m.Root.transform, false);
            Part(PrimitiveType.Cylinder, m.Arrows.transform, buttonGreen, Vector3.zero, new Vector3(0.034f, 0.0016f, 0.034f));
            for (int a = 0; a < 2; a++)
            {
                var arm = new GameObject("ok_" + a); arm.transform.SetParent(m.Arrows.transform, false); arm.transform.localRotation = Quaternion.Euler(0, a * 180, 0);
                for (int s = 0; s < 4; s++)
                {
                    float angle = (20 + s * 28) * Mathf.Deg2Rad;
                    Part(PrimitiveType.Cube, arm.transform, arrowWhite, new Vector3(Mathf.Cos(angle) * 0.0095f, 0.0022f, Mathf.Sin(angle) * 0.0095f), new Vector3(0.0035f, 0.0008f, 0.0072f), Quaternion.Euler(0, -angle * Mathf.Rad2Deg, 0));
                }
                Part(PrimitiveType.Cube, arm.transform, arrowWhite, new Vector3(Mathf.Cos(2.3f) * 0.0095f, 0.0022f, Mathf.Sin(2.3f) * 0.0095f), new Vector3(0.0075f, 0.0008f, 0.0035f), Quaternion.Euler(0, -95, 0));
            }
            pool.Add(m); return m;
        }

        private void LateUpdate()
        {
            if (game == null) return;
            var targets = game.Targets; int focus = game.FocusTile;
            if (focus != lastFocus) { lastFocus = focus; shownAt = Time.unscaledTime; }
            while (pool.Count < targets.Count) Make();
            float t = Time.unscaledTime, u = Mathf.Clamp01((t - shownAt) / .25f), fade = u * u * (3 - 2 * u);
            Visible = targets.Count;
            for (int i = 0; i < pool.Count; i++)
            {
                var m = pool[i]; bool on = i < targets.Count; m.Root.SetActive(on); if (!on) continue;
                var target = targets[i]; bool okey = target.Okey >= 0, hot = i == Hot;
                var pose = target.Pose;
                // Flat on the felt for ends; above the okey tile for swaps.
                m.Root.transform.SetPositionAndRotation(pose.position + Vector3.up * (okey ? 0.012f : 0.0015f), Quaternion.Euler(0, pose.rotation.eulerAngles.y, 0));
                m.Glow.SetActive(!okey); m.Button.SetActive(!okey); m.Arrows.SetActive(okey);
                float breathe = 1 + .05f * Mathf.Sin(t * 3.2f + i), scale = fade * breathe * (hot ? 1.1f : 1);
                m.Root.transform.localScale = Vector3.one * Mathf.Max(0.001f, scale);
                if (!okey) m.Button.transform.localPosition = new Vector3(0, 0.012f + .003f * Mathf.Sin(t * 3.2f), 0);
                else m.Arrows.transform.localRotation = Quaternion.Euler(0, t * 1.4f * Mathf.Rad2Deg, 0);
            }
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
    }
}
