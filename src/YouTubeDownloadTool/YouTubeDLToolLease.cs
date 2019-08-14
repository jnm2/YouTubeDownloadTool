using System;
using System.Threading;

namespace YouTubeDownloadTool
{
    public sealed class YouTubeDLToolLease : IDisposable
    {
        private readonly YouTubeDLTool tool;
        private IDisposable? fileLockLease;

        public YouTubeDLToolLease(YouTubeDLTool tool, IDisposable fileLockLease)
        {
            this.tool = tool ?? throw new ArgumentNullException(nameof(tool));
            this.fileLockLease = fileLockLease ?? throw new ArgumentNullException(nameof(fileLockLease));
        }

        public YouTubeDLTool Tool => Volatile.Read(ref fileLockLease) is null
            ? throw new ObjectDisposedException(nameof(YouTubeDLToolLease))
            : tool;

        public void Dispose()
        {
            Interlocked.Exchange(ref fileLockLease, null)?.Dispose();
        }
    }
}
