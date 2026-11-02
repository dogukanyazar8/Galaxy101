using System;
using UnityEngine;

namespace Galaxy101
{
    /// <summary>
    /// The canvas renderer evaluates its ring noise in JavaScript double precision. A small shared lookup preserves
    /// those samples and gives the GPU a pixel-footprint filter; shader float sin/fract hashes do not preserve them.
    /// Texture values are premultiplied gamma-space data, not sRGB color textures.
    /// </summary>
    internal static class RingProfileTexture
    {
        private const int Width = 4096;
        private const double Extent = 2.4;
        private static Texture2D shared;
        private static int consumers;

        public static Texture2D Acquire()
        {
            if (shared == null)
            {
                var texture = new Texture2D(Width, 1, TextureFormat.RGBAHalf, true, true)
                {
                    name = "Galaxy101 shared ring profile", hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 1
                };
                try
                {
                    var pixels = new Color[Width];
                    for (int i = 0; i < Width; i++)
                    {
                        Color color = Evaluate((i + .5) * Extent / Width);
                        pixels[i] = new Color(color.r * color.a, color.g * color.a, color.b * color.a, color.a);
                    }
                    texture.SetPixels(pixels); texture.Apply(true, true); shared = texture;
                }
                catch { Destroy(texture); throw; }
            }
            consumers++;
            return shared;
        }

        public static void Release(Texture2D texture)
        {
            if (ReferenceEquals(texture, null)) return;
            if (!ReferenceEquals(texture, shared) || consumers <= 0) throw new InvalidOperationException("The ring profile lease does not belong to this cache.");
            if (--consumers != 0) return;
            var old = shared; shared = null; Destroy(old);
        }

        private static void Destroy(Texture2D texture)
        {
            if (Application.isPlaying) UnityEngine.Object.Destroy(texture); else UnityEngine.Object.DestroyImmediate(texture);
        }

        /// <summary>Active planet-renderer.js ringAt: the C ring is removed; only the soft B-ring entrance remains.</summary>
        public static Color Evaluate(double radius)
        {
            double alpha = 0, red = 0, green = 0, blue = 0;
            if (radius > 1.45 && radius < 1.53)
            {
                alpha = .2 * Smooth(1.45, 1.53, radius); red = 150; green = 140; blue = 124;
            }
            else if (radius >= 1.53 && radius < 1.95)
            {
                double t = (radius - 1.53) / .42, wave = Math.Sin(t * Math.PI);
                alpha = .78 + .18 * wave; red = 214 + 22 * wave; green = 194 + 18 * wave; blue = 152 + 10 * t;
            }
            else if (radius >= 1.95 && radius < 2.03) { alpha = .05; red = 120; green = 112; blue = 100; }
            else if (radius >= 2.03 && radius < 2.27) { alpha = radius > 2.205 && radius < 2.218 ? .05 : .62; red = 196; green = 182; blue = 156; }
            else if (radius >= 2.31 && radius < 2.33) { alpha = .35; red = 210; green = 200; blue = 180; }
            if (alpha <= 0) return Color.clear;
            double grain = .82 + .26 * Noise(radius * 140) + .08 * Noise(radius * 380);
            alpha *= .75 + .35 * Noise(radius * 210 + 7);
            return new Color((float)(red * grain / 255), (float)(green * grain / 255), (float)(blue * grain / 255), (float)Math.Min(1, Math.Max(0, alpha)));
        }

        private static double Hash(double index)
        {
            double value = Math.Sin(index * 127.1 + 311.7) * 43758.5453;
            return value - Math.Floor(value);
        }
        private static double Noise(double value)
        {
            double index = Math.Floor(value), fraction = value - index, smooth = fraction * fraction * (3 - 2 * fraction);
            return Hash(index) + (Hash(index + 1) - Hash(index)) * smooth;
        }
        private static double Smooth(double from, double to, double value)
        {
            double t = Math.Min(1, Math.Max(0, (value - from) / (to - from)));
            return t * t * (3 - 2 * t);
        }
    }
}
