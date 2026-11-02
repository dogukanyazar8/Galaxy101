using System;
using System.Collections.Generic;
using Galaxy101.Audio;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    /// <summary>Animated prototype cursor for one UI scope. Input and cursor selection remain native.</summary>
    internal sealed class AuthCursorVisual : IDisposable
    {
        private readonly VisualElement layer, root;
        private readonly Dictionary<string, Image> images = new Dictionary<string, Image>();
        private readonly CursorSet set;
        private readonly IVisualElementScheduledItem ticker;
        private Vector2 panelPosition;
        private bool shown, pressed;
        private string current;
        private float lastSize;
        private int idle;
        private readonly Func<bool> active;
        private readonly Func<string> planet;
        private readonly Func<Color> accent;
        private readonly List<VisualElement> ripples = new List<VisualElement>();
        private string palette;

        public AuthCursorVisual(VisualElement layer, Func<bool> active = null, string prefix = "auth-cursor", Func<string> planet = null, Func<Color> accent = null)
        {
            this.layer = layer;
            this.active = active;
            this.planet = planet;
            this.accent = accent;
            set = CursorSet.Load();
            root = new VisualElement { name = prefix, pickingMode = PickingMode.Ignore };
            root.style.position = Position.Absolute; root.style.width = root.style.height = 0;
            root.style.opacity = 0; root.AddToClassList("auth-cursor-root");
            foreach (var name in set.Names)
            {
                var entry = set.Cursors[name];
                var texture = Resources.Load<Texture2D>(entry.Png2x) ?? Resources.Load<Texture2D>(entry.Png);
                if (texture == null) { Debug.LogWarning("Missing animated cursor texture: " + name); continue; }
                var image = new Image { name = prefix + "-" + name, image = texture, pickingMode = PickingMode.Ignore };
                image.style.position = Position.Absolute; image.AddToClassList("auth-cursor-icon"); image.AddToClassList("auth-cursor-" + name);
                root.Add(image); images.Add(name, image);
            }
            if (!images.ContainsKey(CursorRules.Default) || !images.ContainsKey(CursorRules.Pointer) || !images.ContainsKey(CursorRules.Text))
            {
                Debug.LogWarning("Animated sign-in cursor unavailable; using the hardware cursor.");
                return;
            }
            layer.Add(root); GalaxyCursor.AnimatedLayer = layer;
            layer.RegisterCallback<PointerMoveEvent>(Move, TrickleDown.TrickleDown);
            layer.RegisterCallback<PointerDownEvent>(Down, TrickleDown.TrickleDown);
            layer.RegisterCallback<PointerUpEvent>(Up, TrickleDown.TrickleDown);
            layer.RegisterCallback<PointerCancelEvent>(Cancel, TrickleDown.TrickleDown);
            layer.RegisterCallback<PointerLeaveEvent>(Leave);
            layer.RegisterCallback<FocusInEvent>(Focus, TrickleDown.TrickleDown);
            ticker = layer.schedule.Execute(Tick).Every(0);
        }

        private void Move(PointerMoveEvent evt)
        {
            if (evt.pointerType != UnityEngine.UIElements.PointerType.mouse) return;
            panelPosition = new Vector2(evt.position.x, evt.position.y); shown = true;
            UpdateCursor(evt.target as VisualElement);
        }
        private void Down(PointerDownEvent evt)
        {
            if (evt.pointerType != UnityEngine.UIElements.PointerType.mouse || evt.button != 0) return;
            panelPosition = new Vector2(evt.position.x, evt.position.y); shown = true; pressed = true;
            UpdateCursor(evt.target as VisualElement);
            if (accent != null && (active == null || active()) && GalaxyCursor.Mode != CursorSet.System && (current == CursorRules.Default || current == CursorRules.Pointer)) Ripple();
        }
        private void Up(PointerUpEvent evt)
        {
            if (evt.pointerType != UnityEngine.UIElements.PointerType.mouse || evt.button != 0) return;
            panelPosition = new Vector2(evt.position.x, evt.position.y); pressed = false;
            UpdateCursor(evt.target as VisualElement);
        }
        private void Cancel(PointerCancelEvent evt) { if (evt.pointerType == UnityEngine.UIElements.PointerType.mouse) { pressed = false; UpdateCursor(evt.target as VisualElement); } }
        private void Focus(FocusInEvent evt) => GalaxyCursor.HideHardwareForAnimatedCursor();
        private void Leave(PointerLeaveEvent evt)
        {
            if (evt.pointerType == UnityEngine.UIElements.PointerType.mouse && evt.target == layer) { shown = pressed = false; RefreshVisibility(); }
        }
        private void Tick()
        {
            if (layer.panel == null) return;
            if (active != null && !active()) { shown = pressed = false; RefreshVisibility(); return; }
            RefreshPalette();
            if (!Application.isFocused && !Application.isBatchMode) { shown = pressed = false; root.style.opacity = 0; }
            FollowCapturedDrag();
            if (shown && !pressed && ++idle % 8 == 0) UpdateCursor(layer.panel.Pick(panelPosition));
            else RefreshVisibility();
            if (shown && current == "wait") images[current].style.rotate = new Rotate(Mathf.Repeat(Time.realtimeSinceStartup / 1.1f, 1) * 360);
        }
        /// <summary>
        /// A drag that captures the pointer (a ScrollView's scroller, a slider) keeps its moves to the capturing element, so
        /// the layer's pointer events stop and the drawn cursor would freeze while the content scrolls. During a press or a
        /// capture the cursor follows the mouse device itself, and a release the layer never heard ends the press.
        /// </summary>
        private void FollowCapturedDrag()
        {
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (mouse == null || !shown || layer.panel == null) return;
            bool captured = layer.panel.GetCapturingElement(PointerId.mousePointerId) != null;
            if (pressed && !mouse.leftButton.isPressed && !captured) { pressed = false; foreach (var image in images.Values) image.RemoveFromClassList("auth-cursor-pressed"); }
            if (!pressed && !captured) return;
            var screen = mouse.position.ReadValue();
            var position = RuntimePanelUtils.ScreenToPanel(layer.panel, new Vector2(screen.x, Screen.height - screen.y));
            if ((position - panelPosition).sqrMagnitude < .01f) return;
            panelPosition = position;
            Vector2 local = layer.WorldToLocal(panelPosition); root.style.left = local.x; root.style.top = local.y;
        }
        private void UpdateCursor(VisualElement target)
        {
            if (active != null && !active()) { shown = pressed = false; RefreshVisibility(); return; }
            if (target == null || (target != layer && !layer.Contains(target))) { shown = false; RefreshVisibility(); return; }
            RefreshPalette();
            string name = GalaxyCursor.Held ?? (target == layer ? GalaxyCursor.World : CursorRules.Resolve(target, images.ContainsKey));
            if (pressed && name == "grab") name = "grabbing";
            if (name == CursorSet.None) { shown = false; RefreshVisibility(); return; }
            if (!images.ContainsKey(name)) name = CursorRules.Default;
            if (current != name)
            {
                if (current != null) images[current].RemoveFromClassList("auth-cursor-active");
                current = name; images[current].AddToClassList("auth-cursor-active");
            }
            foreach (var image in images.Values) image.EnableInClassList("auth-cursor-pressed", pressed && image == images[current]);
            Vector2 local = layer.WorldToLocal(panelPosition); root.style.left = local.x; root.style.top = local.y;
            if (root.parent == layer && layer.IndexOf(root) != layer.childCount - 1) root.BringToFront();
            RefreshVisibility();
        }
        private void RefreshPalette()
        {
            if (planet == null) return;
            string next = planet(); if (palette == next) return; palette = next;
            foreach (var pair in images)
            {
                var texture = ThemedCursor.Load(pair.Key, next);
                if (texture != null) pair.Value.image = texture;
            }
        }
        private void Ripple()
        {
            float size = 38 * CursorSet.ModeScale(GalaxyCursor.Mode) + 4;
            Vector2 position = layer.WorldToLocal(panelPosition); Color color = accent();
            var ripple = new VisualElement { name = "cursor-click-ripple", pickingMode = PickingMode.Ignore };
            ripple.style.position = Position.Absolute; ripple.style.left = position.x - size / 2; ripple.style.top = position.y - size / 2;
            ripple.style.width = ripple.style.height = size;
            ripple.style.borderTopLeftRadius = ripple.style.borderTopRightRadius = ripple.style.borderBottomLeftRadius = ripple.style.borderBottomRightRadius = size / 2;
            ripple.style.borderTopWidth = ripple.style.borderRightWidth = ripple.style.borderBottomWidth = ripple.style.borderLeftWidth = 2;
            ripple.style.borderTopColor = ripple.style.borderRightColor = ripple.style.borderBottomColor = ripple.style.borderLeftColor = color;
            GalaxyFilters.Shadow(ripple, 0, 0, 6, color); layer.Add(ripple); ripples.Add(ripple); root.BringToFront();
            float began = Time.realtimeSinceStartup;
            ripple.schedule.Execute(() =>
            {
                float progress = Mathf.Clamp01((Time.realtimeSinceStartup - began) / .45f);
                float ease = MenuView.CssBezier(.2f, .8f, .3f, 1, progress);
                ripple.style.scale = new Scale(Vector2.one * Mathf.Lerp(.25f, 1, ease)); ripple.style.opacity = .9f * (1 - ease);
                if (progress >= 1) { ripple.RemoveFromHierarchy(); ripples.Remove(ripple); }
            }).Every(0);
        }
        private void RefreshVisibility()
        {
            bool visible = shown && GalaxyCursor.Mode != CursorSet.System && (active == null || active());
            if (visible) GalaxyCursor.AnimatedLayer = layer;
            if (GalaxyCursor.AnimatedLayer == layer) GalaxyCursor.AnimatedVisible = visible;
            if (visible) GalaxyCursor.HideHardwareForAnimatedCursor();
            root.style.display = GalaxyCursor.Mode == CursorSet.System ? DisplayStyle.None : DisplayStyle.Flex;
            root.style.opacity = visible ? 1 : 0;
            float pixelScale = Screen.dpi > 0 ? Screen.dpi / 96f : 1;
            float panelScale = layer.scaledPixelsPerPoint;
            float size = 32 * CursorSet.ModeScale(GalaxyCursor.Mode) * pixelScale / (panelScale > 0 ? panelScale : 1);
            if (Mathf.Approximately(lastSize, size)) return;
            lastSize = size;
            foreach (var pair in images)
            {
                Vector2 hotspot = (Vector2)set.Cursors[pair.Key].Hotspot * (size / set.Size);
                var image = pair.Value; image.style.width = image.style.height = size;
                image.style.left = -hotspot.x; image.style.top = -hotspot.y;
                image.style.transformOrigin = new TransformOrigin(hotspot.x, hotspot.y);
            }
        }
        public void Dispose()
        {
            foreach (var ripple in ripples) ripple.RemoveFromHierarchy(); ripples.Clear();
            ticker?.Pause();
            layer.UnregisterCallback<PointerMoveEvent>(Move, TrickleDown.TrickleDown);
            layer.UnregisterCallback<PointerDownEvent>(Down, TrickleDown.TrickleDown);
            layer.UnregisterCallback<PointerUpEvent>(Up, TrickleDown.TrickleDown);
            layer.UnregisterCallback<PointerCancelEvent>(Cancel, TrickleDown.TrickleDown);
            layer.UnregisterCallback<PointerLeaveEvent>(Leave);
            layer.UnregisterCallback<FocusInEvent>(Focus, TrickleDown.TrickleDown);
            if (GalaxyCursor.AnimatedLayer == layer) { GalaxyCursor.AnimatedLayer = null; GalaxyCursor.AnimatedVisible = false; }
            root.RemoveFromHierarchy();
        }
    }
}
