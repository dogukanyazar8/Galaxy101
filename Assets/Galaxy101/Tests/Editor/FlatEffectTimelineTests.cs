using System;
using Galaxy101.Table;
using NUnit.Framework;

namespace Galaxy101.EditorTests
{
    public sealed class FlatEffectTimelineTests
    {
        [TestCase(false, 2.6f, 3.8f)]
        [TestCase(true, 3.6f, 5.2f)]
        public void GiftArrivalUsesTheSourceTimelineAndFiresOnlyOnce(bool rocket, float landing, float duration)
        {
            Assert.That(FlatEffectTimeline.GiftLanding(rocket), Is.EqualTo(landing));
            Assert.That(FlatEffectTimeline.GiftDuration(rocket), Is.EqualTo(duration));
            var time = new FlatEffectTimeline(); int landed = 0;
            time.Schedule(FlatEffectTimeline.GiftLanding(rocket), () => landed++);
            time.Advance(landing - .01f); Assert.That(landed, Is.EqualTo(0));
            time.Advance(.02f); Assert.That(landed, Is.EqualTo(1));
            time.Advance(10); Assert.That(landed, Is.EqualTo(1));
        }

        [Test]
        public void LeavingTheViewCancelsPendingLandingsAndCallbacksScheduledByOtherCallbacks()
        {
            var time = new FlatEffectTimeline(); int called = 0;
            time.Schedule(1, () => { called++; time.Cancel(); });
            time.Schedule(1, () => called++);
            time.Schedule(2.6f, () => called++);
            time.Advance(3); time.Advance(3);
            Assert.That(called, Is.EqualTo(1));
            Assert.That(time.PendingCount, Is.EqualTo(0));
        }

        [Test]
        public void DelayedEffectWorkKeepsItsOrderAndInvalidTimeCannotAdvanceIt()
        {
            var time = new FlatEffectTimeline(); string order = "";
            time.Schedule(.24f, () => order += "C"); time.Schedule(.11f, () => order += "B"); time.Schedule(0, () => order += "A");
            Assert.Throws<ArgumentOutOfRangeException>(() => time.Advance(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => time.Schedule(-1, () => { }));
            time.Advance(.3f); Assert.That(order, Is.EqualTo("ABC"));
            Assert.That(FlatEffectTimeline.DiceDuration, Is.EqualTo(1.5f));
            Assert.That(FlatEffectTimeline.FinishFlightDuration, Is.EqualTo(1.75f));
        }
    }
}
