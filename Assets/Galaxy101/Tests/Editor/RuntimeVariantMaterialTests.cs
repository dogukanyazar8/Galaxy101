using System.Linq;
using Galaxy101.Table.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Galaxy101.Tests
{
    /// <summary>Player builds keep the shader variants of the table's code-made materials (RuntimeVariantMaterials).</summary>
    public sealed class RuntimeVariantMaterialTests
    {
        [Test]
        public void EveryKeeperCarriesExactlyTheKeywordsTheCodeEnables()
        {
            foreach (var (file, shader, keywords) in RuntimeVariantMaterials.Sets)
            {
                var material = AssetDatabase.LoadAssetAtPath<Material>(RuntimeVariantMaterials.Folder + "/" + file + ".mat");
                Assert.That(material, Is.Not.Null, file);
                Assert.That(material.shader.name, Is.EqualTo(shader), file);
                Assert.That(material.shaderKeywords.OrderBy(k => k), Is.EqualTo(keywords.OrderBy(k => k)), file + ": URP must not have re-derived other keywords");
            }
        }

        /// <summary>The design's glowing liquids and windows (tea, rocket window…) need _EMISSION, not just a colour.</summary>
        [Test]
        public void EmissiveTableMaterialsKeepTheirEmissionKeyword()
        {
            var missing = AssetDatabase.FindAssets("t:Material", new[] { "Assets/Galaxy101/Table/Materials" })
                .Select(guid => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(m => m != null && !m.shader.name.Contains("Unlit") && m.HasProperty("_EmissionColor") && m.GetColor("_EmissionColor").maxColorComponent > 0.001f && !m.IsKeywordEnabled("_EMISSION"))
                .Select(m => AssetDatabase.GetAssetPath(m)).ToList();
            Assert.That(missing, Is.Empty);
        }
    }
}
