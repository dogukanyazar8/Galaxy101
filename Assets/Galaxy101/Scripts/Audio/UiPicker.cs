using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101.Audio
{
    /// <summary>
    /// The prototype's document.elementFromPoint for UI Toolkit: the top pickable element under a screen point across
    /// every UIDocument's panel (highest panel sorting order first), or null over the 3D world. A document's own root
    /// counts as the world: the table HUD covers the screen without catching the pointer.
    /// </summary>
    internal sealed class UiPicker
    {
        /// <summary>Frames between UIDocument scans (documents appear with scenes and at runtime).</summary>
        private const int RescanFrames = 30;

        private readonly List<UIDocument> documents = new List<UIDocument>();
        private readonly HashSet<IPanel> seen = new HashSet<IPanel>();
        private int scannedAt = int.MinValue;

        public void Invalidate() => scannedAt = int.MinValue;

        public VisualElement Pick(Vector2 screenPosition)
        {
            if (Time.frameCount - scannedAt >= RescanFrames || documents.Exists(d => d == null)) Scan();
            VisualElement best = null;
            float bestOrder = float.NegativeInfinity;
            seen.Clear();
            foreach (var document in documents)
            {
                if (document == null || !document.isActiveAndEnabled) continue;
                var root = document.rootVisualElement;
                var panel = root?.panel;
                if (panel == null || !seen.Add(panel)) continue;
                float order = document.panelSettings != null ? document.panelSettings.sortingOrder : 0;
                if (best != null && order <= bestOrder) continue;
                // Screen positions grow upwards, panel positions downwards.
                var local = RuntimePanelUtils.ScreenToPanel(panel, new Vector2(screenPosition.x, Screen.height - screenPosition.y));
                var picked = panel.Pick(local);
                if (picked == null || picked == panel.visualTree || IsDocumentRoot(picked)) continue;
                best = picked; bestOrder = order;
            }
            return best;
        }

        private bool IsDocumentRoot(VisualElement element)
        {
            foreach (var document in documents)
                if (document != null && document.rootVisualElement == element) return true;
            return false;
        }

        private void Scan()
        {
            scannedAt = Time.frameCount;
            documents.Clear();
            documents.AddRange(Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        }
    }
}
