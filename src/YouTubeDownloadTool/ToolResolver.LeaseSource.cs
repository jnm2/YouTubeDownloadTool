namespace YouTubeDownloadTool;

partial class ToolResolver
{
    private sealed class LeaseSource
    {
        public LeaseSource(string version, RefCountedFileLock fileLock)
        {
            Version = version;
            FileLock = fileLock;
            Lease = fileLock.Lease();
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
