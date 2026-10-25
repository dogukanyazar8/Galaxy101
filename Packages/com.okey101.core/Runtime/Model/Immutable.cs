using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Okey101.Core.Model
{
    internal static class Immutable
    {
        public static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source, string name)
        {
            if (source == null) throw new ArgumentNullException(name);
            var result = new T[source.Count];
            for (var i = 0; i < result.Length; i++)
            {
                // Value types (tile ids, placed tiles) cannot be null: skip the check rather than box every item.
                if (!typeof(T).IsValueType && (object)source[i] == null) throw new ArgumentException("Collection contains a null item.", name);
                result[i] = source[i];
            }
            return new ReadOnlyCollection<T>(result);
        }

        public static IReadOnlyList<IReadOnlyList<T>> CopyNested<T>(IReadOnlyList<IReadOnlyList<T>> source, string name)
        {
            if (source == null) throw new ArgumentNullException(name);
            var result = new IReadOnlyList<T>[source.Count];
            for (var i = 0; i < result.Length; i++) result[i] = Copy(source[i], name);
            return new ReadOnlyCollection<IReadOnlyList<T>>(result);
        }

        public static void Player(int value, string name)
        {
            if (value < 0 || value > 3) throw new ArgumentOutOfRangeException(name);
        }

        /// <summary>Defined-value check without boxing (Enum.IsDefined boxes; tiles and groups are made by the million in searches).</summary>
        public static void EnumValue<T>(T value, string name) where T : struct, Enum
        {
            if (!Defined<T>.Values.Contains(value)) throw new ArgumentOutOfRangeException(name);
        }

        private static class Defined<T> where T : struct, Enum
        {
            public static readonly HashSet<T> Values = new HashSet<T>((T[])Enum.GetValues(typeof(T)));
        }
    }
}
