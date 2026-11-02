using System.Linq;
using Galaxy101.Table;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Galaxy101.Tests
{
    public sealed class RackAssetFidelityTests
    {
        [TestCase("walnut")]
        [TestCase("sedef")]
        [TestCase("chrome")]
        [TestCase("carbon")]
        [TestCase("neon")]
        [TestCase("andromeda")]
        public void ExportedRacksKeepThirtyPlayableSlotsAndMipmappedFiltering(string skin)
        {
            var catalog = Resources.Load<TableAssetCatalog>("Galaxy101/Table/TableAssetCatalog");
            var rack = TableAssetCatalog.Find(catalog.Racks, skin);
            Assert.That(rack, Is.Not.Null);
            var slots = rack.GetComponentsInChildren<Transform>(true).Where(t => t.name.StartsWith("Slot_")).ToArray();
            Assert.That(slots.Length, Is.EqualTo(30), "Replacing a visual model must preserve the playable rack.");
            for (int row = 0; row < 2; row++) for (int col = 0; col < 15; col++)
            {
                var slot = slots.Single(s => s.name == $"Slot_{row}_{col}");
                Assert.That(Vector3.Distance(slot.localPosition, ThreeSpace.ToUnity(TableConstants.SlotLocal(row, col))), Is.LessThan(.000001f));
            }
            var textures = rack.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials)
                .Where(m => m != null && m.HasProperty("_BaseMap")).Select(m => m.GetTexture("_BaseMap")).Where(t => t != null).Distinct().ToArray();
            if (skin != "neon") Assert.That(textures.Length, Is.GreaterThan(0));
            foreach (var texture in textures)
            {
                Assert.That(texture.mipmapCount, Is.GreaterThan(1), texture.name);
                Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Trilinear), texture.name);
                Assert.That(texture.anisoLevel, Is.EqualTo(16), texture.name);
            }
        }

        [TestCase("Rack_walnut", "istaka_ceviz")]
        [TestCase("Rack_walnut", "istaka_ceviz_uc")]
        [TestCase("Rack_sedef", "istaka_gulagaci")]
        public void CurrentWoodUsesTheSeamlessSourceCanvasAndMeterBasedUvs(string folder, string material)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>($"Assets/Galaxy101/Table/Materials/{folder}/{material}.mat");
            Assert.That(m.GetTexture("_BaseMap").width, Is.EqualTo(1024));
            Assert.That(m.GetTexture("_BaseMap").height, Is.EqualTo(1024));
            Assert.That(m.GetTextureScale("_BaseMap"), Is.EqualTo(Vector2.one), "The new meter-based box UVs already encode the wood scale.");
            Assert.That(m.GetTextureOffset("_BaseMap"), Is.EqualTo(Vector2.zero));
            Assert.That(m.IsKeywordEnabled("_NORMALMAP"), Is.False, "The current source canvas has no normal map.");
        }

        [Test]
        public void PearlInlayKeepsTheSourcesNonPowerOfTwoCanvas()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/Galaxy101/Table/Materials/Rack_sedef/istaka_sedef_panel.mat");
            var texture = material.GetTexture("_BaseMap");
            Assert.That(texture.width, Is.EqualTo(768)); Assert.That(texture.height, Is.EqualTo(128));
            Assert.That(material.GetTextureScale("_BaseMap"), Is.EqualTo(new Vector2(2, 1)));
        }
    }
}
