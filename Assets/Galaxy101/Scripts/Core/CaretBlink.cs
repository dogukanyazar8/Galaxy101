using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    /// <summary>
    /// Every text input's caret as the browser draws it (caret-color: auto): the field's text colour, blinking at the
    /// system caret rate while focused. UI Toolkit rewrites the caret and selection colours from the theme's
    /// --unity-cursor-color/--unity-selection-color on every style resolve (e.g. the :focus switch), which showed the
    /// theme's dark caret for the first blink; the colours are put back after each resolve.
    /// </summary>
    internal static class CaretBlink
    {
#if (UNITY_STANDALONE_WIN && !UNITY_EDITOR) || UNITY_EDITOR_WIN
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetCaretBlinkTime();
#endif
        private static float Period()
        {
#if (UNITY_STANDALONE_WIN && !UNITY_EDITOR) || UNITY_EDITOR_WIN
            uint milliseconds = GetCaretBlinkTime();
            if (milliseconds == uint.MaxValue) return float.PositiveInfinity;
            if (milliseconds > 0) return milliseconds / 1000f;
#endif
            return .53f;
        }
        public static void Attach(TextField field, Color color, Color selection)
        {
            float period = Period(), started = 0;
            bool focused = false, visible = true;
            IVisualElementScheduledItem timer = null;
            void Apply()
            {
                field.textSelection.selectionColor = selection;
                field.textSelection.cursorColor = visible ? color : Color.clear;
            }
            void Show()
            {
                started = Time.realtimeSinceStartup;
                visible = true; Apply();
            }
            // Registered after the field's own handlers, so these run last and win over the theme's colours.
            field.RegisterCallback<CustomStyleResolvedEvent>(_ => Apply());
            field.Q(className: TextField.inputUssClassName)?.RegisterCallback<CustomStyleResolvedEvent>(_ => Apply());
            field.RegisterCallback<FocusInEvent>(_ =>
            {
                focused = true; Show();
                if (timer == null) timer = field.schedule.Execute(() =>
                {
                    if (!focused) return;
                    bool next = float.IsPositiveInfinity(period) || ((int)((Time.realtimeSinceStartup - started) / period) & 1) == 0;
                    if (next == visible) return;
                    visible = next; Apply();
                }).Every(50);
                else timer.Resume();
            });
            field.RegisterCallback<FocusOutEvent>(_ => { focused = false; timer?.Pause(); Show(); });
            field.RegisterCallback<KeyDownEvent>(_ => { if (focused) Show(); }, TrickleDown.TrickleDown);
            field.RegisterCallback<PointerDownEvent>(_ => { if (focused) Show(); }, TrickleDown.TrickleDown);
            field.RegisterValueChangedCallback(_ => { if (focused) Show(); });
            Apply();
        }
    }
}
