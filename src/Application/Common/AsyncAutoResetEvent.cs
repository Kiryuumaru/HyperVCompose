using DisposableHelpers.Attributes;
using System.Threading;

namespace Application.Common;

[Disposable]
public partial class AsyncAutoResetEvent
{
    private static readonly Task s_completed = Task.FromResult(true);
    private readonly Queue<(TaskCompletionSource<bool> tcs, CancellationTokenSource cts)> _waits = new();
    private bool _signaled;

    public Task WaitAsync(CancellationToken cancellationToken = default)
    {
        lock (_waits)
        {
            if (_signaled)
            {
                _signaled = false;
                return s_completed;
            }
            else
            {
                var tcs = new TaskCompletionSource<bool>();
                var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _waits.Enqueue((tcs, cts));
                TimeoutSet(cts.Token);
                return tcs.Task;
            }
        }
    }

    public void Set()
    {
        (TaskCompletionSource<bool> tcs, CancellationTokenSource cts)? toRelease = default;

        lock (_waits)
        {
            if (_waits.Count > 0)
            {
                toRelease = _waits.Dequeue();
            }
            else if (!_signaled)
            {
                _signaled = true;
            }
        }

        if (toRelease != null)
        {
            toRelease.Value.tcs.SetResult(true);
            toRelease.Value.cts.Cancel();
        }
    }

    private async void TimeoutSet(CancellationToken cancellationToken)
    {
        await cancellationToken.WhenCanceled();
        Set();
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            lock (_waits)
            {
                while (_waits.TryDequeue(out var toRelease))
                {
                    toRelease.cts.Cancel();
                }
            }
        }
    }
}