using System;
using System.IO;

namespace YouTubeDownloadTool
{
    internal sealed class RefCountedFileLock
    {
        private readonly FileStream stream;
        private readonly RefCounter referenceCounter;

        private RefCountedFileLock(FileStream stream)
        {
            this.stream = stream;
            referenceCounter = new RefCounter(stream.Dispose);
        }

        public static RefCountedFileLock? CreateIfExists(string filePath)
        {
            try
            {
                return new RefCountedFileLock(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read));
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException)
            {
                return null;
            }
        }

        public IDisposable Lease() => referenceCounter.Lease();

        public string FilePath => referenceCounter.IsDisposed
            ? throw new ObjectDisposedException(nameof(RefCountedFileLock))
            : stream.Name;
    }
}
