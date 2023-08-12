using System;
using System.Threading;

namespace YouTubeDownloadTool;

internal sealed class RefCounter
{
    private Action? action;
    private int referenceCount;

    public RefCounter(Action action)
    {
        this.action = action ?? throw new ArgumentNullException(nameof(action));
    }

    public bool IsDisposed => Volatile.Read(ref action) is null;

    public IDisposable Lease()
    {
        Increment();
        return new RefCounterLease(this);
    }

    private void Increment()
    {
        if (Interlocked.Increment(ref referenceCount) <= 0 || IsDisposed)
        {
            throw new ObjectDisposedException(nameof(RefCountedFileLock));
        }
    }

    private void Decrement()
    {
        if (Interlocked.Decrement(ref referenceCount) == 0)
        {
            Interlocked.Exchange(ref action, null)?.Invoke();
        }
    }

    private sealed class RefCounterLease : IDisposable
    {
        private RefCounter? refCounter;

        public RefCounterLease(RefCounter refCounter)
        {
            this.refCounter = refCounter;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref refCounter, null)?.Decrement();
        }
    }
}
