using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Galaxy101
{
    /// <summary>
    /// Settings → Görüntü → İmleç (G101-Menu cursorRow, g101-cursor.js): Normal draws the Galaxy 101 cursor, Sistem
    /// the operating system's. The choice is kept per device under the prototype's key ("galaxy101.cursor", {"mode": …}).
    /// The art is the package's default arrow (Res/Cursors, lane D); see UNITY_FARKLARI for what a hardware cursor
    /// cannot follow (theme tint, the 1.3× Büyük size, per-element cursors).
    /// </summary>
    public static class GalaxyCursor
    {
        public static readonly string[] Modes = { "Normal", "Büyük", "Sistem" };
        private const string Key = "galaxy101.cursor";

        public static string Mode
        {
            get
            {
                try { var mode = (string)JObject.Parse(PlayerPrefs.GetString(Key, "{}"))["mode"]; return Array.IndexOf(Modes, mode) >= 0 ? mode : Modes[0]; }
                catch (Exception) { return Modes[0]; }
            }
            set
            {
                if (Array.IndexOf(Modes, value) < 0) throw new ArgumentException("Unknown cursor mode: " + value, nameof(value));
                PlayerPrefs.SetString(Key, new JObject { ["mode"] = value }.ToString(Newtonsoft.Json.Formatting.None)); PlayerPrefs.Save(); Apply();
            }
        }

        /// <summary>Sets the hardware cursor for the saved mode (Sistem: the system arrow).</summary>
        public static void Apply()
        {
            if (Application.isBatchMode) return;
            if (Mode == "Sistem") { Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto); return; }
            var spec = Resources.Load<TextAsset>("Galaxy101/Cursors/cursors");
            if (spec == null) { Debug.LogWarning("Missing cursor table Galaxy101/Cursors/cursors"); return; }
            var arrow = JObject.Parse(spec.text)["cursors"]?["default"];
            var texture = arrow == null ? null : Resources.Load<Texture2D>((string)arrow["png"]);
            if (texture == null) { Debug.LogWarning("Missing default cursor art"); return; }
            Cursor.SetCursor(texture, new Vector2((float)arrow["hotspot"][0], (float)arrow["hotspot"][1]), CursorMode.Auto);
        }
    }
}
