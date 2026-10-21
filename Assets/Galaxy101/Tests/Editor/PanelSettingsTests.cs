using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    public sealed class PanelSettingsTests
    {
        /// <summary>
        /// Regression (Release black screen on the bot table): every UIDocument binds the authored PanelSettings for a
        /// moment on scene load before MenuGammaPresenter moves it to an owned copy. The authored panel then stays on
        /// screen, empty; if it clears, it wipes the presented frame every frame. Only the owned copy may clear.
        /// </summary>
        [Test]
        public void AuthoredScreenPanelNeverClearsTheFrame()
        {
            var panel = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.targetTexture, Is.Null, "the authored panel is a screen panel");
            Assert.That(panel.clearColor, Is.False);
        }
    }
}
