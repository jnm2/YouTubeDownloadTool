using System;
using System.IO;

namespace YouTubeDownloadTool
{
    public sealed class YouTubeDLTool
    {
        private readonly string executablePath;

        public string Version { get; }

        public YouTubeDLTool(string version, string executablePath)
        {
            if (string.IsNullOrWhiteSpace(version))
                throw new ArgumentException("Version must be specified.", nameof(version));

            if (executablePath is null || !Path.IsPathFullyQualified(executablePath))
                throw new ArgumentException("Executable path must be fully qualified", nameof(version));

            Version = version;
            this.executablePath = executablePath ?? throw new ArgumentNullException(nameof(executablePath));
        }
    }
}
