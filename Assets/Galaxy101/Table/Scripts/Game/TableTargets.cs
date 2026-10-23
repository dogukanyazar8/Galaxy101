using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Galaxy101.Table
{
    /// <summary>
    /// İşleme göstergeleri (spec/23): while a rack tile is selected or dragged, every valid target shows on the felt — at a
    /// meld end a soft gold tile-sized glow with a framed outline and a floating gold "+" button; on an okey that tile could
    /// replace, a green button with two curved arrows turning at 1.4 rad/s. Fade in with smoothstep, breathe at 3.2 rad/s;
    /// the target under a dragged tile grows 1.1× and brightens. Each part (frame, button, arrows) is one combined mesh with a
    /// submesh per colour, shared by every marker: four renderers a marker instead of thirty primitives.
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
        private (Mesh Mesh, Material[] Materials) frame, button, arrows;
        private readonly Dictionary<PrimitiveType, Mesh> primitives = new Dictionary<PrimitiveType, Mesh>();
        private float shownAt;
        private int lastFocus = -1;

        /// <summary>The target the dragged tile hovers (RackInput), or −1.</summary>
        public int Hot { get; set; } = -1;
        public int Visible { get; private set; }

        public void Init(TableGame owner) => game = owner;

        private static Material Unlit(string name, Color color, bool additive)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = name };
            m.SetColor("_BaseColor", new Color(color.r, color.g, color.b, additive ? ThreeBlend.Additive(color.a) : ThreeBlend.Normal(color.a)));
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

        private Mesh Primitive(PrimitiveType type)
        {
            if (primitives.TryGetValue(type, out var mesh)) return mesh;
            var go = GameObject.CreatePrimitive(type); mesh = go.GetComponent<MeshFilter>().sharedMesh; DestroyImmediate(go);
            return primitives[type] = mesh; // Unity's built-in mesh: shared, never destroyed here
        }

        /// <summary>One mesh from primitive pieces: pieces of one material merged, a submesh per material in first-use order.</summary>
        private (Mesh, Material[]) Combine(string name, List<(Material Material, PrimitiveType Type, Matrix4x4 Matrix)> parts)
        {
            var materials = parts.Select(p => p.Material).Distinct().ToArray();
            var merged = materials.Select(material =>
            {
                var mesh = new Mesh();
                mesh.CombineMeshes(parts.Where(p => p.Material == material).Select(p => new CombineInstance { mesh = Primitive(p.Type), transform = p.Matrix }).ToArray(), true, true);
                return mesh;
            }).ToArray();
            var result = new Mesh { name = name };
            result.CombineMeshes(merged.Select(m => new CombineInstance { mesh = m, transform = Matrix4x4.identity }).ToArray(), false, false);
            foreach (var m in merged) Destroy(m);
            return (result, materials);
        }

        private static Matrix4x4 At(Vector3 position, Vector3 scale, Quaternion? rotation = null) => Matrix4x4.TRS(position, rotation ?? Quaternion.identity, scale);

        private void BuildParts()
        {
            glowGold = Unlit("isleme_parilti", new Color(1, .82f, .35f, .28f), true);
            frameGold = Unlit("isleme_cerceve", new Color(1, .84f, .4f, .9f), false);
            buttonGold = Unlit("isleme_dugme", new Color32(0xFF, 0xC9, 0x4A, 255), false);
            plusDark = Unlit("isleme_arti", new Color32(0x3A, 0x26, 0x04, 255), false);
            buttonGreen = Unlit("okey_dugme", new Color32(0x3C, 0xD8, 0x7E, 255), false);
            arrowWhite = Unlit("okey_ok", Color.white, false);
            float w = TableConstants.TW + 0.014f, h = TableConstants.TH + 0.014f;
            // The dashed frame: four thin bars broken into dashes.
            var dashes = new List<(Material, PrimitiveType, Matrix4x4)>();
            for (int side = 0; side < 4; side++)
            {
                bool horizontal = side < 2; float length = horizontal ? w : h; int count = horizontal ? 3 : 4;
                for (int d = 0; d < count; d++)
                {
                    float along = (d + .5f) / count * length - length / 2, dash = length / count * .62f;
                    var pos = horizontal ? new Vector3(along, 0.0004f, side == 0 ? h / 2 : -h / 2) : new Vector3(side == 2 ? w / 2 : -w / 2, 0.0004f, along);
                    dashes.Add((frameGold, PrimitiveType.Cube, At(pos, horizontal ? new Vector3(dash, 0.0006f, 0.0018f) : new Vector3(0.0018f, 0.0006f, dash))));
                }
            }
            frame = Combine("isleme_cerceve", dashes);
            button = Combine("isleme_dugme", new List<(Material, PrimitiveType, Matrix4x4)>
            {
                (arrowWhite, PrimitiveType.Cylinder, At(Vector3.zero, new Vector3(0.033f, 0.0012f, 0.033f))),
                (buttonGold, PrimitiveType.Cylinder, At(new Vector3(0, 0.0008f, 0), new Vector3(0.030f, 0.0016f, 0.030f))),
                (plusDark, PrimitiveType.Cube, At(new Vector3(0, 0.0026f, 0), new Vector3(0.016f, 0.0008f, 0.0036f))),
                (plusDark, PrimitiveType.Cube, At(new Vector3(0, 0.0026f, 0), new Vector3(0.0036f, 0.0008f, 0.016f))),
            });
            var arrowParts = new List<(Material, PrimitiveType, Matrix4x4)> { (buttonGreen, PrimitiveType.Cylinder, At(Vector3.zero, new Vector3(0.034f, 0.0016f, 0.034f))) };
            for (int a = 0; a < 2; a++)
            {
                var arm = Matrix4x4.Rotate(Quaternion.Euler(0, a * 180, 0));
                for (int s = 0; s < 4; s++)
                {
                    float angle = (20 + s * 28) * Mathf.Deg2Rad;
                    arrowParts.Add((arrowWhite, PrimitiveType.Cube, arm * At(new Vector3(Mathf.Cos(angle) * 0.0095f, 0.0022f, Mathf.Sin(angle) * 0.0095f), new Vector3(0.0035f, 0.0008f, 0.0072f), Quaternion.Euler(0, -angle * Mathf.Rad2Deg, 0))));
                }
                arrowParts.Add((arrowWhite, PrimitiveType.Cube, arm * At(new Vector3(Mathf.Cos(2.3f) * 0.0095f, 0.0022f, Mathf.Sin(2.3f) * 0.0095f), new Vector3(0.0075f, 0.0008f, 0.0035f), Quaternion.Euler(0, -95, 0))));
            }
            arrows = Combine("okey_al", arrowParts);
        }

        private static GameObject Piece(string name, Transform parent, (Mesh Mesh, Material[] Materials) part)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = part.Mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterials = part.Materials; r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
            return go;
        }

        private Marker Make()
        {
            if (glowGold == null) BuildParts();
            var m = new Marker { Root = new GameObject("isleme_hedef") }; m.Root.transform.SetParent(transform, false);
            float w = TableConstants.TW + 0.014f, h = TableConstants.TH + 0.014f;
            m.Glow = new GameObject("parilti"); m.Glow.transform.SetParent(m.Root.transform, false);
            Part(PrimitiveType.Quad, m.Glow.transform, glowGold, Vector3.zero, new Vector3(w * 1.25f, h * 1.25f, 1), Quaternion.Euler(90, 0, 0));
            Piece("cerceve", m.Glow.transform, frame);
            m.Button = Piece("dugme", m.Root.transform, button);
            m.Arrows = Piece("okey_al", m.Root.transform, arrows);
            pool.Add(m); return m;
        }

        private void OnDestroy()
        {
            foreach (var mesh in new[] { frame.Mesh, button.Mesh, arrows.Mesh }) if (mesh != null) Destroy(mesh);
            foreach (var material in new[] { glowGold, frameGold, buttonGold, plusDark, buttonGreen, arrowWhite }) if (material != null) Destroy(material);
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
