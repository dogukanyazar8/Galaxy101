using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    public sealed partial class MenuView
    {
        private IVisualElementScheduledItem fpsJob;

        private void AttachFpsCounter(VisualElement host)
        {
            var label = Text("FPS –", 12, true, "#8EF0B0"); label.name = "app.fps";
            label.style.unityFont = Font("Montserrat-800"); Tracking(label, .5f, 12);
            label.style.position = Position.Absolute; label.style.left = 8; label.style.top = 64;
            label.style.height = 26; label.style.whiteSpace = WhiteSpace.NoWrap; label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.paddingLeft = label.style.paddingRight = 9; label.style.paddingTop = label.style.paddingBottom = 6;
            label.style.borderTopWidth = label.style.borderRightWidth = label.style.borderBottomWidth = label.style.borderLeftWidth = 1;
            label.style.borderTopColor = label.style.borderRightColor = label.style.borderBottomColor = label.style.borderLeftColor = C("rgba(255,255,255,.12)");
            Decorate(label, "rgba(3,5,14,.72)", 8); GalaxyFilters.Shadow(label, 0, 4, 7, C("rgba(0,0,0,.4)")); host.Add(label);
            double began = Time.realtimeSinceStartupAsDouble; int frames = 0, previousFrame = -1;
            fpsJob?.Pause();
            fpsJob = Root.schedule.Execute(() =>
            {
                bool visible = (bool?)Model.Settings["showFps"] ?? false;
                label.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                double now = Time.realtimeSinceStartupAsDouble;
                if (!visible || !focused) { began = now; frames = 0; return; }
                if (host.IndexOf(label) != host.childCount - 1) label.BringToFront();
                if (previousFrame == Time.frameCount) return;
                previousFrame = Time.frameCount; frames++;
                double elapsed = now - began;
                if (elapsed < .5) return;
                int rate = Mathf.RoundToInt((float)(frames / elapsed));
                label.text = "FPS " + rate + " · " + (elapsed * 1000 / frames).ToString("F1", CultureInfo.InvariantCulture) + " ms";
                label.style.color = C(rate >= 50 ? "#8EF0B0" : rate >= 30 ? "#FFD964" : "#FF8A94");
                began = now; frames = 0;
            }).Every(0);
        }
    }
}
