using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Galaxy101.Audio
{
    /// <summary>One cursor of Galaxy101/Cursors/cursors.json: the @1x and @2x PNG and their hotspots (pixels from the top left).</summary>
    public sealed class CursorImage
    {
        public string Name { get; internal set; }
        public string Png { get; internal set; }
        public string Png2x { get; internal set; }
        public int Size { get; internal set; }
        public Vector2Int Hotspot { get; internal set; }
        public Vector2Int Hotspot2x { get; internal set; }
    }

    /// <summary>Which picture to draw a cursor from at a pixel size, and its hotspot at that size.</summary>
    public readonly struct CursorPick
    {
        public readonly string Source;
        public readonly int SourceSize;
        public readonly int Pixels;
        public readonly Vector2 Hotspot;
        /// <summary>The source already has the wanted size (no resampling).</summary>
        public bool Exact => SourceSize == Pixels;
        public CursorPick(string source, int sourceSize, int pixels, Vector2 hotspot) { Source = source; SourceSize = sourceSize; Pixels = pixels; Hotspot = hotspot; }
    }

    /// <summary>
    /// The 11 game cursors (pkg/assets/cursors.json, g101-cursor.js): default, pointer, grab, grabbing, drop, text, no,
    /// wait, progress, ew, ns. The cursor setting (menu "İmleç") is Normal, Büyük (×1.3) or Sistem (the OS cursor).
    /// </summary>
    public sealed class CursorSet
    {
        public const string ResourcePath = "Galaxy101/Cursors/cursors";
        /// <summary>g101-cursor.js setMode(): the three modes, stored under the prototype's labels.</summary>
        public const string Normal = "Normal", Large = "Büyük", System = "Sistem";
        /// <summary>g101-cursor.js setMode(): Büyük draws every cursor 1.3× (hotspot included).</summary>
        public const float LargeScale = 1.3f;
        /// <summary>data-cursor="none" hides the cursor.</summary>
        public const string None = "none";

        public int Size { get; private set; }
        public IReadOnlyList<string> Names { get; private set; }
        public IReadOnlyDictionary<string, CursorImage> Cursors { get; private set; }

        private static CursorSet shared;

        public static CursorSet Load()
        {
            if (shared != null) return shared;
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null) throw new InvalidOperationException("Missing cursor set: Resources/" + ResourcePath + ".json");
            return shared = Parse(asset.text);
        }

        public static CursorSet Parse(string json)
        {
            var root = JObject.Parse(json);
            var set = new CursorSet { Size = (int?)root["size"] ?? throw new InvalidOperationException("cursors.json: missing size") };
            var cursors = new Dictionary<string, CursorImage>(StringComparer.Ordinal);
            foreach (var property in ((JObject)root["cursors"] ?? throw new InvalidOperationException("cursors.json: missing cursors")).Properties())
            {
                var c = (JObject)property.Value;
                cursors[property.Name] = new CursorImage
                {
                    Name = property.Name,
                    Png = (string)c["png"],
                    Png2x = (string)c["png2x"],
                    Size = (int?)c["size"] ?? set.Size,
                    Hotspot = Point(c["hotspot"]),
                    Hotspot2x = Point(c["hotspot2x"]),
                };
            }
            set.Cursors = cursors;
            set.Names = (root["names"] as JArray)?.Values<string>().ToArray() ?? cursors.Keys.ToArray();
            return set;
        }

        public bool Has(string name) => name != null && Cursors.ContainsKey(name);

        public static float ModeScale(string mode) => mode == Large ? LargeScale : 1;

        /// <summary>
        /// Device pixels of a cursor: the design size (32 CSS px) × the display scale (DPI / 96, the browser's
        /// devicePixelRatio) × the mode scale.
        /// </summary>
        public int PixelSize(float displayScale, string mode) =>
            Math.Max(1, (int)Math.Round(Size * (displayScale > 0 ? displayScale : 1) * ModeScale(mode), MidpointRounding.AwayFromZero));

        /// <summary>
        /// The @1x picture at the design size, the @2x picture at twice it; any other size is resampled from @2x (or @1x
        /// when smaller than the design size), the hotspot scaled with it.
        /// </summary>
        public CursorPick Pick(string name, int pixels)
        {
            if (!Cursors.TryGetValue(name, out var c)) throw new ArgumentException("Unknown cursor " + name, nameof(name));
            if (pixels == c.Size) return new CursorPick(c.Png, c.Size, pixels, c.Hotspot);
            if (pixels == c.Size * 2) return new CursorPick(c.Png2x, c.Size * 2, pixels, c.Hotspot2x);
            float k = pixels / (float)c.Size;
            var hotspot = new Vector2((float)Math.Round(c.Hotspot.x * k, MidpointRounding.AwayFromZero), (float)Math.Round(c.Hotspot.y * k, MidpointRounding.AwayFromZero));
            return pixels < c.Size ? new CursorPick(c.Png, c.Size, pixels, hotspot) : new CursorPick(c.Png2x, c.Size * 2, pixels, hotspot);
        }

        private static Vector2Int Point(JToken token) =>
            token is JArray a && a.Count == 2 ? new Vector2Int((int)a[0], (int)a[1]) : throw new InvalidOperationException("cursors.json: bad hotspot");
    }
}
