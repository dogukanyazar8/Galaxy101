using System;
using System.Collections.Generic;
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
        private readonly Material face;
        private readonly Dictionary<int, Mesh> faceMeshes = new Dictionary<int, Mesh>();

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
