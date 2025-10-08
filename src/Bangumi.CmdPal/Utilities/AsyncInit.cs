using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Trarizon.Bangumi.CmdPal.Utilities;
internal sealed class AsyncInit<T>
{
    private readonly Task _initTask;

    public T Value { get; private set; }

    public AsyncInit(T initValue, Func<T, Task<T>> asyncInitialization)
    {
        Value = initValue;
        _initTask = asyncInitialization(initValue).ContinueWith(task => Value = task.Result);
    }

    public AsyncInit<T> OnValueUpdated(Action<T> continuation)
    {
        if (!_initTask.IsCompleted) {
            continuation(Value);
        }
        _initTask.ContinueWith(_ => continuation(Value));
        return this;
    }

    public TaskAwaiter GetAwaiter() => _initTask.GetAwaiter();

    public ConfiguredTaskAwaitable ConfigureAwait(bool continueOnCapturedContext) => _initTask.ConfigureAwait(continueOnCapturedContext);
}
