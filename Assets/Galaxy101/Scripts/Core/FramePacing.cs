using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Galaxy101
{
    /// <summary>
    /// The one place that decides the frame rate (Ayarlar → Görüntü → Kare hızı).
    /// <list type="bullet">
    /// <item><b>Otomatik</b> (default): desktop syncs to the refresh rate Windows runs the window's monitor at
    /// (<c>vSyncCount = 1</c>; 60, 144, 165 Hz…), mobile asks for the display's own rate through <c>targetFrameRate</c>
    /// (VSync settings are ignored there).</item>
    /// <item>A cap (30 … the monitor's maximum): VSync off, <c>targetFrameRate = cap</c>; mobile never asks for more than
    /// the display shows.</item>
    /// <item><b>Sınırsız</b> (desktop only): VSync off, no cap.</item>
    /// </list>
    /// A watcher follows the window to another monitor or a refresh-rate change and re-applies; a desktop build that loses
    /// focus drops to <see cref="BackgroundCap"/> FPS until it is focused again.
    /// </summary>
    public static class FramePacing
    {
        public const int Auto = 0, Unlimited = -1, MinimumCap = 30, MaximumCap = 1000, BackgroundCap = 30;

        /// <summary>Common refresh rates offered as caps, up to the monitor's maximum.</summary>
        public static readonly int[] StandardRates = { 30, 60, 75, 90, 100, 120, 144, 165, 170, 180, 200, 240, 280, 300, 360, 480, 500 };

        /// <summary>The player's choice: <see cref="Auto"/>, <see cref="Unlimited"/> or a cap in FPS.</summary>
        public static int Choice { get; private set; } = Auto;
        /// <summary>Refresh rate the window's monitor runs at now (Hz).</summary>
        public static double CurrentHz { get; private set; } = 60;
        /// <summary>Highest refresh rate the window's monitor supports (Hz).</summary>
        public static double MaxHz { get; private set; } = 60;
        /// <summary>What is applied right now (for diagnostics and tests).</summary>
        public static int AppliedVSync => QualitySettings.vSyncCount;
        public static int AppliedTarget => Application.targetFrameRate;
        /// <summary>Raised after the monitor or its refresh rate changed (the settings row refreshes its labels).</summary>
        public static event Action DisplayChanged;

        public static bool IsMobile => Application.isMobilePlatform;

        /// <summary>Valid stored values: Auto, Unlimited, or a cap from 30 to 1000 FPS.</summary>
        public static bool IsValidChoice(long value) => value == Auto || value == Unlimited || value >= MinimumCap && value <= MaximumCap;

        /// <summary>
        /// Choices for a monitor of <paramref name="maxHz"/>: Otomatik, the standard rates it can show, its own maximum
        /// when that is not a standard rate (e.g. 143.9 → 144, 157 → 157), and Sınırsız on desktop.
        /// </summary>
        public static IReadOnlyList<int> Options(double maxHz, bool mobile)
        {
            int top = Mathf.Max(MinimumCap, (int)Math.Round(maxHz));
            var list = new List<int> { Auto };
            list.AddRange(StandardRates.Where(r => r <= top));
            if (!list.Contains(top)) list.Add(top);
            list.Sort((a, b) => a == Auto ? -1 : b == Auto ? 1 : a.CompareTo(b));
            if (!mobile) list.Add(Unlimited);
            return list;
        }

        /// <summary>vSyncCount and targetFrameRate for a choice (pure: the rules, testable without a device).</summary>
        public static (int VSync, int Target) Policy(int choice, bool mobile, double displayHz, bool focused = true)
        {
            int display = Mathf.Max(MinimumCap, (int)Math.Round(displayHz));
            if (mobile)
            {
                // VSync is always on with the display on phones; targetFrameRate picks the rate (default would be 30).
                int cap = choice == Auto || choice == Unlimited ? display : Mathf.Min(choice, display);
                return (0, cap);
            }
            if (!focused) return (0, Mathf.Min(BackgroundCap, choice > 0 ? choice : BackgroundCap));
            if (choice == Auto) return (1, -1);
            if (choice == Unlimited) return (0, -1);
            return (0, choice);
        }

        public static void Apply(int choice)
        {
            if (!IsValidChoice(choice)) throw new ArgumentOutOfRangeException(nameof(choice), choice, "Frame rate choice must be Auto, Unlimited or 30–1000.");
            Choice = choice;
            Watcher.Ensure();
            Refresh();
        }

        /// <summary>Label for a choice: "Otomatik · 144 Hz", "120 FPS", "Sınırsız".</summary>
        public static string Label(int choice, string language, double currentHz)
        {
            bool en = language == "en";
            if (choice == Auto) return (en ? "Auto · " : "Otomatik · ") + Math.Round(currentHz) + " Hz";
            if (choice == Unlimited) return en ? "Unlimited" : "Sınırsız";
            return choice + " FPS";
        }

        private static bool focused = true;

        private static void Refresh()
        {
            bool background = !focused && !Application.isBatchMode && !Application.isEditor;
            var (vsync, target) = Policy(Choice, IsMobile, CurrentHz, !background);
            QualitySettings.vSyncCount = vsync; Application.targetFrameRate = target;
        }

        /// <summary>Monitor refresh rates as Unity reports them for the window's current display.</summary>
        private static (double Current, double Max, string Display) ReadDisplay()
        {
            double current = Screen.currentResolution.refreshRateRatio.value;
            string display = "";
            try
            {
                var info = Screen.mainWindowDisplayInfo;
                display = info.name + "/" + info.width + "x" + info.height;
                if (info.refreshRate.value > 1) current = info.refreshRate.value;
            }
            catch (Exception) { /* not every platform reports display info */ }
            if (double.IsNaN(current) || current < 1) current = 60;
            double max = current;
            foreach (var mode in Screen.resolutions) if (mode.refreshRateRatio.value > max && !double.IsNaN(mode.refreshRateRatio.value)) max = mode.refreshRateRatio.value;
            return (current, max, display);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Boot()
        {
            Choice = Auto; focused = true;
            var (current, max, _) = ReadDisplay(); CurrentHz = current; MaxHz = max;
            Watcher.Ensure(); Refresh();
        }

        /// <summary>Follows the window across monitors and refresh-rate changes, and focus on desktop.</summary>
        private sealed class Watcher : MonoBehaviour
        {
            private static Watcher instance;
            private string display;
            private float nextCheck;

            public static void Ensure()
            {
                if (instance != null || !Application.isPlaying) return;
                var go = new GameObject("Galaxy101 frame pacing") { hideFlags = HideFlags.HideInHierarchy };
                DontDestroyOnLoad(go); instance = go.AddComponent<Watcher>();
                instance.display = ReadDisplay().Display;
            }

            private void Update()
            {
                if (Time.realtimeSinceStartup < nextCheck) return;
                nextCheck = Time.realtimeSinceStartup + 0.5f;
                var (current, max, name) = ReadDisplay();
                if (name == display && Math.Abs(current - CurrentHz) < 0.5 && Math.Abs(max - MaxHz) < 0.5) return;
                display = name; CurrentHz = current; MaxHz = max;
                Refresh();
                Debug.Log("GALAXY101_FRAME_PACING display=" + name + " hz=" + current.ToString("0.##") + " max=" + max.ToString("0.##"));
                DisplayChanged?.Invoke();
            }

            private void OnApplicationFocus(bool value) { focused = value; Refresh(); }
        }
    }
}
