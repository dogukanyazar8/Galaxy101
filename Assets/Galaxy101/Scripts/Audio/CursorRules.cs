using System;
using UnityEngine.UIElements;

namespace Galaxy101.Audio
{
    /// <summary>
    /// g101-cursor.js resolve() for UI Toolkit: which cursor an element under the pointer asks for, and whether a press on
    /// it is a ui.tap. The prototype reads data-cursor-lock, data-cursor, text inputs, the inline cursor style,
    /// role="slider", disabled controls and clickable tags; here the same order reads USS classes
    /// (<c>g-cursor-lock-&lt;name&gt;</c>, <c>g-cursor-&lt;name&gt;</c>), text fields, sliders, scrollers, disabled controls
    /// and buttons/toggles/popups.
    /// </summary>
    public static class CursorRules
    {
        /// <summary>data-cursor: the element's own cursor (also stands for the prototype's inline cursor style).</summary>
        public const string ClassPrefix = "g-cursor-";
        /// <summary>data-cursor-lock: every element inside shows this cursor (e.g. wait while busy).</summary>
        public const string LockPrefix = "g-cursor-lock-";
        /// <summary>g101-sound.js: the ui.tap listener looks at the pressed element and up to five parents.</summary>
        public const int TapDepth = 6;

        public const string Default = "default", Pointer = "pointer", Text = "text", No = "no", Ew = "ew", Ns = "ns";

        /// <summary>Gives an element its own cursor (data-cursor); null removes it.</summary>
        public static void Mark(VisualElement element, string name)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            Strip(element, ClassPrefix, LockPrefix);
            if (!string.IsNullOrEmpty(name)) element.AddToClassList(ClassPrefix + name);
        }

        /// <summary>Locks the cursor of an element and everything inside it (data-cursor-lock); null unlocks.</summary>
        public static void Lock(VisualElement element, string name)
        {
            if (element == null) throw new ArgumentNullException(nameof(element));
            Strip(element, LockPrefix, null);
            if (!string.IsNullOrEmpty(name)) element.AddToClassList(LockPrefix + name);
        }

        /// <summary>
        /// The cursor for the element under the pointer: the nearest lock, else the first element (from the target up)
        /// with a rule of its own, else default. <paramref name="known"/> limits locks and marks to real cursor names.
        /// </summary>
        public static string Resolve(VisualElement target, Func<string, bool> known = null)
        {
            if (target == null) return Default;
            for (var e = target; e != null; e = e.parent)
            {
                var locked = Read(e, LockPrefix, null);
                if (locked != null && (known == null || known(locked))) return locked;
            }
            for (var e = target; e != null; e = e.parent)
            {
                var own = Own(e, known);
                if (own != null) return own;
            }
            return Default;
        }

        /// <summary>A press on the target is a ui.tap: it or one of its five nearest parents shows the pointer cursor.</summary>
        public static bool IsTap(VisualElement target)
        {
            int depth = 0;
            for (var e = target; e != null && depth < TapDepth; e = e.parent, depth++)
                if (Own(e, null) == Pointer) return true;
            return false;
        }

        /// <summary>The element's own rule, or null when it has none (the parent decides).</summary>
        public static string Own(VisualElement e, Func<string, bool> known = null)
        {
            // A scroll bar (the prototype's scrollBar()): the Scroller or the slider inside it, by its direction.
            if (e is Scroller scroller) return scroller.direction == SliderDirection.Vertical ? Ns : Ew;
            if (e.parent is Scroller owner && Derives(e, typeof(BaseSlider<>))) return owner.direction == SliderDirection.Vertical ? Ns : Ew;
            var marked = Read(e, ClassPrefix, LockPrefix);
            if (marked != null && (marked == CursorSet.None || known == null || known(marked))) return marked;
            if (IsTextInput(e)) return !e.enabledSelf || ReadOnly(e) ? No : Text;
            if (e is GalaxySlider || Derives(e, typeof(BaseSlider<>)) || e is MinMaxSlider) return Ew;
            bool control = e is Button || e is BaseBoolField || Derives(e, typeof(BasePopupField<,>));
            if (control) return e.enabledSelf ? Pointer : No;
            return null;
        }

        private static bool IsTextInput(VisualElement e) => Derives(e, typeof(TextInputBaseField<>));

        private static bool ReadOnly(VisualElement e)
        {
            var property = e.GetType().GetProperty("isReadOnly");
            return property != null && property.GetValue(e) is bool b && b;
        }

        private static bool Derives(VisualElement e, Type openGeneric)
        {
            for (var t = e.GetType(); t != null && t != typeof(VisualElement); t = t.BaseType)
                if (t.IsGenericType && t.GetGenericTypeDefinition() == openGeneric) return true;
            return false;
        }

        private static string Read(VisualElement e, string prefix, string exclude)
        {
            foreach (var name in e.GetClasses())
                if (name.StartsWith(prefix, StringComparison.Ordinal) && (exclude == null || !name.StartsWith(exclude, StringComparison.Ordinal)))
                    return name.Substring(prefix.Length);
            return null;
        }

        private static void Strip(VisualElement e, string prefix, string keep)
        {
            foreach (var name in new System.Collections.Generic.List<string>(e.GetClasses()))
                if (name.StartsWith(prefix, StringComparison.Ordinal) && (keep == null || !name.StartsWith(keep, StringComparison.Ordinal)))
                    e.RemoveFromClassList(name);
        }
    }
}
