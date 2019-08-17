using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace YouTubeDownloadTool
{
    public sealed class YouTubeDLTool
    {
        private readonly string executablePath;

        public YouTubeDLTool(string executablePath)
        {
            if (executablePath is null || !Path.IsPathFullyQualified(executablePath))
                throw new ArgumentException("Executable path must be fully qualified.", nameof(executablePath));

            this.executablePath = executablePath ?? throw new ArgumentNullException(nameof(executablePath));
        }

        public async Task<DownloadResult> DownloadToDirectoryAsync(string url, string destinationDirectory, bool audioOnly = false)
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
                    ArgumentList = { url },
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            if (audioOnly) process.StartInfo.ArgumentList.Add("--extract-audio");

            var output = new List<(bool isError, string line)>();

            process.OutputDataReceived += (sender, e) =>
            {
                if (e.Data is { }) output.Add((isError: false, e.Data));
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data is { }) output.Add((isError: true, e.Data));
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var errorMessage = string.Join(
                    Environment.NewLine,
                    from part in output
                    where part.isError
                    select part.line);

                return DownloadResult.Error(errorMessage, process.ExitCode);
            }

            return DownloadResult.Success;
        }
    }
}
