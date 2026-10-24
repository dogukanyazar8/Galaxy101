using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    /// <summary>
    /// Changing a tab or an option redraws the screen: every frame after the change must already be the old or the new
    /// picture — never an interim one (a jumped paragraph, a gradient painted for the wrong size, a replayed entrance),
    /// which reads as the screen reloading.
    /// </summary>
    public sealed class RebuildStabilityTests
    {
        private GameObject owner;
        private string statePath;
        private FrameWatch watch;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            MainMenuController.AuthOnStart = null;
            if (owner != null) UnityEngine.Object.Destroy(owner);
            watch?.Dispose();
            yield return null;
            if (statePath != null && File.Exists(statePath)) File.Delete(statePath);
        }

        [UnityTest]
        public IEnumerator MenuRedrawsHaveNoInterimFrames()
        {
            statePath = Path.Combine(Path.GetTempPath(), "galaxy-stability-" + Guid.NewGuid().ToString("N") + ".json");
            MainMenuController.AuthOnStart = false;
            owner = new GameObject("Menu stability"); owner.SetActive(false);
            var document = owner.AddComponent<UIDocument>(); document.panelSettings = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
            var menu = owner.AddComponent<MainMenuController>(); owner.SetActive(true); menu.Initialize(statePath);
            // The menu windows' card (980 × 610 centred).
            watch = new FrameWatch(document, new RectInt(150, 55, 980, 610));
            var view = menu.View;

            foreach (var (panel, tabs) in new[] { ("settings", 5), ("me", 3), ("leader", 3), ("rules", 0), ("shop", 3), ("friends", 3) })
            {
                view.OpenPanel(panel, 0); yield return new WaitForSecondsRealtime(1f);
                for (int tab = 1; tab < tabs; tab++)
                {
                    int next = tab; var root = view.Root;
                    if (panel == "settings") yield return watch.Stable(() => FrameWatch.Click(root, "settings-tab-" + next), "settings tab " + next);
                    else yield return watch.Stable(() => view.SetTab(panel, next), panel + " tab " + next);
                }
                yield return watch.Stable(() => view.Refresh(), panel + " refresh");
                view.CloseTop(); yield return new WaitForSecondsRealtime(.5f);
            }
        }
    }

    /// <summary>
    /// Renders a UIDocument into a 1280 × 720 target and checks that a change shows no interim frame inside a region.
    /// GALAXY_STABILITY_DUMP=&lt;dir&gt; keeps the old, interim and new frames of a failing change.
    /// </summary>
    internal sealed class FrameWatch : IDisposable
    {
        private readonly RenderTexture target;
        private readonly PanelSettings panel;
        private readonly RectInt region;

        public FrameWatch(UIDocument document, RectInt region)
        {
            this.region = region;
            target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            panel = UnityEngine.Object.Instantiate(document.panelSettings); panel.targetTexture = target; document.panelSettings = panel;
        }

        public void Dispose()
        {
            if (panel != null) UnityEngine.Object.Destroy(panel);
            if (target != null) { target.Release(); UnityEngine.Object.Destroy(target); }
        }

        public static void Click(VisualElement root, string name)
        {
            var e = root.Q<VisualElement>(name); Assert.That(e, Is.Not.Null, "element " + name);
            using (var evt = ClickEvent.GetPooled()) { evt.target = e; e.SendEvent(evt); }
        }

        public IEnumerator Stable(Action change, string what)
        {
            // Pixels that keep moving on their own (shines, timers) before or after the change are not compared.
            var moving = new bool[1280 * 720]; Color32[] before = null;
            yield return Watch(moving, last => before = last);
            change();
            var early = new Color32[5][];
            for (int i = 0; i < early.Length; i++) { yield return null; early[i] = Read(); }
            yield return new WaitForSecondsRealtime(.8f);
            var after = Read();
            yield return Watch(moving, null);
            for (int i = 0; i < early.Length; i++)
            {
                float old = Diff(early[i], before, moving), now = Diff(early[i], after, moving);
                string dump = Environment.GetEnvironmentVariable("GALAXY_STABILITY_DUMP");
                if (Mathf.Min(old, now) >= .01f && !string.IsNullOrEmpty(dump))
                    foreach (var (frame, tag) in new[] { (before, "old"), (early[i], "interim"), (after, "new") }) Save(frame, Path.Combine(dump, what.Replace(' ', '_') + "_" + tag + ".png"));
                Assert.That(Mathf.Min(old, now), Is.LessThan(.01f), what + ": frame " + (i + 1) + " is neither the old nor the new picture (" + old.ToString("P1") + " / " + now.ToString("P1") + " of the region differs)");
            }
        }

        private IEnumerator Watch(bool[] moving, Action<Color32[]> last)
        {
            var previous = Read();
            for (int k = 0; k < 8; k++)
            {
                yield return new WaitForSecondsRealtime(.15f); var later = Read();
                for (int p = 0; p < later.Length; p++) moving[p] |= Changed(later[p], previous[p]);
                previous = later;
            }
            last?.Invoke(previous);
        }

        private Color32[] Read()
        {
            var previous = RenderTexture.active; RenderTexture.active = target;
            var pixels = new Texture2D(1280, 720, TextureFormat.RGBA32, false); pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
            RenderTexture.active = previous; var data = pixels.GetPixels32(); UnityEngine.Object.Destroy(pixels); return data;
        }

        private static void Save(Color32[] frame, string path)
        {
            var pixels = new Texture2D(1280, 720, TextureFormat.RGBA32, false); pixels.SetPixels32(frame); pixels.Apply();
            File.WriteAllBytes(path, pixels.EncodeToPNG()); UnityEngine.Object.Destroy(pixels);
        }

        private static bool Changed(Color32 a, Color32 b) => Mathf.Abs(a.r - b.r) > 24 || Mathf.Abs(a.g - b.g) > 24 || Mathf.Abs(a.b - b.b) > 24;

        /// <summary>Share of the region's still pixels that changed visibly (any channel by more than 24 of 255).</summary>
        private float Diff(Color32[] a, Color32[] b, bool[] moving)
        {
            int changed = 0, total = 0;
            for (int y = 720 - region.yMax; y < 720 - region.y; y++)
                for (int x = region.x; x < region.xMax; x++)
                {
                    int i = y * 1280 + x; if (moving[i]) continue; total++;
                    if (Changed(a[i], b[i])) changed++;
                }
            return (float)changed / Mathf.Max(1, total);
        }
    }
}
