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
                if (source[i] is null) throw new ArgumentException("Collection contains a null item.", name);
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

        public static void EnumValue<T>(T value, string name) where T : struct, Enum
        {
            if (!Enum.IsDefined(typeof(T), value)) throw new ArgumentOutOfRangeException(name);
        }
    }
}
