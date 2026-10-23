using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Galaxy101
{
    /// <summary>
    /// Ayarlar → Görüntü (spec/19): görüntü modu and çözünürlük, the frame rate's sibling (<see cref="FramePacing"/>).
    /// <list type="bullet">
    /// <item><b>Tam ekran</b> = <see cref="FullScreenMode.ExclusiveFullScreen"/> at the chosen size, at the highest
    /// refresh rate the monitor offers for that size (so the frame rate setting can use it).</item>
    /// <item><b>Çerçevesiz</b> = <see cref="FullScreenMode.FullScreenWindow"/>: covers the monitor, renders at the chosen size.</item>
    /// <item><b>Pencere</b> = <see cref="FullScreenMode.Windowed"/>: a window of the chosen size, shrunk to the largest
    /// option that fits the monitor's work area.</item>
    /// </list>
    /// Resolutions: the standard 16:9 list up to the monitor's native size, then the native size itself (default). The
    /// choice follows the window to another monitor: a size the new monitor cannot show falls back to its native size.
    /// Editor and batch runs only record the request (tests), they never resize the editor.
    /// </summary>
    public static class GameDisplay
    {
        public enum Mode { Fullscreen, Borderless, Windowed }

        public const string Native = "native";
        public static readonly Vector2Int[] StandardSizes =
        {
            new Vector2Int(1280, 720), new Vector2Int(1366, 768), new Vector2Int(1600, 900), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440),
            new Vector2Int(3200, 1800), new Vector2Int(3840, 2160), new Vector2Int(5120, 2880), new Vector2Int(7680, 4320),
        };
        public static readonly string[] ModeKeys = { "fullscreen", "borderless", "windowed" };

        public static Mode CurrentMode { get; private set; } = Mode.Fullscreen;
        /// <summary>"native" or "1920x1080".</summary>
        public static string Resolution { get; private set; } = Native;
        /// <summary>What was last asked of Unity (tests read this in the editor, where nothing is resized).</summary>
        public static (int Width, int Height, FullScreenMode Mode, double Hz) LastRequest { get; private set; }
        /// <summary>Native size of the monitor the window is on.</summary>
        public static Vector2Int NativeSize => ReadNative();

        public static bool CanResize => Application.isPlaying && !Application.isEditor && !Application.isBatchMode && !Application.isMobilePlatform;

        public static bool IsValidMode(string key) => Array.IndexOf(ModeKeys, key) >= 0;
        public static Mode ParseMode(string key) => key == "borderless" ? Mode.Borderless : key == "windowed" ? Mode.Windowed : Mode.Fullscreen;
        public static string Key(Mode mode) => ModeKeys[(int)mode];

        public static bool IsValidResolution(string value) => value == Native || TryParse(value, out _);

        public static bool TryParse(string value, out Vector2Int size)
        {
            size = default;
            if (string.IsNullOrEmpty(value)) return false;
            var parts = value.Split('x');
            if (parts.Length != 2 || !int.TryParse(parts[0], out int w) || !int.TryParse(parts[1], out int h)) return false;
            if (w < 640 || h < 360 || w > 16384 || h > 16384) return false;
            size = new Vector2Int(w, h); return true;
        }

        public static string Format(Vector2Int size) => size.x + "x" + size.y;
        /// <summary>"1920×1080" as the settings row shows it.</summary>
        public static string Label(Vector2Int size) => size.x + "×" + size.y;

        /// <summary>
        /// Standard sizes that fit inside <paramref name="native"/>, then the native size (last, the default). With
        /// <paramref name="modes"/> (Tam ekran: the monitor's own display modes) only sizes the monitor can switch to are
        /// offered — an exclusive 3200×1800 on a monitor without that mode would otherwise land on another size.
        /// </summary>
        public static IReadOnlyList<Vector2Int> Options(Vector2Int native, IEnumerable<Vector2Int> modes = null)
        {
            var available = modes != null ? new HashSet<Vector2Int>(modes) : null;
            var list = StandardSizes.Where(s => s.x <= native.x && s.y <= native.y && s != native && (available == null || available.Contains(s))).ToList();
            list.Add(native);
            return list;
        }

        /// <summary>Options for a görüntü modu on the window's monitor.</summary>
        public static IReadOnlyList<Vector2Int> OptionsFor(string modeKey) => Options(ReadNative(), ParseMode(modeKey) == Mode.Fullscreen ? DisplayModes() : null);

        /// <summary>Sizes the monitor can switch to in exclusive full screen.</summary>
        public static IEnumerable<Vector2Int> DisplayModes() => Screen.resolutions.Select(r => new Vector2Int(r.width, r.height)).Distinct();

        /// <summary>The size a choice means on a monitor: native, or the chosen size if the monitor can show it.</summary>
        public static Vector2Int Resolve(string choice, Vector2Int native)
        {
            if (choice != Native && TryParse(choice, out var size) && size.x <= native.x && size.y <= native.y) return size;
            return native;
        }

        /// <summary>Windowed: the largest option no bigger than the work area (title bar and taskbar included).</summary>
        public static Vector2Int FitWindow(Vector2Int size, Vector2Int native, RectInt workArea)
        {
            if (workArea.width <= 0 || workArea.height <= 0) return size;
            int maxW = workArea.width, maxH = workArea.height - 40; // title bar
            if (size.x <= maxW && size.y <= maxH) return size;
            var fit = Options(native).Where(s => s.x <= maxW && s.y <= maxH).ToList();
            return fit.Count > 0 ? fit[fit.Count - 1] : StandardSizes[0];
        }

        public static FullScreenMode UnityMode(Mode mode) => mode == Mode.Fullscreen ? FullScreenMode.ExclusiveFullScreen : mode == Mode.Borderless ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;

        public static void Apply(string modeKey, string resolution)
        {
            if (!IsValidMode(modeKey)) throw new ArgumentOutOfRangeException(nameof(modeKey), modeKey, "Display mode must be fullscreen, borderless or windowed.");
            if (!IsValidResolution(resolution)) throw new ArgumentOutOfRangeException(nameof(resolution), resolution, "Resolution must be native or WxH.");
            CurrentMode = ParseMode(modeKey); Resolution = resolution;
            Refresh();
        }

        /// <summary>Re-apply after the window moved to another monitor (FramePacing's watcher).</summary>
        internal static void Refresh()
        {
            var native = ReadNative();
            var size = Resolve(Resolution, native);
            var mode = UnityMode(CurrentMode);
            double hz = 0;
            RectInt work = default;
            try { work = Screen.mainWindowDisplayInfo.workArea; } catch (Exception) { }
            if (CurrentMode == Mode.Windowed) size = FitWindow(size, native, work);
            if (CurrentMode == Mode.Fullscreen)
            {
                // A size the monitor has no mode for: its largest mode that fits inside the choice.
                var modes = DisplayModes().ToList();
                if (modes.Count > 0 && !modes.Contains(size))
                {
                    // Prefer the largest offered (16:9) size inside the choice, then any mode that fits.
                    var offered = Options(native, modes).Where(m => m.x <= size.x && m.y <= size.y).ToList();
                    var target = size;
                    size = offered.Count > 0 ? offered[offered.Count - 1] : modes.Where(m => m.x <= target.x && m.y <= target.y).OrderByDescending(m => (long)m.x * m.y).DefaultIfEmpty(native).First();
                }
                // Exclusive mode switches the monitor: take its fastest mode for this size.
                foreach (var r in Screen.resolutions)
                    if (r.width == size.x && r.height == size.y && r.refreshRateRatio.value > hz) hz = r.refreshRateRatio.value;
            }
            LastRequest = (size.x, size.y, mode, hz);
            if (!CanResize) return;
            bool sameHz = hz <= 0 || Math.Abs(Screen.currentResolution.refreshRateRatio.value - hz) < 0.5;
            if (Screen.width == size.x && Screen.height == size.y && Screen.fullScreenMode == mode && sameHz) return;
            if (hz > 0)
            {
                var ratio = Screen.resolutions.Where(r => r.width == size.x && r.height == size.y).OrderByDescending(r => r.refreshRateRatio.value).First().refreshRateRatio;
                Screen.SetResolution(size.x, size.y, mode, ratio);
            }
            else Screen.SetResolution(size.x, size.y, mode);
            Debug.Log("GALAXY101_DISPLAY mode=" + Key(CurrentMode) + " size=" + size.x + "x" + size.y + (hz > 0 ? " hz=" + hz.ToString("0.##") : ""));
        }

        /// <summary>
        /// The monitor's largest mode (its native size). In exclusive full screen the desktop size is the mode we switched
        /// to, so the mode list wins; the display info is the fallback.
        /// </summary>
        private static Vector2Int ReadNative()
        {
            var largest = Screen.resolutions.OrderByDescending(r => (long)r.width * r.height).FirstOrDefault();
            if (largest.width > 0 && largest.height > 0) return new Vector2Int(largest.width, largest.height);
            try
            {
                var info = Screen.mainWindowDisplayInfo;
                if (info.width > 0 && info.height > 0) return new Vector2Int(info.width, info.height);
            }
            catch (Exception) { }
            var current = Screen.currentResolution;
            if (current.width > 0 && current.height > 0) return new Vector2Int(current.width, current.height);
            return new Vector2Int(1920, 1080);
        }
    }
}
