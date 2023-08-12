namespace YouTubeDownloadTool;

/// <summary>
/// Tracks how many references there are to a disposable object, and disposes it when there are none remaining.
/// </summary>
public sealed class RefCountingDisposer
{
    private readonly IDisposable disposable;
    private uint refCount = 1;
    private readonly object lockObject = new();

    /// <summary>
    /// Begins tracking an initial reference to <paramref name="disposable"/>. The reference count starts as <c>1</c>.
    /// If the next call is <see cref="Release"/>, the reference count will go to <c>0</c> and <paramref
    /// name="disposable"/> will be disposed. An additional <see cref="Release"/> call will be needed for each <see
    /// cref="AddRef"/> call, if any.
    /// </summary>
    /// <returns>
    /// A <see cref="RefCountingDisposer"/> which can track further references and which will dispose <paramref
    /// name="disposable"/> when the last reference has been released.
    /// </returns>
    public RefCountingDisposer(IDisposable disposable)
    {
        this.disposable = disposable ?? throw new ArgumentNullException(nameof(disposable));
    }

    /// <summary>
    /// <para>
    /// Reflects that an additional reference to the tracked object has been made. This will require an additional call
    /// to <see cref="Release"/> before the tracked object will be disposed by this <see cref="RefCountingDisposer"/>
    /// instance.
    /// </para>
    /// <para>
    /// <see cref="InvalidOperationException"/> is thrown if all references have already been released and there is no
    /// longer anything to track.
    /// </para>
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if all references have already been released and there is no longer anything to track.
    /// </exception>
    public void AddRef()
    {
        lock (lockObject)
        {
            if (refCount == 0)
                throw new InvalidOperationException($"{nameof(AddRef)} must not be called after all references have been released.");

            refCount++;
        }
    }

    /// <summary>
    /// Reflects that a reference to the tracked object has been released. If the last remaining reference is released,
    /// the tracked object will be disposed and future calls to <see cref="AddRef()"/> and <see cref="Release"/> will
    /// throw <see cref="InvalidOperationException"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if all references have already been released and there is no longer anything to track.
    /// </exception>
    public void Release()
    {
        bool dispose;

        lock (lockObject)
        {
            if (refCount == 0)
                throw new InvalidOperationException($"{nameof(Release)} must not be called after all references have been released.");

            refCount--;

            dispose = refCount == 0;
        }

        if (dispose) disposable.Dispose();
    }

    /// <summary>
    /// <para>
    /// Indicates whether all references have been released. When <see langword="true"/>, the tracked object is either
    /// disposed already or in the process of being disposed on a different thread.
    /// </para>
    /// <para>
    /// ⚠️ Subsequent calls to <see cref="AddRef"/> and <see cref="Release"/> may still throw even if this property
    /// returns <see langword="false"/>. Another thread may have executed the final <see cref="Release"/> call in the
    /// meantime.
    /// </para>
    /// </summary>
    public bool IsClosed => Volatile.Read(ref refCount) == 0;

    /// <summary>
    /// <para>
    /// Reflects that an additional reference to the tracked object has been made and returns a lease object which
    /// releases that reference when disposed. <see cref="IDisposable.Dispose"/> is idempotent and thread-safe on the
    /// returned object.
    /// </para>
    /// <para>
    /// <see cref="InvalidOperationException"/> is thrown if all references have already been released and there is no
    /// longer anything to track. If unbalanced calls to <see cref="Release"/> are made separately, <see
    /// cref="IDisposable.Dispose"/> may also throw <see cref="InvalidOperationException"/> due to all references
    /// already having been released.
    /// </para>
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown if all references have already been released and there is no longer anything to track.
    /// </exception>
    public IDisposable Lease()
    {
        AddRef();
        return new RefLease(this);
    }

    private sealed class RefLease : IDisposable
    {
        private RefCountingDisposer? disposer;

        public RefLease(RefCountingDisposer disposer)
        {
            this.disposer = disposer;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref disposer, null)?.Release();
        }
    }
}
