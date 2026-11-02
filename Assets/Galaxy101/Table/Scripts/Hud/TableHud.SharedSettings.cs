using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101.Table
{
    public sealed partial class TableHud
    {
        private VisualElement sharedSettingsLayer;
        private GalaxyShips ships;
        private AuthCursorVisual tableCursor;
        private JObject appliedSharedSettings;

        private void InitializeSharedSettings()
        {
            var shipLayer = Layer("table.ships"); canvas.Insert(1, shipLayer);
            ships = new GameObject("Galaxy table ships").AddComponent<GalaxyShips>(); ships.transform.SetParent(transform, false);
            ships.Initialize(shipLayer); ships.UseTableLayout();
            ships.Configure(model.Planet, (bool?)model.Settings["motion"] ?? true, (string)model.Settings["quality"] ?? "Yüksek");
            sharedSettingsLayer = Layer("table-shared-settings"); canvas.Add(sharedSettingsLayer);
            view.TableSettings = Settings;
            view.BuildOverlay(sharedSettingsLayer);
            tableCursor = new AuthCursorVisual(view.Root, prefix: "game-cursor", planet: () => model.Planet, accent: () => view.C("var(--a1)"));
            sharedSettingsLayer.style.display = DisplayStyle.None;
            model.Changed += ApplySharedSettings;
            view.BackgroundChanged += PreviewSharedBackground;
            ApplySharedSettings();
        }

        /// <summary>The actual table gear opens the shell's common six-tab settings, initially on Masa.</summary>
        public void OpenGameSettings()
        {
            if (Window != null && Window != "shared-settings") CloseWindow();
            Window = "shared-settings"; sharedSettingsLayer.style.display = DisplayStyle.Flex;
            view.OpenPanel("settings", 5);
        }

        private void PreviewSharedBackground(string planet, bool motion, string quality)
        {
            background?.Configure(planet, motion, quality); ships?.Configure(planet, motion, quality);
        }

        private void ApplySharedSettings()
        {
            background?.Configure(model.Planet, (bool?)model.Settings["motion"] ?? true, (string)model.Settings["quality"] ?? "Yüksek");
            ships?.Configure(model.Planet, (bool?)model.Settings["motion"] ?? true, (string)model.Settings["quality"] ?? "Yüksek");
            GameDisplay.Apply((string)model.Settings["wmode"], (string)model.Settings["res"]);
            FramePacing.Apply((int)model.Settings["fps"]);
            Audio.GalaxyAudio.ApplySettings(model.Settings);
            ApplyEffectQuality();
            if (appliedSharedSettings == null) { appliedSharedSettings = (JObject)model.Settings.DeepClone(); return; }
            var changed = new List<string>();
            foreach (var property in model.Settings.Properties())
            {
                if (JToken.DeepEquals(appliedSharedSettings[property.Name], property.Value)) continue;
                changed.Add(property.Name); appliedSharedSettings[property.Name] = property.Value.DeepClone();
            }
            foreach (var key in changed) Settings.NotifySharedSettingsChanged(key);
        }

        /// <summary>
        /// Efekt kalitesi also sets the 3D view's pixel budget, the table's main GPU cost: resolution cap and MSAA. Düşük
        /// also drops the table's shadow pass and halves the tile trails.
        /// </summary>
        private void ApplyEffectQuality()
        {
            string quality = ((string)model.Settings["quality"] ?? "Yüksek").ToLowerInvariant();
            bool low = quality == "düşük" || quality == "low", medium = quality == "orta" || quality == "medium";
            viewMsaa = low ? 1 : medium ? 2 : 4; viewScaleCap = low ? 1 : medium ? 1.5f : 3f;
            table.SetEffectQuality(low ? 0 : medium ? 1 : 2); TileView.TrailScale = low ? .5f : 1;
        }

        private void DestroySharedSettings()
        {
            tableCursor?.Dispose();
            if (model != null) model.Changed -= ApplySharedSettings;
            if (view != null) view.BackgroundChanged -= PreviewSharedBackground;
        }
    }
}
