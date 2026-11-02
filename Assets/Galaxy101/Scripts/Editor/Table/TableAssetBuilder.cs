using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace Galaxy101.Table.EditorTools
{
    /// <summary>
    /// Galaxy 101 › Build Table Assets: turns the polished GLBs (Assets/Galaxy101/Table/Models, copied from
    /// Design/.../assets_src/blender_out) into prefabs (spec/16 D2–D3), fills the catalog and creates the scene.
    /// * "X" + "X_LOD1" renderers become a two-level LODGroup (Blender pass keeps LOD0 names untouched).
    /// * Racks get 30 "Slot_{row}_{index}" transforms at slotLocal() (row 0 = top, 1 = bottom), under the GLB root,
    ///   whose rotation already carries TILT.
    /// </summary>
    public static class TableAssetBuilder
    {
        private const string Models = "Assets/Galaxy101/Table/Models", Prefabs = "Assets/Galaxy101/Table/Prefabs";
        private const string CatalogPath = "Assets/Galaxy101/Resources/Galaxy101/Table/TableAssetCatalog.asset";
        public const string ScenePath = "Assets/Galaxy101/Table/Scenes/GalaxyTable.unity";

        private static readonly Dictionary<string, string> TableFiles = new Dictionary<string, string> { ["station"] = "masa_istasyon", ["wood"] = "masa_ceviz", ["holo"] = "masa_hologram", ["andromeda"] = "masa_andromeda" };
        private static readonly Dictionary<string, string> RackFiles = new Dictionary<string, string> { ["walnut"] = "istaka_ceviz", ["sedef"] = "istaka_sedef", ["chrome"] = "istaka_aluminyum", ["carbon"] = "istaka_karbon", ["neon"] = "istaka_neon", ["andromeda"] = "istaka_andromeda" };
        private static readonly Dictionary<string, string> TileFiles = new Dictionary<string, string> { ["classic"] = "tas_seti_klasik", ["galaxy"] = "tas_seti_galaksi", ["gold"] = "tas_seti_altin", ["neon"] = "tas_seti_neon", ["marble"] = "tas_seti_mermer" };

        [MenuItem("Galaxy 101/Build Table Assets")]
        public static void Build()
        {
            Directory.CreateDirectory(Prefabs); Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath)); Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            AssetDatabase.Refresh(); TableMaterialConverter.ResetCache();
            var catalog = AssetDatabase.LoadAssetAtPath<TableAssetCatalog>(CatalogPath);
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<TableAssetCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
            catalog.Tables = TableFiles.Select(p => Entry(p.Key, MakePrefab("galaxy101_" + p.Value + "_saturn", "Table_" + p.Key, false))).ToArray();
            catalog.Racks = RackFiles.Select(p => Entry(p.Key, MakePrefab("galaxy101_" + p.Value + "_saturn", "Rack_" + p.Key, true))).ToArray();
            catalog.TileSets = TileFiles.Select(p => Entry(p.Key, MakePrefab("galaxy101_" + p.Value + "_saturn", "TileSet_" + p.Key, false))).ToArray();
            catalog.GiftModels = TableAssetCatalog.Gifts.Select(g => Entry(g, MakePrefab("galaxy101_" + (Array.IndexOf(TableAssetCatalog.Gifts, g) < 6 ? "ikram_" : "hediye_") + g, "Gift_" + g, false))).ToArray();
            catalog.Chair = MakePrefab("galaxy101_koltuk", "Chair", false);
            catalog.Room = MakePrefab("galaxy101_oda_istasyon", "Room", false);
            catalog.Die = MakePrefab("galaxy101_zar", "Die", false); // tools/blender/make_die.py (spec/24)
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
            // Face atlases are 2048×2520 (8×7 cells of 256×360): keep them at full resolution with clean alpha edges.
            foreach (var path in Directory.GetFiles("Assets/Galaxy101/Resources/Galaxy101/Table/TileFaces", "*.png"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace(Path.DirectorySeparatorChar, '/'));
                if (importer.maxTextureSize == 4096 && importer.alphaIsTransparency) continue;
                importer.maxTextureSize = 4096; importer.alphaIsTransparency = true; importer.wrapMode = TextureWrapMode.Clamp; importer.anisoLevel = 8;
                importer.SaveAndReimport();
            }
            // Effects: a material asset that keeps the URP particle shader in player builds (TableFx).
            const string fxPath = "Assets/Galaxy101/Resources/Galaxy101/Table/Fx/fx_particles.mat";
            Directory.CreateDirectory(Path.GetDirectoryName(fxPath));
            if (AssetDatabase.LoadAssetAtPath<Material>(fxPath) == null)
                AssetDatabase.CreateAsset(new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "fx_particles" }, fxPath);
            BuildScene();
            Debug.Log("GALAXY101_TABLE_ASSETS_READY prefabs=" + Directory.GetFiles(Prefabs, "*.prefab").Length);
        }

        private static TableAssetCatalog.Entry Entry(string key, GameObject prefab) => new TableAssetCatalog.Entry { Key = key, Prefab = prefab };

        /// <summary>Refresh only the six rack assets after exporting a newer racks3d.js; preserve scenes and other models.</summary>
        [MenuItem("Galaxy 101/Rebuild Rack Assets")]
        public static void RebuildRacks()
        {
            AssetDatabase.Refresh(); TableMaterialConverter.ResetCache();
            foreach (var model in RackFiles.Values)
            {
                var importer = AssetImporter.GetAtPath(Models + "/galaxy101_" + model + "_saturn.glb");
                var serialized = new SerializedObject(importer);
                serialized.FindProperty("importSettings.generateMipMaps").boolValue = true;
                serialized.FindProperty("importSettings.defaultMinFilterMode").intValue = 9987; // LINEAR_MIPMAP_LINEAR, racks3d.js tex().
                serialized.FindProperty("importSettings.anisotropicFilterLevel").intValue = 16;
                if (serialized.ApplyModifiedPropertiesWithoutUndo()) importer.SaveAndReimport();
            }
            var catalog = AssetDatabase.LoadAssetAtPath<TableAssetCatalog>(CatalogPath)
                ?? throw new InvalidOperationException("The table asset catalog must exist before refreshing racks.");
            catalog.Racks = RackFiles.Select(p => Entry(p.Key, MakePrefab("galaxy101_" + p.Value + "_saturn", "Rack_" + p.Key, true))).ToArray();
            EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssets();
            Debug.Log("GALAXY101_RACK_ASSETS_READY count=" + catalog.Racks.Length);
        }

        private static GameObject MakePrefab(string model, string prefabName, bool rackSlots)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Models + "/" + model + ".glb") ?? throw new InvalidOperationException("Model not imported: " + model);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            try
            {
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                instance.name = prefabName;
                TableMaterialConverter.ConvertAll(instance, "Assets/Galaxy101/Table/Materials/" + prefabName);
                if (rackSlots) ExtractRackCanvasTextures(instance, Models + "/" + model + ".glb", "Assets/Galaxy101/Table/Materials/" + prefabName);
                AddLodGroup(instance);
                if (rackSlots) AddSlots(instance.transform);
                return PrefabUtility.SaveAsPrefabAsset(instance, Prefabs + "/" + prefabName + ".prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        // Imported embedded glTF images lose their sampler settings in the Editor import path.
        // Keep the exact PNG bytes from the source canvas as ordinary texture assets, with
        // the source's mipmaps/trilinear/16x sampling explicitly serialized for player builds.
        private static void ExtractRackCanvasTextures(GameObject root, string modelPath, string folder)
        {
            var bytes = File.ReadAllBytes(modelPath); int jsonLength = BitConverter.ToInt32(bytes, 12);
            var json = JObject.Parse(System.Text.Encoding.UTF8.GetString(bytes, 20, jsonLength));
            int binaryStart = 20 + jsonLength + 8;
            foreach (var material in root.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Distinct())
            {
                var source = json["materials"]?.FirstOrDefault(m => (string)m["name"] == material.name);
                var slot = source?["pbrMetallicRoughness"]?["baseColorTexture"];
                if (slot == null) continue;
                var texture = json["textures"][(int)slot["index"]];
                var image = json["images"][(int)texture["source"]];
                if ((string)image["mimeType"] != "image/png" || image["bufferView"] == null)
                    throw new InvalidOperationException("Rack canvas must be an embedded PNG: " + modelPath + "/" + material.name);
                var view = json["bufferViews"][(int)image["bufferView"]];
                int offset = checked(binaryStart + ((int?)view["byteOffset"] ?? 0)), length = (int)view["byteLength"];
                if (offset < binaryStart || length < 8 || offset > bytes.Length - length)
                    throw new InvalidDataException("Invalid rack canvas range: " + modelPath);
                var png = new byte[length]; Buffer.BlockCopy(bytes, offset, png, 0, length);
                string path = folder + "/" + material.name + "_BaseColor.png";
                if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(png)) File.WriteAllBytes(path, png);
                AssetDatabase.ImportAsset(path);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                importer.sRGBTexture = true; importer.mipmapEnabled = true; importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Repeat; importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 16; importer.maxTextureSize = 2048;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
                if (source?["emissiveTexture"]?["index"] is JToken emissionIndex &&
                    (int)json["textures"][(int)emissionIndex]["source"] == (int)texture["source"])
                    material.SetTexture("_EmissionMap", material.GetTexture("_BaseMap"));
                EditorUtility.SetDirty(material);
            }
        }

        private static void AddLodGroup(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            var lod1 = renderers.Where(r => r.name.EndsWith("_LOD1", StringComparison.Ordinal)).ToArray();
            if (lod1.Length == 0) return;
            var lod0 = renderers.Except(lod1).ToArray();
            var group = root.AddComponent<LODGroup>();
            // Table-scale assets: full detail until they cover < 18 % of the screen height, LOD1 down to 1 %.
            group.SetLODs(new[] { new LOD(0.18f, lod0), new LOD(0.01f, lod1) });
            group.RecalculateBounds();
        }

        private static void AddSlots(Transform rackRoot)
        {
            var holder = new GameObject("Slots").transform; holder.SetParent(rackRoot, false);
            for (int row = 0; row < TableConstants.Rows; row++)
                for (int i = 0; i < TableConstants.SlotsPerRow; i++)
                {
                    var slot = new GameObject($"Slot_{row}_{i}").transform; slot.SetParent(holder, false);
                    // slotLocal uses r = 1 for the bottom row.
                    slot.localPosition = ThreeSpace.ToUnity(TableConstants.SlotLocal(row == 1 ? 1 : 0, i));
                }
        }

        private static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("GalaxyTable").AddComponent<TableScene>();
            // HUD: UI Toolkit layer stack over the 3D view (TableHud), on the menu's authored PanelSettings.
            var hud = new GameObject("TableHud");
            hud.AddComponent<UnityEngine.UIElements.UIDocument>().panelSettings = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>("Assets/Galaxy101/Resources/Galaxy101/UI/PanelSettings.asset");
            hud.AddComponent<TableHud>();
            // Game flow (engine + bots) and rack input; with a TableGame present the scene skips its static demo layout.
            var game = new GameObject("TableGame");
            game.AddComponent<TableGame>(); game.AddComponent<RackInput>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            var list = EditorBuildSettings.scenes.ToList();
            if (!list.Any(s => s.path == ScenePath)) { list.Add(new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = list.ToArray(); }
        }

        /// <summary>Batch: build the scene in edit mode and render the main camera to PNG (env GALAXY_TABLE_CAPTURE).</summary>
        public static void Capture()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var table = UnityEngine.Object.FindAnyObjectByType<TableScene>();
            table.Build();
            var cam = table.CameraRig.Camera;
            // Optional: GALAXY_TABLE_HIDE=name1,name2 disables renderers (and their _LOD1 twins) for diagnostics.
            var hide = Environment.GetEnvironmentVariable("GALAXY_TABLE_HIDE");
            if (!string.IsNullOrEmpty(hide))
                foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>())
                    if (hide.Split(',').Any(h => r.name == h || r.name == h + "_LOD1")) r.enabled = false;
            string output = Environment.GetEnvironmentVariable("GALAXY_TABLE_CAPTURE") ?? "table.png";
            string view = Environment.GetEnvironmentVariable("GALAXY_TABLE_VIEW");
            var rt = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            // Batch screens are 4:3; render at the 16:9 reference so the rig keeps the 40° vertical FOV.
            cam.targetTexture = rt; cam.aspect = 16f / 9f;
            table.CameraRig.Initialize(cam, view == "top" ? TableCameraRig.View.Top : view == "lobby" ? TableCameraRig.View.Lobby : TableCameraRig.View.Player);
            // Materials created during this same editor tick are only uploaded to the SRP Batcher after a first
            // submit; that first frame draws some objects with a neighbour's material data. Render twice more.
            for (int pass = 0; pass < 3; pass++)
            {
                var request = new RenderPipeline.StandardRequest { destination = rt };
                if (!RenderPipeline.SupportsRenderRequest(cam, request)) throw new InvalidOperationException("Render request unsupported");
                RenderPipeline.SubmitRenderRequest(cam, request);
            }
            var resolved = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            Graphics.Blit(rt, resolved);
            var previous = RenderTexture.active; RenderTexture.active = resolved;
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            RenderTexture.active = previous;
            File.WriteAllBytes(output, image.EncodeToPNG());
            Debug.Log("GALAXY101_TABLE_CAPTURE " + output);
        }
    }
}
