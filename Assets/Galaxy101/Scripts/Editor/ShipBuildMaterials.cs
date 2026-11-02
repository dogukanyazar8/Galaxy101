using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using GLTFast;
using UnityEditor;
using UnityEngine;

namespace Galaxy101.EditorTools
{
    /// <summary>Keeps exactly the shader feature combinations used by the original runtime-imported ship models.</summary>
    public static class ShipBuildMaterials
    {
        private const string Source = "Assets/Galaxy101/Resources/Galaxy101/Ships";
        private const string Folder = Source + "/RuntimeVariants";
        private const string ManifestPath = Folder + "/manifest.json";
        [Serializable] private sealed class Manifest
        {
            public string SourceHash;
            public string[] MaterialPaths;
        }

        [MenuItem("Galaxy101/Rebuild Ship Material Variants")]
        public static async void Rebuild()
        {
            try
            {
                await Generate();
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }

        public static async Task Generate()
        {
            Directory.CreateDirectory(Folder);
            var paths = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (string file in SourceFiles())
            {
                using var import = new GltfImport(deferAgent: new UninterruptedDeferAgent(), logger: new GLTFast.Logging.ConsoleLogger());
                if (!await import.Load(File.ReadAllBytes(file))) throw new InvalidOperationException("Cannot prepare ship build materials from " + file);
                for (int index = 0; index < import.MaterialCount; index++)
                {
                    var original = import.GetMaterial(index);
                    if (original == null || original.shader == null) throw new InvalidOperationException("Missing imported ship material: " + file + " #" + index);
                    string key = original.shader.name + "|" + string.Join(" ", original.shaderKeywords.OrderBy(k => k, StringComparer.Ordinal));
                    if (paths.ContainsKey(key)) continue;
                    string path = Folder + "/variant_" + Hash(Encoding.UTF8.GetBytes(key)).Substring(0, 16) + ".mat";
                    // These assets retain compiler variants only. Runtime imports still supply original GLB textures.
                    // EXT_mesh_gpu_instancing enables this flag when the scene is instantiated, after material import.
                    var keeper = new Material(original) { name = Path.GetFileNameWithoutExtension(path), hideFlags = HideFlags.None, enableInstancing = true };
                    // Match glTFast's SrcAlpha blending; URP's asset upgrader otherwise enables premultiplication.
                    if (keeper.HasProperty("_BlendModePreserveSpecular")) keeper.SetFloat("_BlendModePreserveSpecular", 0);
                    foreach (string property in keeper.GetTexturePropertyNames())
                        if (keeper.GetTexture(property) != null) keeper.SetTexture(property, Texture2D.whiteTexture);
                    keeper.shaderKeywords = original.shaderKeywords;
                    var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (existing == null) AssetDatabase.CreateAsset(keeper, path);
                    else { EditorUtility.CopySerialized(keeper, existing); UnityEngine.Object.DestroyImmediate(keeper); EditorUtility.SetDirty(existing); }
                    paths.Add(key, path);
                }
            }
            if (paths.Count == 0) throw new InvalidOperationException("No original ship shader variants were generated.");
            AssetDatabase.SaveAssets();
            File.WriteAllText(ManifestPath, JsonUtility.ToJson(new Manifest { SourceHash = SourceHash(), MaterialPaths = paths.Values.ToArray() }, true));
            AssetDatabase.ImportAsset(ManifestPath);
            RequireCurrent();
            Debug.Log("GALAXY101_SHIP_VARIANTS_READY count=" + paths.Count);
        }

        public static void RequireCurrent()
        {
            const string action = " Run Galaxy101/Rebuild Ship Material Variants before building.";
            if (!File.Exists(ManifestPath)) throw new InvalidOperationException("Ship material variants are missing." + action);
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(ManifestPath));
            if (manifest == null || manifest.SourceHash != SourceHash() || manifest.MaterialPaths == null || manifest.MaterialPaths.Length == 0)
                throw new InvalidOperationException("Ship material variants are stale." + action);
            foreach (string path in manifest.MaterialPaths)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == null) throw new InvalidOperationException("Missing ship material variant: " + path + "." + action);
            }
        }

        private static string[] SourceFiles()
        {
            var files = Directory.GetFiles(Source, "*.bytes").OrderBy(p => p, StringComparer.Ordinal).ToArray();
            if (files.Length != 5) throw new InvalidOperationException("Expected the five original ship GLBs in " + Source);
            return files;
        }
        private static string SourceHash() => Hash(Encoding.UTF8.GetBytes(string.Join("\n", SourceFiles().Select(p => Path.GetFileName(p) + ":" + Hash(File.ReadAllBytes(p))))));
        private static string Hash(byte[] data)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
        }
    }
}
