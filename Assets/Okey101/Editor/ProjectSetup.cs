using System;
using System.IO;
using Okey101.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Okey101.Editor
{
    public static class ProjectSetup
    {
        public const string Root = "Assets/Okey101";
        public const string BootstrapScene = Root + "/Scenes/Bootstrap.unity";
        public const string TableScene = Root + "/Scenes/Table.unity";

        [MenuItem("Okey 101/Create or update local game")]
        public static void CreateOrUpdate()
        {
            for (var i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save open scenes before generating the A0 table. No scene was discarded.");
            ScenarioExporter.Export();
            Directory.CreateDirectory(Root + "/Scenes");
            Directory.CreateDirectory(Root + "/Art/Materials");
            Directory.CreateDirectory(Root + "/Prefabs/Tiles");
            Directory.CreateDirectory(Root + "/Prefabs/Table");
            Directory.CreateDirectory(Root + "/Prefabs/UI");
            AssetDatabase.Refresh();
            var wood = Material("Wood", Color.white, "WoodGrain");
            var felt = Material("Felt", Color.white, "FeltWeave");
            var brass = Material("Brass", new Color(.57f, .39f, .15f));
            var ivory = Material("Tile", new Color(.96f, .94f, .85f));
            var atlas = FaceMaterial();
            var tile = SavePrefab(TileView.CreateArt(Model("Tile"), ivory, atlas, Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")).gameObject,
                Root + "/Prefabs/Tiles/Tile.prefab").GetComponent<TileView>();
            var table = SavePrefab(ModelInstance("Table", wood, felt, brass, ivory), Root + "/Prefabs/Table/Table.prefab");
            var rackSource = ModelInstance("Rack", wood, felt, brass, ivory); rackSource.AddComponent<RackLayout>();
            var rack = SavePrefab(rackSource, Root + "/Prefabs/Table/Rack.prefab");
            var menuSource=new GameObject("Main menu",typeof(MainMenu));menuSource.GetComponent<MainMenu>().BuildView();
            var menu=SavePrefab(menuSource,Root+"/Prefabs/UI/MainMenu.prefab");
            var pauseSource=new GameObject("Pause menu",typeof(RectTransform),typeof(PauseMenu));pauseSource.GetComponent<PauseMenu>().BuildView();
            var pause=SavePrefab(pauseSource,Root+"/Prefabs/UI/PauseMenu.prefab").GetComponent<PauseMenu>();
            var resultSource=new GameObject("Result panel",typeof(RectTransform),typeof(ResultPanel));resultSource.GetComponent<ResultPanel>().BuildView();
            var result=SavePrefab(resultSource,Root+"/Prefabs/UI/ResultPanel.prefab").GetComponent<ResultPanel>();
            var bootstrap = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PrefabUtility.InstantiatePrefab(menu);
            if (!EditorSceneManager.SaveScene(bootstrap, BootstrapScene)) throw new IOException("Could not save Bootstrap scene.");
            var tableScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Local table");
            var cameraObject = new GameObject("Table Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(root.transform, false);
            root.AddComponent<TablePresenter>().Configure(cameraObject.GetComponent<Camera>(), tile, table, rack);
            root.AddComponent<GameBootstrap>().ConfigureUI(pause,result);
            root.AddComponent<TableAudio>().Configure(Clip("draw"), Clip("place"), Clip("discard"));
            root.AddComponent<DebugPanel>().Configure(System.Array.ConvertAll(ScenarioExporter.Names,
                name => AssetDatabase.LoadAssetAtPath<TextAsset>(ScenarioExporter.Root + "/" + name + ".json")));
            if (!EditorSceneManager.SaveScene(tableScene, TableScene)) throw new IOException("Could not save Table scene.");
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BootstrapScene, true), new EditorBuildSettingsScene(TableScene, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("T12_ASSETS_READY " + TableScene);
        }
        private static AudioClip Clip(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "/Art/Audio/" + name + ".wav")
            ?? throw new InvalidOperationException("Missing authored audio: " + name);
        private static GameObject Model(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Art/Models/" + name + ".fbx")
            ?? throw new InvalidOperationException("Generate Blender art before updating the table: " + name);
        private static GameObject ModelInstance(string name, Material wood, Material felt, Material brass, Material ivory)
        {
            var instance = UnityEngine.Object.Instantiate(Model(name)); instance.name = name;
            foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>())
                renderer.sharedMaterials = System.Array.ConvertAll(renderer.sharedMaterials, material =>
                    material.name.StartsWith("Felt", StringComparison.Ordinal) ? felt : material.name.StartsWith("Brass", StringComparison.Ordinal) ? brass
                    : material.name.StartsWith("Tile", StringComparison.Ordinal) ? ivory : wood);
            return instance;
        }
        private static Material FaceMaterial()
        {
            var texturePath = Root + "/Art/Textures/TileFaces.png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
            importer.alphaIsTransparency = true; importer.mipmapEnabled = true; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096; importer.wrapMode = TextureWrapMode.Clamp; importer.SaveAndReimport();
            var path = Root + "/Art/Materials/TileFaces.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); AssetDatabase.CreateAsset(material, path); }
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            material.SetFloat("_Surface", 1); material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); material.SetFloat("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); material.renderQueue = 3000;
            EditorUtility.SetDirty(material); return material;
        }
        private static Material Material(string name, Color color, string texture = null)
        {
            var path = Root + "/Art/Materials/" + name + ".mat";
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("URP Lit shader is required.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
            material.shader = shader; material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", .20f);
            if (texture != null)
            {
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Art/Textures/" + texture + ".png"));
                material.SetTextureScale("_BaseMap", name == "Felt" ? new Vector2(5, 5) : new Vector2(2, 2));
            }
            if (name == "Brass") material.SetFloat("_Metallic", .65f);
            EditorUtility.SetDirty(material); return material;
        }
        private static GameObject SavePrefab(GameObject source, string path)
        {
            try
            {
                var prefab = PrefabUtility.SaveAsPrefabAsset(source, path);
                if (prefab == null) throw new IOException("Could not save prefab: " + path);
                return prefab;
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }
    }
}
