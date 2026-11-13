using System.IO;
using System.Net;
using System.Net.Http;

namespace YouTubeDownloadTool;

public sealed class DownloadDataAccess : IDownloadDataAccess
{
    private readonly ToolResolver ytDlpResolver;
    private readonly ToolResolver ffmpegResolver;

    public DownloadDataAccess(string toolCachePath, string userAgent)
    {
        ytDlpResolver = new ToolResolver(
            cacheDirectory: Path.Join(toolCachePath, "yt-dlp"),
            executableFileName: "yt-dlp.exe",
            DownloadResolvers.GitHubReleaseAsset(
                owner: "yt-dlp",
                repo: "yt-dlp",
                // Unlike yt-dlp.exe, which is a self-extracting archive of this same content, this does not need to
                // unpack itself to a temporary directory each time it starts.
                assetName: "yt-dlp_win.zip",
                userAgent,
                ToolInstallers.Unzip()),
            // Self-extracting yt-dlp.exe installations from older versions of this app have no _internal directory.
            // (Older versions of this app can still use the newer installations.)
            isObsoleteInstallation: directory => !Directory.Exists(Path.Join(directory, "_internal")));

        ffmpegResolver = new ToolResolver(
            cacheDirectory: Path.Join(toolCachePath, "ffmpeg"),
            executableFileName: "ffmpeg.exe",
            getLatestDownloadAsync: async cancellationToken =>
            {
                using var client = OwnershipTracker.Create(
                    new HttpClient(new HttpClientHandler {AutomaticDecompression = DecompressionMethods.All}));

                var version = await client.OwnedInstance.GetStringAsync("https://www.gyan.dev/ffmpeg/builds/release-version", cancellationToken).ConfigureAwait(false);

                return new AvailableToolDownload(
                    version,
                    client.ReleaseOwnership(),
                    downloadUrl: "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip",
                    ToolInstallers.UnzipSingleFile($"ffmpeg-{version}-essentials_build/bin/ffmpeg.exe", "ffmpeg.exe"));
            });
    }

    public void Dispose()
    {
        ytDlpResolver.Dispose();
        ffmpegResolver.Dispose();
    }

    public async Task CheckForToolUpdatesAsync(CancellationToken cancellationToken)
    {
        ytDlpResolver.PurgeOldVersions();
        ffmpegResolver.PurgeOldVersions();

        await Task.WhenAll(
            ytDlpResolver.CheckForUpdatesAsync(cancellationToken),
            ffmpegResolver.CheckForUpdatesAsync(cancellationToken)).ConfigureAwait(false);
    }

    public async Task<DownloadResult> DownloadAsync(
        string url,
        string destinationDirectory,
        bool audioOnly,
        CancellationToken cancellationToken,
        IProgress<double?>? progress,
        IProgress<string?>? status)
    {
        var ffmpegResolution = ffmpegResolver.LeaseToolAsync(cancellationToken);
        var ytDlpResolution = ytDlpResolver.LeaseToolAsync(cancellationToken);

        var downloadingTools = (ffmpegResolution.IsCompleted, ytDlpResolution.IsCompleted) switch
        {
            (false, false) => "ffmpeg and yt-dlp",
            (false, _) => "ffmpeg",
            (_, false) => "yt-dlp",
            _ => null,
        };

        if (downloadingTools is not null)
            status?.Report($"Downloading {downloadingTools}...");

        using var ffmpegLease = await ffmpegResolution;
        using var ytDlpLease = await ytDlpResolution;

        status?.Report(null);

        var ytDlp = new YTDlpTool(
            ytDlpLease.FilePath,
            ffmpegDirectory: Path.GetDirectoryName(ffmpegLease.FilePath)!);

        return await ytDlp.DownloadToDirectoryAsync(url, destinationDirectory, audioOnly, cancellationToken, progress, status).ConfigureAwait(false);
    }
}
