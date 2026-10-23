using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace Galaxy101.Table
{
    /// <summary>
    /// Tile instances: body from the polished tile-set GLB (two materials: tas_&lt;skin&gt; + tas_kenar_&lt;skin&gt;) and a
    /// face quad sampling the baked face atlas (spec/16 D4; tools/blender/bake_tile_faces.py). The per-tile face
    /// textures inside the GLBs are reference only.
    /// </summary>
    public sealed class TileFactory
    {
        public const int AtlasColumns = 8, AtlasRows = 7, CellWidth = 256, CellHeight = 360;
        private static readonly string[] Suits = { "R", "B", "Y", "K" };
        private readonly GameObject body;
        private readonly Material face, flatFace;
        private readonly Dictionary<int, Mesh> faceMeshes = new Dictionary<int, Mesh>();
        private Material okeyGold;
        private Mesh barMesh;
        /// <summary>Under-tile bars (table3d.js barGeo): green = valid set, yellow = new tile (its alpha blinks, set by TableFx).</summary>
        public static Material ValidBar, NewBar;
        /// <summary>The v2 gold okey frame instead of the blank v3 face (kept for comparison captures).</summary>
        public static bool LegacyOkeyMark;
        private Mesh okeyFrame, okeyDot;

        public TileFactory(GameObject tileSetInstance, string skin, float roughness, float emission)
        {
            var source = tileSetInstance.transform.Find("tas_R1") ?? throw new ArgumentException("Tile set lacks tas_R1");
            body = source.Find("tas_govde").gameObject;
            tileSetInstance.SetActive(false);
            var atlas = Resources.Load<Texture2D>("Galaxy101/Table/TileFaces/TileFaces_" + skin) ?? throw new InvalidOperationException("Missing tile face atlas: " + skin);
            face = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "tas_yuz_" + skin };
            face.SetTexture("_BaseMap", atlas); face.SetColor("_BaseColor", Color.white); face.SetFloat("_Smoothness", 1 - roughness);
            face.SetFloat("_Surface", 1); face.SetFloat("_Blend", 0); face.SetFloat("_ZWrite", 0);
            face.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha); face.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            // Alpha accumulates "over" (One, OneMinusSrcAlpha): the HUD composites this camera's alpha, and URP's
            // default alpha factors would copy the face's transparent margins into the target as holes.
            face.SetInt("_SrcBlendAlpha", (int)BlendMode.One); face.SetInt("_DstBlendAlpha", (int)BlendMode.OneMinusSrcAlpha);
            face.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); face.renderQueue = (int)RenderQueue.Transparent;
            if (emission > 0) { face.EnableKeyword("_EMISSION"); face.SetTexture("_EmissionMap", atlas); face.SetColor("_EmissionColor", Color.white * emission); }
            // spec/23: tiles lying on the felt show their faces unlit — full, crisp colours whatever the lights do.
            // table3d.js faceMatFlat is also toneMapped:false: Galaxy101/FlatFace undoes the table camera's tone mapping.
            flatFace = new Material(Shader.Find("Galaxy101/FlatFace")) { name = "tas_yuz_isiksiz_" + skin };
            flatFace.SetTexture("_BaseMap", atlas); flatFace.SetColor("_BaseColor", Color.white);
            flatFace.SetFloat("_Galaxy101Exposure", ThreeToneMapping.DesignExposure); flatFace.SetFloat("_Galaxy101ToneMapped", 1);
            flatFace.renderQueue = (int)RenderQueue.Transparent;
        }

        /// <summary>Lying on the table (melds, piles, indicator, stacks) → unlit face; standing on a rack → lit PBR face.</summary>
        public void SetLying(GameObject tile, bool lying)
        {
            var f = tile.transform.Find("tas_yuz"); if (f == null) return;
            var r = f.GetComponent<MeshRenderer>(); var m = lying ? flatFace : face; if (r.sharedMaterial != m) r.sharedMaterial = m;
        }

        /// <summary>Atlas cell: R1…R13 = 0…12, B = 13…25, Y = 26…38, K = 39…51, fake okey = 52.</summary>
        public static int CellOf(string code)
        {
            if (code == "joker") return 52;
            int suit = Array.IndexOf(Suits, code.Substring(0, 1));
            if (suit < 0 || !int.TryParse(code.Substring(1), out int n) || n < 1 || n > 13) throw new ArgumentException("Bad tile code: " + code, nameof(code));
            return suit * 13 + n - 1;
        }

        /// <summary>code: "R7", "K13", "joker" or null for a face-less tile (deck, opponents).</summary>
        public GameObject Create(string code, Transform parent)
        {
            var tile = new GameObject(code == null ? "tas" : "tas_" + code);
            tile.transform.SetParent(parent, false);
            var b = UnityEngine.Object.Instantiate(body, tile.transform, false); b.name = "tas_govde";
            b.transform.localPosition = Vector3.zero; b.transform.localRotation = Quaternion.identity; b.SetActive(true);
            // Pick volume for the rack input (table3d.js raycasts the tile body).
            tile.AddComponent<BoxCollider>().size = new Vector3(TableConstants.TW, TableConstants.TH, TableConstants.TD);
            SetFace(tile, code);
            return tile;
        }

        /// <summary>Give a tile made by another factory this factory's body and face material (tile style change).</summary>
        public void Restyle(GameObject tile)
        {
            var old = tile.transform.Find("tas_govde");
            var b = UnityEngine.Object.Instantiate(body, tile.transform, false); b.name = "tas_govde";
            b.transform.localPosition = Vector3.zero; b.transform.localRotation = Quaternion.identity; b.SetActive(true);
            if (old != null) { b.transform.SetSiblingIndex(old.GetSiblingIndex()); old.gameObject.SetActive(false); UnityEngine.Object.Destroy(old.gameObject); }
            var f = tile.transform.Find("tas_yuz"); if (f != null) { var r = f.GetComponent<MeshRenderer>(); r.sharedMaterial = r.sharedMaterial != null && r.sharedMaterial.name.StartsWith("tas_yuz_isiksiz") ? flatFace : face; }
        }

        /// <summary>Show <paramref name="code"/> on the tile's face plane, or hide it (null) for a tile seen from behind.</summary>
        public void SetFace(GameObject tile, string code)
        {
            var f = tile.transform.Find("tas_yuz");
            if (code == null) { if (f != null) f.gameObject.SetActive(false); tile.name = "tas"; return; }
            if (f == null)
            {
                f = new GameObject("tas_yuz").transform; f.SetParent(tile.transform, false);
                // Face plane: 92 % of the tile, 0.6 mm in front of the body (table3d.js makeTile).
                f.localPosition = new Vector3(0, 0, TableConstants.TD / 2 + 0.0006f);
                f.gameObject.AddComponent<MeshFilter>();
                var r = f.gameObject.AddComponent<MeshRenderer>(); r.sharedMaterial = face; r.shadowCastingMode = ShadowCastingMode.Off;
            }
            f.GetComponent<MeshFilter>().sharedMesh = FaceMesh(CellOf(code)); f.gameObject.SetActive(true); tile.name = "tas_" + code;
        }

        /// <summary>
        /// Okey mark (spec/17 common rules): the tile keeps its own face; a gold dot (#E8B730) covers the face's colour dot
        /// and a 2 px gold inner frame runs inside the tile edge. Shown on every face-up okey (rack, melds, piles).
        /// </summary>
        public void SetOkeyMark(GameObject tile, bool on)
        {
            // spec/23 (v3): the real okey's face is completely blank — no number, colour or mark.
            var face = tile.transform.Find("tas_yuz");
            if (face != null) face.gameObject.SetActive(!on && tile.name != "tas");
            var old = tile.transform.Find("tas_okey"); if (old != null) old.gameObject.SetActive(false);
            if (!on || !LegacyOkeyMark) return;
            var mark = old;
            if (mark == null)
            {
                if (okeyGold == null)
                {
                    okeyGold = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "tas_okey_altin" };
                    ColorUtility.TryParseHtmlString("#E8B730", out var gold); okeyGold.SetColor("_BaseColor", gold);
                    okeyFrame = FrameMesh(); okeyDot = DotMesh();
                }
                mark = new GameObject("tas_okey").transform; mark.SetParent(tile.transform, false);
                // In front of the face plane (TD/2 + 0.6 mm) so the face's own dot is covered.
                mark.localPosition = new Vector3(0, 0, TableConstants.TD / 2 + 0.0009f);
                foreach (var (name, mesh) in new[] { ("tas_okey_cerceve", okeyFrame), ("tas_okey_nokta", okeyDot) })
                {
                    var part = new GameObject(name); part.transform.SetParent(mark, false);
                    part.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var r = part.AddComponent<MeshRenderer>(); r.sharedMaterial = okeyGold; r.shadowCastingMode = ShadowCastingMode.Off;
                }
            }
            mark.gameObject.SetActive(true);
        }

        /// <summary>A 0.78 TW × 3.5 mm × 1 mm bar along the tile face's bottom edge; hidden until shown.</summary>
        public GameObject Bar(GameObject tile)
        {
            var bar = tile.transform.Find("tas_cubuk");
            if (bar != null) return bar.gameObject;
            if (ValidBar == null)
            {
                ValidBar = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "gecerli" }; ValidBar.SetColor("_BaseColor", new Color32(0x3C, 0xCB, 0x6E, 255));
                NewBar = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { name = "yeni_tas" };
                NewBar.SetFloat("_Surface", 1); NewBar.SetFloat("_ZWrite", 0); NewBar.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); NewBar.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                NewBar.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); NewBar.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                NewBar.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); NewBar.renderQueue = (int)RenderQueue.Transparent; NewBar.SetColor("_BaseColor", new Color32(0xFF, 0xD9, 0x64, 255));
            }
            if (barMesh == null)
            {
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube); barMesh = cube.GetComponent<MeshFilter>().sharedMesh; UnityEngine.Object.Destroy(cube);
            }
            bar = new GameObject("tas_cubuk").transform; bar.SetParent(tile.transform, false);
            // table3d.js hangs the bar just under the tile; this rack's front lip would hide it there, so it underlines
            // the face's bottom edge instead (in front of the face plane).
            // 9 mm above the bottom edge: between the face's colour dot and the part the rack's lip covers.
            bar.localPosition = new Vector3(0, -TableConstants.TH / 2 + 0.009f, TableConstants.TD / 2 + 0.0012f);
            bar.localScale = new Vector3(TableConstants.TW * 0.78f, 0.0035f, 0.001f);
            bar.gameObject.AddComponent<MeshFilter>().sharedMesh = barMesh;
            var r = bar.gameObject.AddComponent<MeshRenderer>(); r.sharedMaterial = ValidBar; r.shadowCastingMode = ShadowCastingMode.Off;
            bar.gameObject.SetActive(false);
            return bar.gameObject;
        }

        /// <summary>Rectangular ring just inside the tile edge, ~2 px at rack size (46 px wide tile).</summary>
        private static Mesh FrameMesh()
        {
            float w = TableConstants.TW / 2 * 0.95f, h = TableConstants.TH / 2 * 0.96f, t = TableConstants.TW * 2 / 46f;
            var v = new[] { new Vector3(-w, -h), new Vector3(w, -h), new Vector3(w, h), new Vector3(-w, h), new Vector3(-w + t, -h + t), new Vector3(w - t, -h + t), new Vector3(w - t, h - t), new Vector3(-w + t, h - t) };
            var tri = new List<int>();
            for (int i = 0; i < 4; i++) { int n = (i + 1) % 4; tri.AddRange(new[] { i, i + 4, n, n, i + 4, n + 4 }); }
            var mesh = new Mesh { name = "tas_okey_cerceve", vertices = v, triangles = tri.ToArray(), normals = v.Select(_ => Vector3.forward).ToArray() };
            FaceForward(mesh); mesh.RecalculateBounds(); return mesh;
        }

        /// <summary>Disc over the face dot: canvas (64,145) r 11 of 128×180 on the 92 % face plane.</summary>
        private static Mesh DotMesh()
        {
            float faceH = TableConstants.TH * 0.92f, y = faceH * (0.5f - 145f / 180f), r = 12f / 180f * faceH;
            const int segments = 24;
            var v = new Vector3[segments + 1]; v[0] = new Vector3(0, y);
            for (int i = 0; i < segments; i++) { float a = i * Mathf.PI * 2 / segments; v[i + 1] = new Vector3(Mathf.Cos(a) * r, y + Mathf.Sin(a) * r); }
            var tri = new List<int>(); for (int i = 0; i < segments; i++) tri.AddRange(new[] { 0, 1 + (i + 1) % segments, 1 + i });
            var mesh = new Mesh { name = "tas_okey_nokta", vertices = v, triangles = tri.ToArray(), normals = v.Select(_ => Vector3.forward).ToArray() };
            FaceForward(mesh); mesh.RecalculateBounds(); return mesh;
        }

        /// <summary>Wind every triangle like the face quad (FaceMesh: cross(v1 − v0, v2 − v0).z &gt; 0), which renders from the player side.</summary>
        private static void FaceForward(Mesh mesh)
        {
            var v = mesh.vertices; var t = mesh.triangles;
            for (int i = 0; i < t.Length; i += 3)
                if (Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]).z < 0) { int s = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = s; }
            mesh.triangles = t;
        }

        private Mesh FaceMesh(int cell)
        {
            if (faceMeshes.TryGetValue(cell, out var mesh)) return mesh;
            float w = TableConstants.TW * 0.92f / 2, h = TableConstants.TH * 0.92f / 2;
            int col = cell % AtlasColumns, row = cell / AtlasColumns;
            float u0 = col / (float)AtlasColumns, u1 = (col + 1) / (float)AtlasColumns;
            float v1 = 1 - row / (float)AtlasRows, v0 = 1 - (row + 1) / (float)AtlasRows;
            // Seen from +Z (the player side) the viewer's right is −X, so u grows towards −X.
            mesh = new Mesh
            {
                name = "tas_yuz_" + cell,
                vertices = new[] { new Vector3(w, -h), new Vector3(w, h), new Vector3(-w, h), new Vector3(-w, -h) },
                uv = new[] { new Vector2(u0, v0), new Vector2(u0, v1), new Vector2(u1, v1), new Vector2(u1, v0) },
                normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward },
                triangles = new[] { 0, 1, 2, 0, 2, 3 },
            };
            mesh.RecalculateBounds(); faceMeshes.Add(cell, mesh); return mesh;
        }
    }
}
