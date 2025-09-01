using System;
using System.Threading;

namespace Trarizon.Bangumi.CommandPalette.Utilities;
internal sealed partial class ResetableCancellationTokenSource : IDisposable
{
    private CancellationTokenSource _source = new();
    private readonly Lock _lock = new();

    public CancellationToken Token => _source.Token;

    public void Cancel() => _source.Cancel();

    public void Reset()
    {
        lock (_lock) {
            if (!_source.IsCancellationRequested) {
                _source.Cancel();
            }
            _source.Dispose();
            _source = new();
        }
    }

    public void Dispose()
    {
        _source.Cancel();
        _source.Dispose();
    }
}
