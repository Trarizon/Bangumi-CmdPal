using System;
using System.Collections.Generic;
using Trarizon.Library.Functional;

namespace Trarizon.Bangumi.CommandPalette.Utilities;
internal static class MonadExtensions
{
    public static ReadOnlySpan<T>.Enumerator GetEnumerator<T>(this in Optional<T> optional)
    {
        if (optional.HasValue) {
            return new ReadOnlySpan<T>(in optional.GetValueRefOrDefaultRef()!).GetEnumerator();
        }
        else {
            return ReadOnlySpan<T>.Empty.GetEnumerator();
        }
    }

    public static void AddOptional<T>(this List<T> list, Optional<T> optional)
    {
        if (optional.HasValue) {
            list.Add(optional.Value);
        }
    }
}
