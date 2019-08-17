using System;

namespace YouTubeDownloadTool.YouTubeDL
{
    partial class YouTubeDLToolResolver
    {
        private sealed class LeaseSource
        {
            public LeaseSource(string version, RefCountedFileLock fileLock)
            {
                using var lease = OwnershipTracker.Create(fileLock.Lease());

                Tool = new YouTubeDLTool(version, fileLock.FilePath);
                FileLock = fileLock;
                Lease = lease.ReleaseOwnership();
            }

            public YouTubeDLTool Tool { get; }
            public RefCountedFileLock FileLock { get; }
            public IDisposable Lease { get; }

            public YouTubeDLToolLease CreateLease()
            {
                return new YouTubeDLToolLease(Tool, FileLock.Lease());
            }
        }
    }
}
