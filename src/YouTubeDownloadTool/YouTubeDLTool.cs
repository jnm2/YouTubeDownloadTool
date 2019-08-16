using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

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
                throw new ArgumentException("Executable path must be fully qualified.", nameof(version));

            Version = version;
            this.executablePath = executablePath ?? throw new ArgumentNullException(nameof(executablePath));
        }

        public async Task DownloadToDirectoryAsync(string url, string destinationDirectory)
        {
            if (string.IsNullOrWhiteSpace(url))
                throw new ArgumentException("URL must be specified.", nameof(url));

            if (destinationDirectory is null || !Path.IsPathFullyQualified(destinationDirectory))
                throw new ArgumentException("The destination directory path must be fully qualified.", nameof(destinationDirectory));

            Directory.CreateDirectory(destinationDirectory);

            using var process = new Process
            {
                StartInfo =
                {
                    UseShellExecute = false,
                    WorkingDirectory = destinationDirectory,
                    FileName = executablePath,
                    CreateNoWindow = true,
                    ArgumentList = { url }
                }
            };

            process.Start();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
                throw new NotImplementedException("youtube-dl.exe exited with code " + process.ExitCode);
        }
    }
}
