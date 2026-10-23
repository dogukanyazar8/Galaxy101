using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Galaxy101.Tests
{
    /// <summary>Görüntü modu and çözünürlük rules (spec/19): options per monitor, fallbacks, window fitting, validation.</summary>
    public sealed class GameDisplayTests
    {
        private static Vector2Int V(int w, int h) => new Vector2Int(w, h);

        [Test] public void A1080pMonitorOffersTheSmallerStandardSizesThenItsOwn()
        {
            Assert.That(GameDisplay.Options(V(1920, 1080)), Is.EqualTo(new[] { V(1280, 720), V(1366, 768), V(1600, 900), V(1920, 1080) }));
        }

        [Test] public void ANonStandardNativeSizeIsLastAndSizesThatDoNotFitAreLeftOut()
        {
            var options = GameDisplay.Options(V(3456, 1944));
            Assert.That(options.Last(), Is.EqualTo(V(3456, 1944)));
            Assert.That(options, Does.Contain(V(3200, 1800)));
            Assert.That(options.Any(o => o.x > 3456 || o.y > 1944), Is.False);
            // 16:10 monitor: 1920×1080 fits, 2560×1440 does not (1600 high is fine, 2560 wide is fine) — width and height both checked.
            Assert.That(GameDisplay.Options(V(1920, 1200)).Last(), Is.EqualTo(V(1920, 1200)));
            Assert.That(GameDisplay.Options(V(1920, 1200)), Does.Contain(V(1920, 1080)));
        }

        [Test] public void FullscreenOnlyOffersSizesTheMonitorHasAModeFor()
        {
            var modes = new[] { V(1280, 720), V(1920, 1080), V(2560, 1440), V(2560, 1600), V(3840, 2160) };
            Assert.That(GameDisplay.Options(V(3840, 2160), modes), Is.EqualTo(new[] { V(1280, 720), V(1920, 1080), V(2560, 1440), V(3840, 2160) }));
        }

        [Test] public void NativeOrASizeTheMonitorCannotShowResolvesToNative()
        {
            Assert.That(GameDisplay.Resolve(GameDisplay.Native, V(2560, 1440)), Is.EqualTo(V(2560, 1440)));
            Assert.That(GameDisplay.Resolve("1920x1080", V(2560, 1440)), Is.EqualTo(V(1920, 1080)));
            Assert.That(GameDisplay.Resolve("3840x2160", V(1920, 1080)), Is.EqualTo(V(1920, 1080)), "moved to a smaller monitor");
        }

        [Test] public void AWindowShrinksToTheLargestOptionThatFitsTheWorkArea()
        {
            var work = new RectInt(0, 0, 1920, 1040); // taskbar
            Assert.That(GameDisplay.FitWindow(V(1920, 1080), V(1920, 1080), work), Is.EqualTo(V(1600, 900)));
            Assert.That(GameDisplay.FitWindow(V(1280, 720), V(1920, 1080), work), Is.EqualTo(V(1280, 720)));
        }

        [Test] public void ModesMapToUnityScreenModes()
        {
            Assert.That(GameDisplay.UnityMode(GameDisplay.Mode.Fullscreen), Is.EqualTo(FullScreenMode.ExclusiveFullScreen));
            Assert.That(GameDisplay.UnityMode(GameDisplay.Mode.Borderless), Is.EqualTo(FullScreenMode.FullScreenWindow));
            Assert.That(GameDisplay.UnityMode(GameDisplay.Mode.Windowed), Is.EqualTo(FullScreenMode.Windowed));
        }

        [TestCase("native", true)] [TestCase("1920x1080", true)] [TestCase("1920×1080", false)] [TestCase("100x100", false)] [TestCase("", false)]
        public void OnlyNativeOrWxHAreValid(string value, bool valid) => Assert.That(GameDisplay.IsValidResolution(value), Is.EqualTo(valid));

        [TestCase("fullscreen", true)] [TestCase("borderless", true)] [TestCase("windowed", true)] [TestCase("maximized", false)]
        public void OnlyTheThreeModesAreValid(string value, bool valid) => Assert.That(GameDisplay.IsValidMode(value), Is.EqualTo(valid));

        [Test] public void ApplyRecordsTheRequestWithoutResizingTheEditor()
        {
            GameDisplay.Apply("windowed", "1280x720");
            Assert.That(GameDisplay.LastRequest.Mode, Is.EqualTo(FullScreenMode.Windowed));
            Assert.That(GameDisplay.LastRequest.Width, Is.EqualTo(1280));
            GameDisplay.Apply("fullscreen", GameDisplay.Native);
            Assert.That(GameDisplay.LastRequest.Mode, Is.EqualTo(FullScreenMode.ExclusiveFullScreen));
        }
    }
}
