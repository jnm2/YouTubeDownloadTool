using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace YouTubeDownloadTool;

public sealed class YTDlpTool
{
    private readonly string executablePath;
    private readonly string ffmpegDirectory;
    private readonly string denoPath;

    public YTDlpTool(string executablePath, string ffmpegDirectory, string denoPath)
    {
        if (executablePath is null || !Path.IsPathFullyQualified(executablePath))
            throw new ArgumentException("Executable path must be fully qualified.", nameof(executablePath));

        if (ffmpegDirectory is null || !Path.IsPathFullyQualified(ffmpegDirectory))
            throw new ArgumentException("Ffmpeg directory path must be fully qualified.", nameof(ffmpegDirectory));

        if (!File.Exists(Path.Join(ffmpegDirectory, "ffmpeg.exe")))
            throw new ArgumentException("Ffmpeg.exe does not exist in the specified directory.", nameof(ffmpegDirectory));

        if (denoPath is null || !Path.IsPathFullyQualified(denoPath))
            throw new ArgumentException("Deno path must be fully qualified.", nameof(denoPath));

        this.executablePath = executablePath;
        this.ffmpegDirectory = ffmpegDirectory;
        this.denoPath = denoPath;
    }

    public async Task<DownloadResult> DownloadToDirectoryAsync(
        string url,
        string destinationDirectory,
        bool audioOnly = false,
        CancellationToken cancellationToken = default,
        IProgress<double?>? progress = null,
        IProgress<string?>? status = null)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("URL must be specified.", nameof(url));

        if (destinationDirectory is null || !Path.IsPathFullyQualified(destinationDirectory))
            throw new ArgumentException("The destination directory path must be fully qualified.", nameof(destinationDirectory));

        progress?.Report(null);

        Directory.CreateDirectory(destinationDirectory);

        using var process = new Process
        {
            StartInfo =
            {
                UseShellExecute = false,
                KillOnParentExit = true,
                WorkingDirectory = destinationDirectory,
                FileName = executablePath,
                CreateNoWindow = true,
                ArgumentList = { url, "--ffmpeg-location", ffmpegDirectory, "--js-runtimes", "deno:" + denoPath },
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        if (audioOnly) process.StartInfo.ArgumentList.Add("--extract-audio");

        var output = new List<ProcessOutputLine>();

        var downloadNumber = 0;
        var progressRangeStart = 0.0;
        var progressRangeLength = 0.95;
        var alreadyDownloadedMessage = (string?)null;

        cancellationToken.ThrowIfCancellationRequested();
        process.Start();

        void Cancel()
        {
            var processName = process.ProcessName; // This can't be accessed after Process.Kill

            // Prevent from starting any future child processes
            process.Kill();

            // yt-dlp's self-extracting build (which may still be in use if another instance of this app is running an
            // older installation) starts a child process with the same name (yt-dlp). Once it's done this, killing the
            // process we started doesn't cancel anything.
            Utils.TryKillImmediateChildrenWithProcessName(parentProcess: process, childProcessName: processName);
        }

        await using (cancellationToken.Register(Cancel))
        {
            await foreach (var line in process.ReadAllLinesAsync(CancellationToken.None).ConfigureAwait(false))
            {
                if (line.StandardError)
                {
                    if (progressRangeStart == 0
                        && line.Content.StartsWith("WARNING: Requested formats are incompatible for merge", StringComparison.OrdinalIgnoreCase))
                    {
                        // Audio file will be downloaded separately, so reserve an extra 5% for the second file.
                        progressRangeLength = 0.90;
                    }
                }
                else if (line.Content.StartsWith("[download] ", StringComparison.OrdinalIgnoreCase))
                {
                    var downloadInfo = line.Content["[download] ".Length..];

                    if (Regex.Match(downloadInfo, @"\s*(?<percent>[\d.]+)%") is { Success: true } match)
                    {
                        var downloadPercent = double.Parse(match.Groups["percent"].Value, NumberStyles.Number, CultureInfo.CurrentCulture);
                        progress?.Report(progressRangeStart + (downloadPercent * 0.01 * progressRangeLength));
                    }
                    else
                    {
                        if (downloadInfo.StartsWith("Destination:", StringComparison.OrdinalIgnoreCase))
                        {
                            downloadNumber++;
                            if (downloadNumber > 1)
                            {
                                progressRangeStart += progressRangeLength;
                                progressRangeLength = (1 - progressRangeStart) / 2;
                            }
                        }

                        if (downloadInfo.Contains("has already been downloaded", StringComparison.OrdinalIgnoreCase))
                        {
                            alreadyDownloadedMessage = downloadInfo;
                        }
                    }
                }
                else if (line.Content.StartsWith("[ffmpeg] ", StringComparison.OrdinalIgnoreCase))
                {
                    progress?.Report(null);
                }

                status?.Report(line.Content);
                output.Add(line);
            }

            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (process.ExitCode != 0)
        {
            var errorMessage = string.Join(
                Environment.NewLine,
                from part in output
                where part.StandardError
                select part.Content);

            return DownloadResult.Error(errorMessage, process.ExitCode);
        }

        return DownloadResult.Success(alreadyDownloadedMessage);
    }
}
