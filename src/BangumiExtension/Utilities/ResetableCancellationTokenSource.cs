using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Trarizon.Bangumi.CommandPalette.Utilities;
internal sealed partial class ResetableCancellationTokenSource : IDisposable
{
    public CancellationTokenSource? Source { get; private set; } = new();

    public CancellationToken Token => Source?.Token ?? default;

    public void Cancel()
    {
        if (Source is not null) {
            Source.Cancel();
            Source.Dispose();
            Source = null;
        }
    }

    [MemberNotNull(nameof(Source))]
    public void Reset()
    {
        if (Source is not null) {
            Source.Cancel();
            Source.Dispose();
        }
        Source = new();
    }

    public void Dispose()
    {
        Source?.Dispose();
    }
}
