using System;
using System.Threading;
using System.Threading.Tasks;

namespace Trarizon.Bangumi.CommandPalette.Utilities;
internal sealed partial class Debouncer<T> : IDisposable
{
    private readonly Timer _timer;
    private readonly ResettableCancellationTokenSource _cts = new();
    private readonly Lock _lock = new();

    private T _currentValue = default!;
    private Func<T, CancellationToken, Task> _callback;
    private CancellationToken _externalCancellationToken;

    public Debouncer()
    {
        _callback = default!;
        _timer = new Timer(TimerCallback, null, Timeout.Infinite, Timeout.Infinite);
    }

    public void CancelInvoke()
    {
        lock (_lock) {
            _cts.Cancel();
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }

    public void DelayInvoke(T value, Func<T, CancellationToken, Task> func, int time_ms, CancellationToken cancellationToken = default)
    {
        lock (_lock) {
            _cts.Reset();
            _externalCancellationToken = cancellationToken;
            _currentValue = value;
            _callback = func;
            _timer.Change(dueTime: time_ms, Timeout.Infinite);
        }
    }

    private void TimerCallback(object? state)
    {
        if (_lock.TryEnter()) {
            CancellationToken ct;
            try {
                if (_externalCancellationToken == default) {
                    ct = _cts.Token;
                }
                else {
                    var cts = CancellationTokenSource.CreateLinkedTokenSource(_externalCancellationToken, _cts.Token);
                    ct = cts.Token;
                }
            }
            finally {
                _lock.Exit();
            }
            _callback(_currentValue, ct);
        }

        // 无法获取锁时，表明当前DelayInvoke已被调用，无需执行
    }

    public void Dispose()
    {
        lock (_lock) {
            _timer.Dispose();
            _cts.Dispose();
        }
    }
}
