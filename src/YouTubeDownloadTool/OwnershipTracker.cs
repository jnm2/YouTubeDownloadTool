using System;
using System.Threading;

namespace YouTubeDownloadTool
{
    internal static class OwnershipTracker
    {
        public static OwnershipTracker<T> Create<T>(T instance)
            where T : class, IDisposable
        {
            return new OwnershipTracker<T>(instance);
        }
    }

    internal sealed class OwnershipTracker<T> : IDisposable
        where T : class, IDisposable
    {
        private T? instance;

        public OwnershipTracker(T instance)
        {
            this.instance = instance ?? throw new ArgumentNullException(nameof(instance));
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref instance, null)?.Dispose();
        }

        public T OwnedInstance => Volatile.Read(ref instance) ?? throw CreateException();

        public T ReleaseOwnership()
        {
            return Interlocked.Exchange(ref instance, null) ?? throw CreateException();
        }

        private static Exception CreateException()
        {
            return new InvalidOperationException("The instance is no longer owned by this tracker.");
        }
    }
}
