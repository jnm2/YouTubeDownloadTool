using System.IO;

namespace YouTubeDownloadTool;

public sealed class ToolLease : IDisposable
{
    private IDisposable? lease;

    public ToolLease(string version, string filePath, IDisposable lease)
    {
        if (string.IsNullOrWhiteSpace(version))
            throw new ArgumentException("Tool version must be specified.", nameof(version));

        if (filePath is null || !Path.IsPathFullyQualified(filePath))
            throw new ArgumentException("File path must be fully qualified.", nameof(filePath));

        Version = version;
        FilePath = filePath;
        this.lease = lease ?? throw new ArgumentNullException(nameof(lease));
    }

    public string Version { get; }

    public string FilePath => Volatile.Read(ref lease) is null
        ? throw new ObjectDisposedException(nameof(ToolLease))
        : field;

    public void Dispose()
    {
        Interlocked.Exchange(ref lease, null)?.Dispose();
    }
}
