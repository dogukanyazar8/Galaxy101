using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Galaxy101.Audio
{
    /// <summary>
    /// The game cursor (g101-cursor.js): the hardware cursor follows the element under the pointer (<see cref="CursorRules"/>),
    /// the 3D table's own choice where no UI is (<see cref="World"/>), and a drag's hold (<see cref="Hold"/>). The cursor
    /// setting "İmleç" (Normal / Büyük / Sistem) is kept like the prototype's localStorage galaxy101.cursor.
    /// </summary>
    public static class GalaxyCursor
    {
        /// <summary>g101-cursor.js: localStorage key of the cursor setting ({"mode": …}).</summary>
        public const string PrefsKey = "galaxy101.cursor";

        private static string mode;
        private static string world = CursorRules.Default;
        private static string held;
        internal static VisualElement AnimatedLayer { get; set; }
        internal static bool AnimatedVisible { get; set; }

        internal static void HideHardwareForAnimatedCursor()
        {
            if (AnimatedVisible && AnimatedLayer?.panel != null && Mode != CursorSet.System &&
                (Application.isFocused || Application.isBatchMode) && UnityEngine.Cursor.visible)
                UnityEngine.Cursor.visible = false;
        }

        /// <summary>The cursor setting: <see cref="CursorSet.Normal"/>, <see cref="CursorSet.Large"/> or <see cref="CursorSet.System"/>.</summary>
        public static string Mode
        {
            get
            {
                if (mode != null) return mode;
                mode = CursorSet.Normal;
                try
                {
                    var stored = PlayerPrefs.GetString(PrefsKey, null);
                    var value = string.IsNullOrEmpty(stored) ? null : (string)JObject.Parse(stored)["mode"];
                    if (IsMode(value)) mode = value;
                }
                catch (Exception ex) when (ex is Newtonsoft.Json.JsonException || ex is InvalidCastException) { }
                return mode;
            }
            set
            {
                if (!IsMode(value)) throw new ArgumentException("Unknown cursor mode " + value, nameof(value));
                mode = value;
                PlayerPrefs.SetString(PrefsKey, new JObject { ["mode"] = value }.ToString(Newtonsoft.Json.Formatting.None));
                Changed?.Invoke();
            }
        }

        /// <summary>The cursor over the 3D table (no UI under the pointer): grab over a movable tile, default elsewhere.</summary>
        public static string World { get => world; set => world = string.IsNullOrEmpty(value) ? CursorRules.Default : value; }

        /// <summary>The cursor a drag holds over everything (grabbing while a tile is dragged, drop over a valid target); null when no drag holds one.</summary>
        public static string Held => held;

        /// <summary>The cursor drawn now (diagnostics and tests): a name, "system" in Sistem mode, null before the first frame.</summary>
        public static string Current { get; internal set; }

        public static event Action Changed;

        /// <summary>The setting's options in the order of Ayarlar → Görüntü → İmleç (G101-Menu cursorRow).</summary>
        public static readonly string[] Modes = { CursorSet.Normal, CursorSet.Large, CursorSet.System };

        public static bool IsMode(string value) => value == CursorSet.Normal || value == CursorSet.Large || value == CursorSet.System;

        public static void Hold(string name) => held = string.IsNullOrEmpty(name) ? null : name;
        public static void Release() => held = null;

        /// <summary>data-cursor: the element's own cursor (pointer for custom clickables, grab, text…); null removes it.</summary>
        public static void Mark(VisualElement element, string name) => CursorRules.Mark(element, name);

        /// <summary>data-cursor-lock: e.g. wait over a busy screen; null unlocks.</summary>
        public static void Lock(VisualElement element, string name) => CursorRules.Lock(element, name);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { mode = null; world = CursorRules.Default; held = null; Current = null; Changed = null; AnimatedLayer = null; AnimatedVisible = false; }
    }

    /// <summary>Draws <see cref="GalaxyCursor"/> each frame with Cursor.SetCursor (run by the audio host).</summary>
    internal sealed class CursorDriver
    {
        /// <summary>g101-cursor.js poll(): without movement the element under the pointer is checked every 8 frames.</summary>
        private const int IdleCheckFrames = 8;
        /// <summary>CSS reference pixel density: 96 DPI is devicePixelRatio 1.</summary>
        private const float ReferenceDpi = 96;

        private readonly CursorSet set;
        private readonly UiPicker picker;
        private readonly Func<string, bool> known;
        private readonly Dictionary<string, (Texture2D texture, Vector2 hotspot)> textures = new Dictionary<string, (Texture2D, Vector2)>();
        private Vector2 lastPosition = new Vector2(float.NaN, float.NaN);
        private string resolved = CursorRules.Default, dragHold, applied;
        private int idle;
        private bool hidden;

        public CursorDriver(CursorSet set, UiPicker picker) { this.set = set; this.picker = picker; known = set.Has; }

        public void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return; // touch only: the prototype keeps the system cursor without a fine pointer
            if (GalaxyCursor.Mode == CursorSet.System) { Apply(null); return; }
            var position = mouse.position.ReadValue();
            bool pressed = mouse.leftButton.isPressed, down = mouse.leftButton.wasPressedThisFrame, up = mouse.leftButton.wasReleasedThisFrame;
            if (position != lastPosition || down || up || (!pressed && ++idle % IdleCheckFrames == 0))
            {
                lastPosition = position;
                var target = picker.Pick(position);
                resolved = target == null ? GalaxyCursor.World : CursorRules.Resolve(target, known);
            }
            // onDown: a press on a resize cursor keeps it until release; onUp lets go.
            if (down && (resolved == CursorRules.Ew || resolved == CursorRules.Ns)) dragHold = resolved;
            if (!pressed) dragHold = null;
            string name = GalaxyCursor.Held ?? dragHold ?? resolved;
            if (pressed && name == "grab") name = "grabbing";
            Apply(name);
        }

        private void Apply(string name)
        {
            if (name == null)
            {
                if (applied == "system") return;
                Show();
                UnityEngine.Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                applied = GalaxyCursor.Current = "system";
                return;
            }
            if (name == CursorSet.None)
            {
                if (!hidden || UnityEngine.Cursor.visible) { UnityEngine.Cursor.visible = false; hidden = true; }
                applied = GalaxyCursor.Current = name;
                return;
            }
            if (!set.Has(name)) name = CursorRules.Default;
            if (GalaxyCursor.AnimatedVisible && GalaxyCursor.AnimatedLayer?.panel != null && (Application.isFocused || Application.isBatchMode))
            {
                if (!hidden || UnityEngine.Cursor.visible) { UnityEngine.Cursor.visible = false; hidden = true; }
                applied = null; GalaxyCursor.Current = name; return;
            }
            float scale = Screen.dpi > 0 ? Screen.dpi / ReferenceDpi : 1;
            int pixels = set.PixelSize(scale, GalaxyCursor.Mode);
            string key = name + "@" + pixels;
            Show();
            if (key == applied) return;
            var (texture, hotspot) = Texture(name, pixels);
            if (texture == null) return;
            UnityEngine.Cursor.SetCursor(texture, hotspot, CursorMode.Auto);
            applied = key; GalaxyCursor.Current = name;
        }

        private void Show()
        {
            if (!hidden && UnityEngine.Cursor.visible) return;
            UnityEngine.Cursor.visible = true; hidden = false;
        }

        private (Texture2D, Vector2) Texture(string name, int pixels)
        {
            string key = name + "@" + pixels;
            if (textures.TryGetValue(key, out var cached)) return cached;
            var pick = set.Pick(name, pixels);
            var source = Resources.Load<Texture2D>(pick.Source);
            if (source == null) { Debug.LogWarning("Missing cursor texture " + pick.Source); textures[key] = (null, Vector2.zero); return (null, Vector2.zero); }
            var texture = pick.Exact ? source : Resample(source, pixels);
            return textures[key] = (texture, pick.Hotspot);
        }

        /// <summary>Bilinear resample with premultiplied alpha (no dark fringe), for the Büyük mode and fractional display scales.</summary>
        internal static Texture2D Resample(Texture2D source, int size)
        {
            var pixels = source.GetPixels32();
            int sw = source.width, sh = source.height;
            var result = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                float v = Mathf.Clamp((y + .5f) * sh / size - .5f, 0, sh - 1);
                int y0 = (int)v, y1 = Mathf.Min(y0 + 1, sh - 1);
                float fy = v - y0;
                for (int x = 0; x < size; x++)
                {
                    float u = Mathf.Clamp((x + .5f) * sw / size - .5f, 0, sw - 1);
                    int x0 = (int)u, x1 = Mathf.Min(x0 + 1, sw - 1);
                    float fx = u - x0;
                    Vector4 a = Premultiplied(pixels[y0 * sw + x0]), b = Premultiplied(pixels[y0 * sw + x1]);
                    Vector4 c = Premultiplied(pixels[y1 * sw + x0]), d = Premultiplied(pixels[y1 * sw + x1]);
                    var p = Vector4.Lerp(Vector4.Lerp(a, b, fx), Vector4.Lerp(c, d, fx), fy);
                    result[y * size + x] = p.w <= 0 ? new Color32(0, 0, 0, 0)
                        : new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(p.x / p.w) * 255), (byte)Mathf.RoundToInt(Mathf.Clamp01(p.y / p.w) * 255),
                            (byte)Mathf.RoundToInt(Mathf.Clamp01(p.z / p.w) * 255), (byte)Mathf.RoundToInt(Mathf.Clamp01(p.w) * 255));
                }
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = source.name + "@" + size, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            texture.SetPixels32(result);
            texture.Apply(false, false);
            return texture;
        }

        private static Vector4 Premultiplied(Color32 c)
        {
            float a = c.a / 255f;
            return new Vector4(c.r / 255f * a, c.g / 255f * a, c.b / 255f * a, a);
        }
    }
}
