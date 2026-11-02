using System.IO;
using UnityEditor;
using UnityEngine;

namespace Galaxy101.Table.EditorTools
{
    /// <summary>
    /// The table builds several materials in code (transparent tile faces, FX, targets, the felt's specular setup). Their
    /// keywords are URP <c>shader_feature</c>s, which a player build compiles only when some built material uses them —
    /// otherwise the player silently draws the nearest variant: opaque tile faces with dark margins, white square
    /// particles. These keeper materials in Resources carry every keyword set the code enables, so the variants ship.
    /// Batch: -executeMethod Galaxy101.Table.EditorTools.RuntimeVariantMaterials.Ensure (also run by the Windows build).
    /// </summary>
    public static class RuntimeVariantMaterials
    {
        public const string Folder = "Assets/Galaxy101/Resources/Galaxy101/Table/RuntimeVariants";

        /// <summary>(file, shader, keywords) — keep in step with TileFactory, TableFx, TableTargets, TableScene, SkinService.</summary>
        public static readonly (string File, string Shader, string[] Keywords)[] Sets =
        {
            ("lit_transparent", "Universal Render Pipeline/Lit", new[] { "_SURFACE_TYPE_TRANSPARENT" }),
            ("lit_transparent_emission", "Universal Render Pipeline/Lit", new[] { "_SURFACE_TYPE_TRANSPARENT", "_EMISSION" }),
            ("lit_specular", "Universal Render Pipeline/Lit", new[] { "_SPECULAR_SETUP" }),
            ("lit_specular_emission", "Universal Render Pipeline/Lit", new[] { "_SPECULAR_SETUP", "_EMISSION" }),
            ("lit_specular_normal", "Universal Render Pipeline/Lit", new[] { "_SPECULAR_SETUP", "_NORMALMAP" }),
            ("lit_specular_normal_emission", "Universal Render Pipeline/Lit", new[] { "_SPECULAR_SETUP", "_NORMALMAP", "_EMISSION" }),
            ("unlit_transparent", "Universal Render Pipeline/Unlit", new[] { "_SURFACE_TYPE_TRANSPARENT" }),
            ("particles_unlit_transparent", "Universal Render Pipeline/Particles/Unlit", new[] { "_SURFACE_TYPE_TRANSPARENT" }),
        };

        [MenuItem("Galaxy101/Ensure Runtime Variant Materials")]
        public static void Ensure()
        {
            Directory.CreateDirectory(Folder);
            foreach (var (file, shaderName, keywords) in Sets)
            {
                string path = Folder + "/" + file + ".mat";
                var shader = Shader.Find(shaderName);
                if (shader == null) { Debug.LogError("Galaxy101: missing shader " + shaderName); continue; }
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                bool created = material == null;
                if (created) material = new Material(shader) { name = file };
                material.shader = shader;
                material.shaderKeywords = keywords;
                bool transparent = System.Array.IndexOf(keywords, "_SURFACE_TYPE_TRANSPARENT") >= 0;
                if (material.HasProperty("_Surface")) material.SetFloat("_Surface", transparent ? 1 : 0);
                if (material.HasProperty("_WorkflowMode")) material.SetFloat("_WorkflowMode", System.Array.IndexOf(keywords, "_SPECULAR_SETUP") >= 0 ? 0 : 1);
                material.renderQueue = transparent ? 3000 : -1;
                // URP re-derives keywords from properties when the asset is saved: plain alpha blending (no premultiply),
                // an emission colour for _EMISSION, a normal map for _NORMALMAP.
                if (material.HasProperty("_Blend")) material.SetFloat("_Blend", 0);
                if (material.HasProperty("_BlendModePreserveSpecular")) material.SetFloat("_BlendModePreserveSpecular", 0);
                bool emission = System.Array.IndexOf(keywords, "_EMISSION") >= 0;
                if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", emission ? Color.white : Color.black);
                if (emission) material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                if (System.Array.IndexOf(keywords, "_NORMALMAP") >= 0 && material.HasProperty("_BumpMap"))
                    material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Galaxy101/Table/Materials/Table_station/cuha_Normal.png"));
                material.shaderKeywords = keywords;
                if (created) AssetDatabase.CreateAsset(material, path); else EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Galaxy101: runtime variant materials ready in " + Folder);
        }
    }
}
