using System.Reflection;
using Galaxy101.Table;
using NUnit.Framework;
using UnityEngine;

namespace Galaxy101.Tests
{
    public sealed class DealTileAnimationTests
    {
        private GameObject tile;

        [TearDown]
        public void TearDown()
        {
            if (tile != null) Object.DestroyImmediate(tile);
        }

        [Test]
        public void DealFlightDurationFollowsDistanceInsteadOfTheOldFixedSwirl()
        {
            tile = new GameObject("tas_t1");
            var view = tile.AddComponent<TileView>();
            view.Snap(new Pose(Vector3.zero, Quaternion.identity));
            view.MoveTo(new Pose(new Vector3(.3f, .4f, 0), Quaternion.identity), TileView.Motion.Deal, .08f);
            Assert.That(view.Duration, Is.EqualTo(.66f).Within(.00001f), "table3d.js deal: .56 + clamp(distance * .2, 0, .24). Delay does not shorten the flight.");
        }

        [Test]
        public void DealFlightUsesTheSourceSinusoidalBezierAndWorldQuarterTurn()
        {
            tile = new GameObject("tas_t1");
            var view = tile.AddComponent<TileView>();
            view.Id = 0;
            var from = new Pose(new Vector3(.2f, .8f, .3f), Quaternion.Euler(30, 20, 10));
            var to = new Pose(new Vector3(-.4f, 1, -.1f), Quaternion.Euler(-40, 70, 15));
            view.Snap(from); view.MoveTo(to, TileView.Motion.Deal);
            typeof(TileView).GetField("t0", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(view, Time.time - view.Duration * .25f);
            typeof(TileView).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(view, null);
            // Independent table3d.js fixture, hash('t1x')=.652, hash('t1y')=.653, hash('t1s')=.647.
            Assert.That(Vector3.Distance(tile.transform.position, new Vector3(.16866350f, .86387386f, .28073075f)), Is.LessThan(.00001f));
            var expected = Quaternion.AngleAxis(-80.683594f, Vector3.up) * Quaternion.Slerp(from.rotation, to.rotation, .1257232f);
            Assert.That(Quaternion.Angle(tile.transform.rotation, expected), Is.LessThan(.03f), "Mirror X and premultiply world spin; do not postmultiply in tile coordinates.");
            Assert.That(tile.transform.localScale, Is.EqualTo(Vector3.one), "A plain deal has two small vertical hops after landing, never a scale pop.");
        }

        [Test]
        public void BlockTransportTiltsAroundTheTravelNormalAndKeepsEveryTileAtTheSameProgress()
        {
            var from = new Pose(new Vector3(.2f, .8f, .3f), Quaternion.Euler(30, 20, 10));
            var to = new Pose(new Vector3(-.4f, 1, -.1f), Quaternion.Euler(-40, 70, 15));
            var motion = new DealTileMotion("t1", from, to, DealTileMotion.Kind.Block);
            Assert.That(motion.Duration, Is.EqualTo(.62463293f).Within(.000001f));
            var quarter = motion.Sample(motion.Duration * .25f);
            Assert.That(Vector3.Distance(quarter.position, new Vector3(.11213203f, .86698622f, .24142136f)), Is.LessThan(.000001f));
            var expected = Quaternion.AngleAxis(.077781746f * Mathf.Rad2Deg, new Vector3(-.4f, 0, .6f).normalized) * Quaternion.Slerp(from.rotation, to.rotation, .103515625f);
            Assert.That(Quaternion.Angle(quarter.rotation, expected), Is.LessThan(.03f));
            var forced = new DealTileMotion("t2", from, to, DealTileMotion.Kind.Block, .85f);
            Assert.That(forced.Duration, Is.EqualTo(.85f));
            Assert.That(Vector3.Distance(forced.Sample(.85f).position, to.position), Is.LessThan(.000001f));
        }

        [Test]
        public void IndicatorFlipsInOneBezierWithoutTheOldSeparateOutwardSlide()
        {
            var from = new Pose(new Vector3(.2f, .8f, .3f), Quaternion.Euler(30, 20, 10));
            var to = new Pose(new Vector3(-.4f, 1, -.1f), Quaternion.Euler(-40, 70, 15));
            var motion = new DealTileMotion("t1", from, to, DealTileMotion.Kind.Flip, 1.15f);
            Assert.That(motion.Duration, Is.EqualTo(1.15f));
            var quarter = motion.Sample(1.15f * .25f);
            Assert.That(Vector3.Distance(quarter.position, new Vector3(.14588692f, .83219762f, .24785951f)), Is.LessThan(.000001f));
            Assert.That(Quaternion.Angle(quarter.rotation, Quaternion.Slerp(from.rotation, to.rotation, .1257232f)), Is.LessThan(.03f));
        }

        [Test]
        public void SettlingTilesDropWithoutAnArcAndLandingHasTwoDecayingHops()
        {
            var from = new Pose(new Vector3(.1f, .9f, .2f), Quaternion.identity);
            var to = new Pose(new Vector3(.1f, .8f, .2f), Quaternion.identity);
            var drop = new DealTileMotion("t1", from, to, DealTileMotion.Kind.Drop);
            Assert.That(drop.Duration, Is.EqualTo(.3f));
            Assert.That(drop.Sample(.075f).position.y, Is.EqualTo(.8421875f).Within(.000001f));
            Assert.That(DealTileMotion.LandingHop(.065f, true), Is.EqualTo(.0018586723f).Within(.00000001f));
            Assert.That(DealTileMotion.LandingHop(.195f, true), Is.EqualTo(.0001657815f).Within(.00000001f));
            Assert.That(DealTileMotion.LandingHop(.26f, true), Is.Zero);
            Assert.That(DealTileMotion.LandingScale(.085f), Is.EqualTo(1.0265165f).Within(.000001f));
            Assert.That(DealTileMotion.LandingScale(.34f), Is.EqualTo(1));
        }

        [Test]
        public void DelayedFreshTilesStayHiddenButAnExistingStackRemainsVisibleUntilLiftOff()
        {
            tile = new GameObject("tas_t1");
            var child = new GameObject("body"); child.transform.SetParent(tile.transform, false);
            var renderer = child.AddComponent<MeshRenderer>();
            var view = tile.AddComponent<TileView>();
            view.MoveTo(new Pose(Vector3.up, Quaternion.identity), TileView.Motion.Deal, .4f);
            Assert.That(renderer.enabled, Is.False);
            var addedFace = new GameObject("face bound during fast fill"); addedFace.transform.SetParent(tile.transform, false);
            var addedRenderer = addedFace.AddComponent<MeshRenderer>();
            view.MoveTo(new Pose(Vector3.left, Quaternion.identity), TileView.Motion.Deal, .024f);
            Assert.That(addedRenderer.enabled, Is.False, "A face bound after skip must inherit the existing hidden state until its rack-flight lead ends.");
            view.Snap(new Pose(Vector3.up, Quaternion.identity));
            view.MoveTo(new Pose(Vector3.right, Quaternion.identity), TileView.Motion.Block, .1f);
            Assert.That(renderer.enabled, Is.True, "The second bundle waits visibly on the dealer's table, rather than disappearing for its stagger.");
        }
    }
}
