using System;
using System.IO;
using System.Threading;

namespace YouTubeDownloadTool
{
    public sealed class ToolLease : IDisposable
    {
        private readonly string filePath;
        private IDisposable? lease;

        public ToolLease(string version, string filePath, IDisposable lease)
        {
            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentException("Tool version must be specified.", nameof(version));

            if (filePath is null || !Path.IsPathFullyQualified(filePath))
                throw new ArgumentException("File path must be fully qualified.", nameof(filePath));

            Version = version;
            this.filePath = filePath;
            this.lease = lease ?? throw new ArgumentNullException(nameof(lease));
        }

        public string Version { get; }

        public string FilePath => Volatile.Read(ref lease) is null
            ? throw new ObjectDisposedException(nameof(ToolLease))
            : filePath;

        public void Dispose()
        {
            Interlocked.Exchange(ref lease, null)?.Dispose();
        }
    }
}
