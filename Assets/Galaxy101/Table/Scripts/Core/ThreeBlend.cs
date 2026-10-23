using UnityEngine;

namespace Galaxy101.Table
{
    /// <summary>
    /// three.js tone-maps every fragment and blends in the 8-bit sRGB framebuffer; the table camera blends in linear HDR
    /// and tone-maps once at the end. A translucent white over a dark felt therefore lands about four times brighter in
    /// linear blending (a 30 % glass veils the tea pink, an additive glow turns the felt beige). Remapping the opacity
    /// gives the design's look for light-over-dark, the common case on the table: normal blending a^2.2 (exact over black),
    /// additive a^1.5 (matches a gold glow over the felt).
    /// </summary>
    public static class ThreeBlend
    {
        public static float Normal(float opacity) => Mathf.Pow(Mathf.Clamp01(opacity), 2.2f);
        public static float Additive(float opacity) => Mathf.Pow(Mathf.Clamp01(opacity), 1.5f);
    }
}
