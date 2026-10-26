using Galaxy101.Table;
using NUnit.Framework;
using UnityEngine;

namespace Galaxy101.Tests
{
    public sealed class TableSpaceTests
    {
        [Test]
        public void PackageLoadingAndCameraTextsResolveInBothLanguages()
        {
            Assert.That(TableStrings.Has("app.STR.ldMsg.0"), Is.True, "The loading UI must read the v4 string table, not parse JSON as CSV.");
            Assert.That(TableStrings.Get("app.STR.ldMsg.0", "tr"), Is.EqualTo("Masaya bağlanılıyor…"));
            Assert.That(TableStrings.Get("table.STR.views.1.0", "tr"), Is.EqualTo("Yarı yüksek"));
            Assert.That(TableStrings.Get("table.STR.views.1.0", "en"), Is.EqualTo("Mid view"));
        }

        [Test]
        public void HashMatchesThePrototype()
        {
            // Reference values from table3d.js hash(): ((h * 31 + c) | 0) >>> 0) % 1000 / 1000, h0 = 7.
            Assert.That(TableConstants.Hash("d1"), Is.EqualTo(0.876f).Within(1e-6f));
            Assert.That(TableConstants.Hash("R7"), Is.EqualTo(0.324f).Within(1e-6f));
            Assert.That(TableConstants.Hash("tas_B12"), Is.EqualTo(0.051f).Within(1e-6f));
            Assert.That(TableConstants.Hash("deck"), Is.EqualTo(0.984f).Within(1e-6f));
        }

        [Test]
        public void MirrorMatchesGltfastHandedness()
        {
            // glTFast mirrors X: a three.js point at +x lands at −x; rotations keep their visual sense.
            Assert.That(ThreeSpace.ToUnity(new Vector3(1, 2, 3)), Is.EqualTo(new Vector3(-1, 2, 3)));
            var seat = ThreeSpace.Euler(0, Mathf.PI / 2, 0);
            var threeRight = seat * Vector3.right;
            var unityRight = ThreeSpace.ToUnity(seat) * ThreeSpace.ToUnity(Vector3.right);
            Assert.That(Vector3.Distance(ThreeSpace.ToUnity(threeRight), unityRight), Is.LessThan(1e-5f));
        }

        [Test]
        public void SlotsFollowTheRackContract()
        {
            // spec/09 §7: x = (i − 7)·SL; bottom row y 0.045, z −0.003 + TD/2; top row y 0.128, z −0.022 + TD/2.
            var bottom = TableConstants.SlotLocal(1, 0); var top = TableConstants.SlotLocal(0, 14);
            Assert.That(bottom.x, Is.EqualTo(-7 * 0.053f).Within(1e-6f));
            Assert.That(bottom.y, Is.EqualTo(0.045f).Within(1e-6f)); Assert.That(bottom.z, Is.EqualTo(0.002f).Within(1e-6f));
            Assert.That(top.x, Is.EqualTo(7 * 0.053f).Within(1e-6f));
            Assert.That(top.y, Is.EqualTo(0.128f).Within(1e-6f)); Assert.That(top.z, Is.EqualTo(-0.017f).Within(1e-6f));
        }

        [Test]
        public void FaceAtlasCells()
        {
            Assert.That(TileFactory.CellOf("R1"), Is.EqualTo(0));
            Assert.That(TileFactory.CellOf("B1"), Is.EqualTo(13));
            Assert.That(TileFactory.CellOf("Y13"), Is.EqualTo(38));
            Assert.That(TileFactory.CellOf("K13"), Is.EqualTo(51));
            Assert.That(TileFactory.CellOf("joker"), Is.EqualTo(52));
            Assert.Throws<System.ArgumentException>(() => TileFactory.CellOf("R14"));
        }
    }
}
