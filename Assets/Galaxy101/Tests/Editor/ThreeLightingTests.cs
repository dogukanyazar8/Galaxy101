using Galaxy101.Table;
using NUnit.Framework;
using UnityEngine;

namespace Galaxy101.Tests
{
    /// <summary>The table's design lighting conversions (three.js r184 → URP).</summary>
    public sealed class ThreeLightingTests
    {
        private static Color Eval(UnityEngine.Rendering.SphericalHarmonicsL2 sh, Vector3 normal)
        {
            var result = new Color[1]; sh.Evaluate(new[] { normal }, result); return result[0];
        }

        [Test]
        public void HemisphereLightComesFromAboveAtThreeJsStrength()
        {
            // Only the hemisphere light: three lights an up-facing surface with sky × 0.45 / π (BRDF_Lambert).
            var sky = new Color(0.8f, 0.85f, 1f); var ground = Color.black;
            var withRoom = ThreeEnvironment.AmbientProbe(Color.black, sky, ground, 0.45f);
            var up = Eval(withRoom, Vector3.up); var down = Eval(withRoom, Vector3.down);
            Assert.That(up.b, Is.GreaterThan(down.b * 3), "sky above, dark ground below");
            // Unity's ambient term is albedo × probe(n); three's is albedo × E / π with E(up) = sky.linear × 0.45.
            var roomOnly = ThreeEnvironment.AmbientProbe(Color.black, Color.black, Color.black, 0);
            float hemi = up.b - Eval(roomOnly, Vector3.up).b, expected = sky.linear.b * 0.45f / Mathf.PI;
            Assert.That(hemi, Is.EqualTo(expected).Within(expected * 0.05f), "hemisphere term, no extra π");
            // The ceiling panel (5 × 1.4 m, 3.2×, 5.5 m up) adds ≈ 3.2 × 0.75 × 0.23 sr / π on an up-facing surface.
            Assert.That(Eval(roomOnly, Vector3.up).b, Is.InRange(0.12f, 0.24f));
        }

        [Test]
        public void CeilingPanelBrightensUpFacingSurfacesMostOfTheRoom()
        {
            var sh = ThreeEnvironment.AmbientProbe(new Color(1, 0.79f, 0.29f), Color.black, Color.black, 0);
            var up = Eval(sh, Vector3.up).grayscale;
            foreach (var n in new[] { Vector3.down, Vector3.left, Vector3.right, Vector3.forward })
                Assert.That(up, Is.GreaterThan(Eval(sh, n).grayscale), "the 3.2× ceiling panel is above: " + n);
        }

        [Test]
        public void ThreeLightIntensitiesLoseTheirPi()
        {
            Assert.That(TableScene.ThreeToUrp * Mathf.PI, Is.EqualTo(1).Within(1e-6f));
        }
    }
}
