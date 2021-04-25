using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace YouTubeDownloadTool
{
    [DebuggerDisplay("{ToString(),nq}")]
    internal sealed class TempFile : IDisposable
    {
        private string? path;

        public TempFile()
        {
            path = System.IO.Path.GetTempFileName();
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref this.path, null) is { } path)
            {
                File.Delete(path);
            }
        }

        public string Path => path ?? throw new ObjectDisposedException(nameof(TempFile));

        public FileStream OpenStream()
        {
            return new FileStream(Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }

        public override string ToString() => Path;
    }
}
