using System;
using System.Threading;
using System.Threading.Tasks;
using Trarizon.Bangumi.CommandPalette.Utilities;

namespace Trarizon.Bangumi.CommandPalette.Helpers;
internal sealed partial class Debouncer<T> : IDisposable
{
    private readonly Timer _timer;
    private readonly Func<T, CancellationToken, Task> _callback;
    private readonly CancellationToken _externalCancellationToken;
    private readonly ResettableCancellationTokenSource _cts = new();
    private readonly Lock _lock = new();

    private T _currentValue = default!;

    public Debouncer(Func<T, CancellationToken, Task> callback, CancellationToken cancellationToken = default)
    {
        _callback = callback;
        _externalCancellationToken = cancellationToken;
        _timer = new Timer(TimerCallback, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void CancelInvoke()
    {
        lock (_lock) {
            _cts.Cancel();
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    public void DelayInvoke(T value, int time_ms)
    {
        lock (_lock) {
            _cts.Reset();
            _currentValue = value;
            _timer.Change(dueTime: time_ms, Timeout.Infinite);
        }
    }

    private void TimerCallback(object? state)
    {
        CancellationToken ct;
        if (_externalCancellationToken == default) {
            ct = _cts.Token;
        }
        else {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(_externalCancellationToken, _cts.Token);
            ct = cts.Token;
        }

        _callback(_currentValue, ct);
    }

    public void Dispose()
    {
        lock (_lock) {
            _timer.Dispose();
            _cts.Dispose();
        }
    }
}
