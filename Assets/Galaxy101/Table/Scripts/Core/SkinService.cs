using System.Collections.Generic;
using UnityEngine;

namespace Galaxy101.Table
{
    /// <summary>
    /// Applies planet colours to imported glTF materials by material name (spec/09 §8, spec/16 D6), matching the
    /// prototype's applyLook() and rackKit.theme(). Imported materials are shared assets, so every distinct source
    /// material gets one runtime clone per SkinService and all renderers under the managed roots are pointed at it.
    /// </summary>
    public sealed class SkinService
    {
        // Prefab materials are URP Lit / Complex Lit / Unlit (converted from glTF at build time).
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int Emissive = Shader.PropertyToID("_EmissionColor");
        private readonly Dictionary<Material, Material> clones = new Dictionary<Material, Material>();
        private readonly Dictionary<Material, Color> baseEmission = new Dictionary<Material, Color>();
        private TableTheme theme;
        private string tableSkin = "station";

        public TableTheme Theme => theme;

        public void SetTheme(TableTheme value) { theme = value; RecolorAll(); }

        /// <summary>v4 materials.json tableSkinApply: Andromeda has fixed holo/halo hues; other skins use planet hues.</summary>
        public void SetTableSkin(string value) { tableSkin = value; RecolorAll(); }

        private void RecolorAll()
        {
            foreach (var pair in clones) Recolor(pair.Key, pair.Value);
        }

        /// <summary>Takes ownership of every material used under <paramref name="root"/> and recolours it.</summary>
        public void Adopt(GameObject root)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var shared = renderer.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                {
                    var source = shared[i];
                    if (source == null) continue;
                    if (!clones.TryGetValue(source, out var clone))
                    {
                        clone = new Material(source) { name = source.name };
                        clones.Add(source, clone);
                        if (clone.HasProperty(Emissive)) baseEmission[clone] = clone.GetColor(Emissive);
                        Recolor(source, clone);
                    }
                    shared[i] = clone;
                }
                renderer.sharedMaterials = shared;
            }
        }

        public void Release()
        {
            foreach (var clone in clones.Values) { if (Application.isPlaying) Object.Destroy(clone); else Object.DestroyImmediate(clone); }
            clones.Clear(); baseEmission.Clear();
        }

        private void Recolor(Material source, Material m)
        {
            if (theme == null) return;
            switch (m.name)
            {
                // Emissive accents: keep each model's authored strength, swap the hue.
                case "led": case "istaka_led": case "istaka_karbon_dikis": case "istaka_neon_tup1": case "masa_holo_led":
                    SetEmission(m, theme.A1); break;
                case "istaka_neon_tup2":
                    SetEmission(m, theme.A2); break;
                case "duvar":
                    SetEmission(m, theme.A1); break;
                case "masa_cam_kenar":
                    SetEmission(m, theme.A1); break;
                // Unlit accent lines take the accent as their colour.
                case "led_cizgi": case "masa_holo_cizgi":
                    SetBase(m, theme.A1, keepAlpha: true); break;
                case "holo":
                    SetBase(m, tableSkin == "andromeda" ? new Color32(0xB7, 0x9C, 0xFF, 255) : theme.A1, keepAlpha: true); break;
                case "hale":
                    SetBase(m, tableSkin == "andromeda" ? new Color32(0x7F, 0x9C, 0xFF, 255) : theme.A2, keepAlpha: true); break;
                case "masa_kenar":
                    SetBase(m, theme.Rim); break;
                case "deri":
                    SetBase(m, theme.Leather); break;
                case "istaka_eloksal":
                    SetBase(m, theme.Rim * 0.7f); break;
                case "tas_classic":
                    SetBase(m, theme.Tile); break;
                case "cuha":
                    MatchThreeRoughSpecular(m); ApplyFeltEmission(m); break;
            }
        }

        private void ApplyFeltEmission(Material material)
        {
            if (!material.HasProperty(Emissive)) return;
            // table3d.js applyLook(): map-based emission, holo accent × .35 / Andromeda white × .5 / opaque zero.
            bool hologram = tableSkin == "holo", andromeda = tableSkin == "andromeda";
            material.SetColor(Emissive, hologram ? theme.A1.linear * .35f : andromeda ? Color.white * .5f : Color.black);
            material.SetTexture("_EmissionMap", hologram || andromeda ? material.GetTexture("_BaseMap") : null);
            if (hologram || andromeda) material.EnableKeyword("_EMISSION"); else material.DisableKeyword("_EMISSION");
        }

        /// <summary>
        /// The felt (roughness 0.96): three.js's GGX (height-correlated Smith) gives a very rough dielectric about 1.75×
        /// the specular of URP's minimalist Cook-Torrance, a near-white sheen measurable under the spot and key light.
        /// Specular setup with F0 0.07 (0.04 × 1.75) and the map's constant smoothness restores it.
        /// </summary>
        private static void MatchThreeRoughSpecular(Material m)
        {
            if (!m.HasProperty("_WorkflowMode")) return;
            m.SetFloat("_WorkflowMode", 0); m.EnableKeyword("_SPECULAR_SETUP");
            m.DisableKeyword("_METALLICSPECGLOSSMAP"); m.SetTexture("_MetallicGlossMap", null);
            m.SetColor("_SpecColor", new Color(0.07f, 0.07f, 0.07f).gamma); m.SetFloat("_Smoothness", 0.04f);
        }

        private static void SetBase(Material m, Color c, bool keepAlpha = false)
        {
            if (!m.HasProperty(BaseColor)) return;
            var linear = c.linear;
            if (keepAlpha) linear.a = m.GetColor(BaseColor).a; else linear.a = 1;
            m.SetColor(BaseColor, linear);
        }

        private void SetEmission(Material m, Color c)
        {
            if (!m.HasProperty(Emissive)) return;
            var authored = baseEmission.TryGetValue(m, out var e) ? e : m.GetColor(Emissive);
            float strength = Mathf.Max(authored.r, Mathf.Max(authored.g, authored.b));
            var hue = c.linear; float peak = Mathf.Max(hue.r, Mathf.Max(hue.g, hue.b), 1e-4f);
            m.SetColor(Emissive, new Color(hue.r / peak * strength, hue.g / peak * strength, hue.b / peak * strength, 1));
        }
    }
}
