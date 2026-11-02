using System.Reflection;
using Galaxy101.Table;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace Galaxy101.Tests
{
    public sealed class TableBoardSourceParityTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        [TestCase(0, 0, .49f, -.3f)]
        [TestCase(19, 7, -.4885f, .2999f)]
        [TestCase(14, 2, -.231f, -.1286f)]
        public void BoardCellsUseTheActiveHtmlOriginAndSpacing(int column, int row, float x, float z)
        {
            var go = new GameObject("board geometry test");
            try
            {
                var scene = go.AddComponent<TableScene>();
                var frames = (ThreeFrame[])typeof(TableScene).GetField("seatFrames", PrivateInstance).GetValue(scene); frames[0] = ThreeFrame.World;
                var pose = scene.BoardPose(column, row);
                Assert.That(pose.position.x, Is.EqualTo(x).Within(.000001f));
                Assert.That(pose.position.z, Is.EqualTo(z).Within(.000001f));
                Assert.That(pose.position.y, Is.EqualTo(TableConstants.TY + TableConstants.TD / 2).Within(.000001f));
                var tag = scene.BoardRowStart(row);
                Assert.That(tag.x, Is.EqualTo(.536f).Within(.000001f)); Assert.That(tag.z, Is.EqualTo(z).Within(.000001f));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void PairDividerAndCanvasLabelHaveTheSourceGeometryBlendAndDealFade()
        {
            var go = new GameObject("pair zone test");
            try
            {
                var scene = go.AddComponent<TableScene>();
                typeof(TableScene).GetMethod("BuildPairZone", PrivateInstance).Invoke(scene, null);
                var divider = go.transform.Find("cift_ayrac"); var label = go.transform.Find("cift_etiket");
                Assert.That(divider.position.x, Is.EqualTo(-.231f).Within(.000001f));
                Assert.That(label.position.x, Is.EqualTo(-.3855f).Within(.000001f)); Assert.That(label.position.z, Is.EqualTo(-.358f).Within(.000001f));
                Assert.That(divider.GetComponent<MeshFilter>().sharedMesh.bounds.size.x, Is.EqualTo(.003f).Within(.000001f));
                Assert.That(divider.GetComponent<MeshFilter>().sharedMesh.bounds.size.z, Is.EqualTo(.7f).Within(.000001f));
                Assert.That(label.GetComponent<MeshFilter>().sharedMesh.bounds.size.x, Is.EqualTo(.22f).Within(.000001f));
                Assert.That(label.GetComponent<MeshFilter>().sharedMesh.bounds.size.z, Is.EqualTo(.033f).Within(.000001f));
                var lineRenderer = divider.GetComponent<MeshRenderer>(); var labelRenderer = label.GetComponent<MeshRenderer>();
                Assert.That(lineRenderer.enabled, Is.False); Assert.That(labelRenderer.enabled, Is.False);
                scene.UpdatePairZone(true, .125f);
                Assert.That(lineRenderer.sharedMaterial.GetFloat("_DstBlend"), Is.EqualTo((int)BlendMode.One));
                Assert.That(lineRenderer.sharedMaterial.GetColor("_BaseColor").a, Is.EqualTo(.16f).Within(.000001f));
                Assert.That(labelRenderer.sharedMaterial.GetColor("_BaseColor").a, Is.EqualTo(.25f).Within(.000001f));
                Assert.That(labelRenderer.sharedMaterial.GetTexture("_BaseMap"), Is.EqualTo(Resources.Load<Texture2D>("Galaxy101/Table/Board/pairs-tr")));
                scene.DeckHeap = true; scene.UpdatePairZone(true, .25f);
                Assert.That(lineRenderer.enabled, Is.False); Assert.That(labelRenderer.enabled, Is.False);
                foreach (string language in new[] { "tr", "en" })
                {
                    var texture = Resources.Load<Texture2D>("Galaxy101/Table/Board/pairs-" + language);
                    Assert.That(texture, Is.Not.Null); Assert.That(texture.width, Is.EqualTo(512)); Assert.That(texture.height, Is.EqualTo(76));
                    Assert.That(texture.anisoLevel, Is.EqualTo(8)); Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Trilinear));
                }
            }
            finally { Object.DestroyImmediate(go); }
        }

        [TestCase(1.25f, 2f, .0085297893f, -.0086580178f)]
        [TestCase(12.345f, 12.765f, -.0054248740f, .0033874849f)]
        [TestCase(1f, 1f, 0f, 0f)]
        public void CameraShakeMatchesIndependentSourceSamplesWithoutConsumingRandom(float now, float until, float dx, float dy)
        {
            var go = new GameObject("camera shake test"); var cameraObject = new GameObject("test camera");
            var random = Random.state;
            try
            {
                var rig = go.AddComponent<TableCameraRig>(); var camera = cameraObject.AddComponent<Camera>(); camera.aspect = 16f / 9f;
                rig.Initialize(camera, TableCameraRig.View.Player);
                typeof(TableCameraRig).GetField("shakeUntil", PrivateInstance).SetValue(rig, until);
                Random.InitState(139); float next = Random.value; Random.InitState(139);
                var apply = typeof(TableCameraRig).GetMethod("Apply", PrivateInstance); apply.Invoke(rig, new object[] { now });
                var position = ThreeSpace.ToUnity(camera.transform.position);
                Assert.That(position.x - TableConstants.PlayerView.Position.x - Mathf.Sin(now * .21f) * .006f, Is.EqualTo(dx).Within(.000002f));
                Assert.That(position.y - TableConstants.PlayerView.Position.y - Mathf.Sin(now * .33f) * .004f, Is.EqualTo(dy).Within(.000002f));
                Assert.That(Random.value, Is.EqualTo(next));
                var first = camera.transform.position; apply.Invoke(rig, new object[] { now });
                Assert.That(camera.transform.position, Is.EqualTo(first));
            }
            finally { Random.state = random; Object.DestroyImmediate(go); Object.DestroyImmediate(cameraObject); }
        }
    }
}
