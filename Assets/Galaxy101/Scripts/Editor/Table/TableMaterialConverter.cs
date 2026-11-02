using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Galaxy101.Table.EditorTools
{
    /// <summary>
    /// Converts glTFast shader-graph materials into URP Lit / Complex Lit (clearcoat) / Unlit materials saved as
    /// assets (spec/14 §4: clearcoat via Complex Lit). Player builds then need no glTFast shader graphs, and the
    /// table's runtime theming works on URP's standard properties. glTF names are kept, so SkinService still
    /// finds materials by name; KHR_materials_emissive_strength is folded into the HDR emission colour.
    /// glTF metallicRoughness (G = roughness, B = metallic) is repacked to URP's metallic-gloss layout (R = metallic,
    /// A = smoothness).
    /// </summary>
    public static class TableMaterialConverter
    {
        private static readonly Dictionary<Material, Material> Cache = new Dictionary<Material, Material>();

        public static void ResetCache() => Cache.Clear();

        public static void ConvertAll(GameObject root, string folder)
        {
            Directory.CreateDirectory(folder);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var mats = renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) if (mats[i] != null) mats[i] = Convert(mats[i], folder);
                renderer.sharedMaterials = mats;
            }
        }

        private static Material Convert(Material source, string folder)
        {
            if (!source.shader.name.StartsWith("Shader Graphs/glTF", StringComparison.Ordinal)) return source;
            if (Cache.TryGetValue(source, out var done)) return done;
            string path = $"{folder}/{source.name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool unlit = source.shader.name.Contains("unlit");
            bool coat = source.shader.name.Contains("Clearcoat");
            var shader = Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : coat ? "Universal Render Pipeline/Complex Lit" : "Universal Render Pipeline/Lit");
            var m = existing != null ? existing : new Material(shader);
            m.shader = shader; m.name = source.name;
            m.shaderKeywords = Array.Empty<string>();

            var baseColor = Get(source, "baseColorFactor", Color.white);
            var baseTex = Tex(source, "baseColorTexture");
            m.SetColor("_BaseColor", baseColor);
            if (baseTex != null) { m.SetTexture("_BaseMap", baseTex); CopyTransform(source, "baseColorTexture", m); }

            bool transparent = source.renderQueue >= (int)RenderQueue.Transparent || Float(source, "_Surface", 0) > 0.5f;
            if (transparent)
            {
                bool additive = Float(source, "_DstBlend", 0) == (float)BlendMode.One;
                m.SetFloat("_Surface", 1); m.SetFloat("_Blend", additive ? 2 : 0); m.SetFloat("_ZWrite", 0);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
                // Destination alpha is the HUD's coverage of the 3D layer: blend "over", additive glows leave it alone.
                m.SetFloat("_SrcBlendAlpha", additive ? (float)BlendMode.Zero : (float)BlendMode.One); m.SetFloat("_DstBlendAlpha", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); m.renderQueue = (int)RenderQueue.Transparent;
                m.SetOverrideTag("RenderType", "Transparent");
                // three.js scales specular and reflections by the opacity too; URP's "Preserve Specular" would keep them at
                // full strength and veil a 30 % glass (tea, cola) in white.
                m.SetFloat("_BlendModePreserveSpecular", 0); m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                // three.js blends in sRGB after tone mapping; ThreeBlend gives the same veil in the linear HDR target.
                if (!additive && m.name != "holo") { var c = m.GetColor("_BaseColor"); c.a = Galaxy101.Table.ThreeBlend.Normal(c.a); m.SetColor("_BaseColor", c); }
                // Glass reflects the table's environment: the design's dark studio box (ThreeEnvironment), as in three.js.
                m.SetFloat("_EnvironmentReflections", 1); m.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            }
            else { m.SetFloat("_Surface", 0); m.SetFloat("_ZWrite", 1); m.renderQueue = -1; m.SetOverrideTag("RenderType", "Opaque"); }
            if (Float(source, "_Cull", 2) == 0) m.SetFloat("_Cull", 0);

            if (!unlit)
            {
                float metallic = Float(source, "metallicFactor", 1), roughness = Float(source, "roughnessFactor", 1);
                var mr = Tex(source, "metallicRoughnessTexture");
                if (mr != null)
                {
                    m.SetTexture("_MetallicGlossMap", Repack(mr, metallic, roughness, folder + "/" + source.name + "_MetallicGloss.png"));
                    m.SetFloat("_Smoothness", 1); m.SetFloat("_Metallic", 1); m.EnableKeyword("_METALLICSPECGLOSSMAP");
                }
                else { m.SetTexture("_MetallicGlossMap", null); m.SetFloat("_Metallic", metallic); m.SetFloat("_Smoothness", 1 - roughness); }
                var normal = Tex(source, "normalTexture");
                if (normal != null) { m.SetTexture("_BumpMap", ExtractNormal(normal, folder + "/" + source.name + "_Normal.png")); m.SetFloat("_BumpScale", Float(source, "normalTexture_scale", 1)); m.EnableKeyword("_NORMALMAP"); }
                else m.SetTexture("_BumpMap", null);
                var emissive = Get(source, "emissiveFactor", Color.black);
                // Some glTFast shader variants (clearcoat liquids: tea, cola, syrup) drop emissiveFactor; read it from the GLB.
                if (emissive.maxColorComponent <= 0 && GlbEmissive(source) is Color fromGlb) emissive = fromGlb;
                var emissiveTex = Tex(source, "emissiveTexture");
                if (emissive.maxColorComponent > 0 || emissiveTex != null)
                {
                    float strength = EmissiveStrength(source);
                    m.SetColor("_EmissionColor", new Color(emissive.r * strength, emissive.g * strength, emissive.b * strength, 1)); if (emissiveTex != null) m.SetTexture("_EmissionMap", emissiveTex);
                    // RealtimeEmissive (not None/EmissiveIsBlack): URP's material validation keeps _EMISSION only then.
                    m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                if (coat)
                {
                    m.SetFloat("_ClearCoat", 1); m.EnableKeyword("_CLEARCOAT");
                    m.SetFloat("_ClearCoatMask", Float(source, "clearcoatFactor", 1));
                    m.SetFloat("_ClearCoatSmoothness", 1 - Float(source, "clearcoatRoughnessFactor", 0.1f));
                }
            }
            if (existing == null) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
            Cache[source] = m; return m;
        }

        private static readonly Dictionary<string, Dictionary<string, float>> Strengths = new Dictionary<string, Dictionary<string, float>>();
        private static readonly Dictionary<string, Dictionary<string, Color>> Emissives = new Dictionary<string, Dictionary<string, Color>>();

        /// <summary>KHR_materials_emissive_strength from the source GLB (glTFast keeps emissiveFactor within 0–1).</summary>
        private static float EmissiveStrength(Material source)
        {
            string path = AssetDatabase.GetAssetPath(source);
            if (!path.EndsWith(".glb", StringComparison.OrdinalIgnoreCase)) return 1;
            if (!Strengths.TryGetValue(path, out var map))
            {
                map = new Dictionary<string, float>(); var emissives = new Dictionary<string, Color>();
                var bytes = File.ReadAllBytes(path); int length = BitConverter.ToInt32(bytes, 12);
                var json = Newtonsoft.Json.Linq.JObject.Parse(System.Text.Encoding.UTF8.GetString(bytes, 20, length));
                foreach (var mat in json["materials"] ?? new Newtonsoft.Json.Linq.JArray())
                {
                    var s = mat["extensions"]?["KHR_materials_emissive_strength"]?["emissiveStrength"];
                    if (s != null) map[(string)mat["name"]] = (float)s;
                    if (mat["emissiveFactor"] is Newtonsoft.Json.Linq.JArray e && e.Count == 3) emissives[(string)mat["name"]] = new Color((float)e[0], (float)e[1], (float)e[2], 1);
                }
                Strengths[path] = map; Emissives[path] = emissives;
            }
            return map.TryGetValue(source.name, out var value) ? value : 1;
        }

        /// <summary>emissiveFactor of the GLB material with the source's name, if it is not black.</summary>
        private static Color? GlbEmissive(Material source)
        {
            EmissiveStrength(source);
            string path = AssetDatabase.GetAssetPath(source);
            return Emissives.TryGetValue(path, out var map) && map.TryGetValue(source.name, out var c) && c.maxColorComponent > 0 ? c : (Color?)null;
        }

        private static Color Get(Material m, string p, Color fallback) => m.HasProperty(p) ? m.GetColor(p) : fallback;
        private static float Float(Material m, string p, float fallback) => m.HasProperty(p) ? m.GetFloat(p) : fallback;
        private static Texture2D Tex(Material m, string p) => m.HasProperty(p) ? m.GetTexture(p) as Texture2D : null;

        private static void CopyTransform(Material source, string slot, Material target)
        {
            if (!source.HasProperty(slot + "_ST")) return;
            var st = source.GetVector(slot + "_ST");
            target.SetTextureScale("_BaseMap", new Vector2(st.x, st.y)); target.SetTextureOffset("_BaseMap", new Vector2(st.z, st.w));
        }

        /// <summary>glTF normal maps live inside the GLB; URP needs them imported as Normal Map textures.</summary>
        private static Texture2D ExtractNormal(Texture2D normal, string path)
        {
            var readable = Readable(normal);
            File.WriteAllBytes(path, readable.EncodeToPNG());
            if (readable != normal) UnityEngine.Object.DestroyImmediate(readable);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.NormalMap; importer.maxTextureSize = 2048; importer.wrapMode = normal.wrapMode; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>glTF MR (G roughness, B metallic, scaled by the factors) → URP (R metallic, A smoothness).</summary>
        private static Texture2D Repack(Texture2D mr, float metallicFactor, float roughnessFactor, string path)
        {
            var readable = Readable(mr);
            var pixels = readable.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                float metal = pixels[i].b * metallicFactor, rough = pixels[i].g * roughnessFactor;
                pixels[i] = new Color(metal, metal, metal, 1 - rough);
            }
            var output = new Texture2D(readable.width, readable.height, TextureFormat.RGBA32, true, true);
            output.SetPixels(pixels); output.Apply();
            File.WriteAllBytes(path, output.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(output); if (readable != mr) UnityEngine.Object.DestroyImmediate(readable);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.sRGBTexture = false; importer.maxTextureSize = 2048; importer.wrapMode = mr.wrapMode; importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Texture2D Readable(Texture2D texture)
        {
            if (texture.isReadable) return texture;
            var rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(texture, rt);
            var previous = RenderTexture.active; RenderTexture.active = rt;
            var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false, true);
            copy.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); copy.Apply();
            RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt);
            return copy;
        }
    }
}
