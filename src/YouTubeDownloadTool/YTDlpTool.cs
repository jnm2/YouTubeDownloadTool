using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace YouTubeDownloadTool;

public sealed class YTDlpTool
{
    private readonly string executablePath;
    private readonly string ffmpegDirectory;

    public YTDlpTool(string executablePath, string ffmpegDirectory)
    {
        if (executablePath is null || !Path.IsPathFullyQualified(executablePath))
            throw new ArgumentException("Executable path must be fully qualified.", nameof(executablePath));

        if (ffmpegDirectory is null || !Path.IsPathFullyQualified(ffmpegDirectory))
            throw new ArgumentException("Ffmpeg directory path must be fully qualified.", nameof(ffmpegDirectory));

        if (!File.Exists(Path.Join(ffmpegDirectory, "ffmpeg.exe")))
            throw new ArgumentException("Ffmpeg.exe does not exist in the specified directory.", nameof(ffmpegDirectory));

        this.executablePath = executablePath;
        this.ffmpegDirectory = ffmpegDirectory;
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
                WorkingDirectory = destinationDirectory,
                FileName = executablePath,
                CreateNoWindow = true,
                ArgumentList = { url, "--ffmpeg-location", ffmpegDirectory },
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };

        if (audioOnly) process.StartInfo.ArgumentList.Add("--extract-audio");

        var output = new List<(bool IsError, string Line)>();

        var downloadNumber = 0;
        var progressRangeStart = 0.0;
        var progressRangeLength = 0.95;
        var alreadyDownloadedMessage = (string?)null;

        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data is null) return;

            if (e.Data.StartsWith("[download] ", StringComparison.OrdinalIgnoreCase))
            {
                var downloadInfo = e.Data["[download] ".Length..];

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
            else if (e.Data.StartsWith("[ffmpeg] ", StringComparison.OrdinalIgnoreCase))
            {
                progress?.Report(null);
            }

            status?.Report(e.Data);
            output.Add((IsError: false, e.Data));
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data is null) return;

            if (progressRangeStart == 0
                && e.Data.StartsWith("WARNING: Requested formats are incompatible for merge", StringComparison.OrdinalIgnoreCase))
            {
                // Audio file will be downloaded separately, so reserve an extra 5% for the second file.
                progressRangeLength = 0.90;
            }

            status?.Report(e.Data);
            output.Add((IsError: true, e.Data));
        };

        cancellationToken.ThrowIfCancellationRequested();
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await using (cancellationToken.Register(process.Kill))
            await process.WaitForExitAsync(CancellationToken.None);

        cancellationToken.ThrowIfCancellationRequested();

        if (process.ExitCode != 0)
        {
            var errorMessage = string.Join(
                Environment.NewLine,
                from part in output
                where part.IsError
                select part.Line);

            return DownloadResult.Error(errorMessage, process.ExitCode);
        }

        return DownloadResult.Success(alreadyDownloadedMessage);
    }
}
