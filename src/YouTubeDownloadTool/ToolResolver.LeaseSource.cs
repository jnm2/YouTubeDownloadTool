using System;

namespace YouTubeDownloadTool
{
    partial class ToolResolver
    {
        private sealed class LeaseSource
        {
            public LeaseSource(string version, RefCountedFileLock fileLock)
            {
                using var lease = OwnershipTracker.Create(fileLock.Lease());

                Version = version;
                FileLock = fileLock;
                Lease = lease.ReleaseOwnership();
            }

            public string Version { get; }
            public RefCountedFileLock FileLock { get; }
            public IDisposable Lease { get; }

            public ToolLease CreateLease()
            {
                return new ToolLease(Version, FileLock.FilePath, FileLock.Lease());
            }
        }
    }
}
