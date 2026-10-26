using Galaxy101.Table;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Galaxy101.Tests
{
    public sealed class TileFactoryTagTests
    {
        [Test]
        public void TagsReuseOneQuadAndTwoMaterialsAndReleaseOwnedAssets()
        {
            var catalog = TableAssetCatalog.Load();
            var source = Object.Instantiate(TableAssetCatalog.Find(catalog.TileSets, "classic"));
            var root = new GameObject("Tile badge ownership test");
            TileFactory factory = null;
            try
            {
                factory = new TileFactory(source, "classic", .4f, 0);
                var first = factory.Create("R7", root.transform);
                int plainChildren = first.transform.childCount;
                factory.SetTag(first, null);
                Assert.That(first.transform.childCount, Is.EqualTo(plainChildren), "Hiding an unused badge must allocate no object.");

                factory.SetTag(first, "side");
                var badge = first.transform.Find("tas_simge");
                Assert.That(badge, Is.Not.Null);
                var renderer = badge.GetComponent<MeshRenderer>();
                var mesh = badge.GetComponent<MeshFilter>().sharedMesh;
                var side = renderer.sharedMaterial;
                Assert.That(Vector3.Distance(badge.localPosition, new Vector3(-.018f, .038f, .0062f)), Is.LessThan(1e-6f));
                Assert.That(mesh.bounds.size.x, Is.EqualTo(.025f).Within(1e-6f));
                Assert.That(mesh.bounds.size.y, Is.EqualTo(.025f).Within(1e-6f));
                Assert.That(mesh.normals, Is.All.EqualTo(Vector3.forward));
                Assert.That(mesh.vertices[0].x, Is.GreaterThan(0));
                Assert.That(mesh.uv[0], Is.EqualTo(Vector2.zero), "Like FaceMesh, u grows toward the player's right (-X).");
                Assert.That(mesh.uv[2], Is.EqualTo(Vector2.one));
                var vertices = mesh.vertices; var indices = mesh.triangles;
                Assert.That(Vector3.Cross(vertices[indices[1]] - vertices[indices[0]], vertices[indices[2]] - vertices[indices[0]]).z, Is.GreaterThan(0));
                Assert.That(side.name, Is.EqualTo("simge_side"));
                Assert.That(side.mainTexture.width, Is.EqualTo(128));
                Assert.That(side.mainTexture.height, Is.EqualTo(128));
                Assert.That(side.renderQueue, Is.EqualTo((int)RenderQueue.Transparent + 3));
                Assert.That(side.GetFloat("_ZWrite"), Is.Zero);
                Assert.That(side.GetInt("_SrcBlend"), Is.EqualTo((int)BlendMode.SrcAlpha));
                Assert.That(side.GetInt("_DstBlend"), Is.EqualTo((int)BlendMode.OneMinusSrcAlpha));

                factory.SetTag(first, "islek");
                var islek = renderer.sharedMaterial;
                Assert.That(islek.name, Is.EqualTo("simge_islek"));
                Assert.That(islek, Is.Not.SameAs(side));
                factory.SetTag(first, null);
                Assert.That(badge.gameObject.activeSelf, Is.False);
                factory.SetTag(first, "side"); factory.SetTag(first, "side");
                Assert.That(first.transform.childCount, Is.EqualTo(plainChildren + 1));
                Assert.That(first.transform.Find("tas_simge"), Is.SameAs(badge));
                Assert.That(renderer.sharedMaterial, Is.SameAs(side));
                Assert.That(badge.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));

                var second = factory.Create("B4", root.transform); factory.SetTag(second, "islek");
                var secondBadge = second.transform.Find("tas_simge");
                Assert.That(secondBadge.GetComponent<MeshRenderer>().sharedMaterial, Is.SameAs(islek));
                Assert.That(secondBadge.GetComponent<MeshFilter>().sharedMesh, Is.SameAs(mesh));
                factory.Dispose(); factory = null;
                Assert.That(side == null && islek == null && mesh == null, Is.True, "The factory releases both shared badge materials and its mesh.");
            }
            finally
            {
                factory?.Dispose(); Object.DestroyImmediate(root); Object.DestroyImmediate(source);
            }
        }
    }
}
