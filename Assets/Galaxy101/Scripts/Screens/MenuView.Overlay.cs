using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    public sealed partial class MenuView
    {
        private bool overlayOnly;

        /// <summary>Hosts the same menu panels above a live table, without building a second main screen.</summary>
        public void BuildOverlay(VisualElement host)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (mainLayer != null) throw new InvalidOperationException("A MenuView can only have one host.");
            overlayOnly = true;
            mainLayer = Box(); mainLayer.style.display = DisplayStyle.None;
            panelLayer = Box("fill"); panelLayer.name = "shared-modal-layer"; host.Add(panelLayer);
            dialogLayer = Box("fill"); dialogLayer.name = "shared-dialog-layer"; host.Add(dialogLayer);
            toastLayer = Box("fill"); toastLayer.pickingMode = PickingMode.Ignore; host.Add(toastLayer);
            float last = Time.realtimeSinceStartup;
            animationJob = Root.schedule.Execute(() =>
            {
                float now = Time.realtimeSinceStartup, delta = Mathf.Clamp(now - last, 0, .05f); last = now;
                if (!focused) return;
                animationTime = GameClock.Frozen ? (float)(GameClock.CanonMs / 1000) : animationTime + delta;
                foreach (var motion in panelMotions) motion(animationTime);
                foreach (var motion in dialogMotions) motion(animationTime);
            }).Every(0);
            Rebuild();
            AttachFpsCounter(host.parent ?? Root);
        }
    }
}
