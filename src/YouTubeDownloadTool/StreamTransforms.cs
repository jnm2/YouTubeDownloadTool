using System;
using System.IO;
using System.IO.Compression;

namespace YouTubeDownloadTool
{
    public static class StreamTransforms
    {
        public static Func<Stream, Stream> UnzipSingleFile(string zippedPath)
        {
            if (string.IsNullOrWhiteSpace(zippedPath))
                throw new ArgumentException("Zipped path must be specified.", nameof(zippedPath));

            return stream => new ZipArchive(stream).GetEntry(zippedPath)?.Open()
                ?? throw new InvalidOperationException("No entry was found with the following path: " + zippedPath);
        }
    }
}
