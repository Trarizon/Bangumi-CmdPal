using Microsoft.Extensions.Logging;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Trarizon.Library.Functional;
using ZLogger;

namespace Trarizon.Bangumi.CmdPal.Toolkit;

internal static class Extensions
{
    public static void ZLogInitialized<T>(this ILogger<T> logger)
        => logger.ZLogInformation($"Initialized.");

    public static ReadOnlySpan<T>.Enumerator GetEnumerator<T>(this in Optional<T> optional)
    {
        if (optional.HasValue) {
            return new ReadOnlySpan<T>(in optional.GetValueRefOrDefaultRef()!).GetEnumerator();
        }
        else {
            return ReadOnlySpan<T>.Empty.GetEnumerator();
        }
    }

    public static Range OffsetOf<T>(ReadOnlySpan<T> source, ReadOnlySpan<T> slice)
    {
        var start = Unsafe.ByteOffset(ref MemoryMarshal.GetReference(source), ref MemoryMarshal.GetReference(slice)) / Unsafe.SizeOf<T>();
        if (start < 0)
            throw new ArgumentException("Slice should be part of source", nameof(slice));
        var end = start + slice.Length;
        if (end > source.Length)
            throw new ArgumentException("Slice should be part of source", nameof(slice));
        return (int)start..(int)end;
    }
}
