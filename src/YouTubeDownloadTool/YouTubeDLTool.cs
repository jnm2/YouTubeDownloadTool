using System;

namespace YouTubeDownloadTool
{
    public sealed class YouTubeDLTool
    {
        private readonly string executablePath;

        public YouTubeDLTool(string executablePath)
        {
            this.executablePath = executablePath ?? throw new ArgumentNullException(nameof(executablePath));
        }
    }
}
