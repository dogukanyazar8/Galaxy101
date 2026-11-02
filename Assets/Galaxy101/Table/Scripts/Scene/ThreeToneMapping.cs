using UnityEngine;

namespace Galaxy101.Table
{
    /// <summary>
    /// Marks a camera for <see cref="ThreeToneMappingFeature"/>: its HDR colour is tone-mapped with three.js
    /// ACESFilmic at <see cref="Exposure"/> instead of URP's tone mapping.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThreeToneMapping : MonoBehaviour
    {
        /// <summary>table3d.js: renderer.toneMappingExposure = 1.05.</summary>
        public const float DesignExposure = 1.05f;
        public float Exposure = DesignExposure;
        /// <summary>Enabled only while finish sprites are live; their source blends in an encoded sRGB framebuffer.</summary>
        public bool EncodedFinishBlending { get; set; }
    }
}
