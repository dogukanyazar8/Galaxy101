using System.Reflection;
using Galaxy101.Table;
using NUnit.Framework;
using UnityEngine;

namespace Galaxy101.Tests
{
    public sealed class SkinServiceV4Tests
    {
        private static void Skin(SkinService service, string skin)
        {
            // Keeps the regression runnable against the earlier implementation; absent skin handling still fails on
            // the resulting colours below, rather than on a missing-method/compilation error.
            typeof(SkinService).GetMethod("SetTableSkin", BindingFlags.Public | BindingFlags.Instance)?.Invoke(service, new object[] { skin });
        }

        private static TableTheme Theme(Color accent, Color second) => new TableTheme { A1 = accent, A2 = second, Rim = Color.gray, Leather = Color.black, Tile = Color.white };

        [Test]
        public void AndromedaSurvivesPlanetChangesAndLeavingItRestoresThePlanetsAccents()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var source = new Material(shader) { name = "holo" }; source.SetColor("_BaseColor", new Color(.2f, .3f, .4f, .35f));
            var importedColor = source.GetColor("_BaseColor");
            var haloSource = new Material(shader) { name = "hale" }; haloSource.SetColor("_BaseColor", new Color(.4f, .3f, .2f, .25f));
            var root = new GameObject("Polished table materials"); var renderer = root.AddComponent<MeshRenderer>(); renderer.sharedMaterials = new[] { source, haloSource };
            var service = new SkinService();
            try
            {
                service.SetTheme(Theme(Color.red, Color.green)); Skin(service, "andromeda"); service.Adopt(root);
                var holo = renderer.sharedMaterials[0]; var halo = renderer.sharedMaterials[1];
                Assert.That(holo, Is.Not.SameAs(source));
                ColorUtility.TryParseHtmlString("#B79CFF", out var andromedaHolo); ColorUtility.TryParseHtmlString("#7F9CFF", out var andromedaHalo);
                AssertColor(holo, andromedaHolo.linear, .35f); AssertColor(halo, andromedaHalo.linear, .25f);
                service.SetTheme(Theme(Color.blue, Color.yellow));
                AssertColor(holo, andromedaHolo.linear, .35f); AssertColor(halo, andromedaHalo.linear, .25f);
                Skin(service, "station"); AssertColor(holo, Color.blue.linear, .35f); AssertColor(halo, Color.yellow.linear, .25f);
                Assert.That(source.GetColor("_BaseColor"), Is.EqualTo(importedColor), "Imported polished materials must remain unchanged.");
            }
            finally { service.Release(); Object.DestroyImmediate(root); Object.DestroyImmediate(source); Object.DestroyImmediate(haloSource); }
        }

        [Test]
        public void HologramFeltFollowsThePlanetThenStopsEmittingOnAnOpaqueSkin()
        {
            var source = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "cuha" };
            var root = new GameObject("Polished felt"); var renderer = root.AddComponent<MeshRenderer>(); renderer.sharedMaterial = source;
            var service = new SkinService();
            try
            {
                service.SetTheme(Theme(Color.red, Color.green)); Skin(service, "holo"); service.Adopt(root);
                var felt = renderer.sharedMaterial;
                Assert.That(felt.IsKeywordEnabled("_EMISSION"), Is.True);
                Assert.That(felt.GetColor("_EmissionColor").r, Is.EqualTo(.35f).Within(1e-5));
                service.SetTheme(Theme(Color.blue, Color.green));
                Assert.That(felt.GetColor("_EmissionColor").b, Is.EqualTo(.35f).Within(1e-5));
                Assert.That(felt.GetColor("_EmissionColor").r, Is.EqualTo(0).Within(1e-5));
                Skin(service, "wood"); Assert.That(felt.IsKeywordEnabled("_EMISSION"), Is.False);
                Assert.That(felt.GetColor("_EmissionColor").maxColorComponent, Is.EqualTo(0));
                Assert.That(source.IsKeywordEnabled("_EMISSION"), Is.False);
            }
            finally { service.Release(); Object.DestroyImmediate(root); Object.DestroyImmediate(source); }
        }

        private static void AssertColor(Material material, Color color, float alpha)
        {
            var actual = material.GetColor("_BaseColor");
            Assert.That(actual.r, Is.EqualTo(color.r).Within(1e-5)); Assert.That(actual.g, Is.EqualTo(color.g).Within(1e-5)); Assert.That(actual.b, Is.EqualTo(color.b).Within(1e-5));
            Assert.That(actual.a, Is.EqualTo(alpha).Within(1e-5));
        }
    }
}
