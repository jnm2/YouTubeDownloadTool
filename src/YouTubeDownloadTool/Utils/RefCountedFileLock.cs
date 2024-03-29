using System.IO;

namespace YouTubeDownloadTool;

internal sealed class RefCountedFileLock
{
    private readonly FileStream stream;
    private readonly RefCountingDisposer referenceCounter;

    private RefCountedFileLock(FileStream stream)
    {
        this.stream = stream;
        referenceCounter = new RefCountingDisposer(stream);
    }

    public static RefCountedFileLock? CreateIfExists(string filePath)
    {
        try
        {
            return new RefCountedFileLock(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read));
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    public IDisposable Lease() => referenceCounter.Lease();

    public string FilePath => referenceCounter.IsClosed
        ? throw new ObjectDisposedException(nameof(RefCountedFileLock))
        : stream.Name;
}
