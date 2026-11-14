using System.IO;
using System.Net;
using System.Net.Http;

namespace YouTubeDownloadTool;

public sealed class DownloadDataAccess : IDownloadDataAccess
{
    private readonly ToolResolver ytDlpResolver;
    private readonly ToolResolver ffmpegResolver;
    private readonly ToolResolver denoResolver;

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

        // yt-dlp needs a JavaScript runtime for full YouTube support, and Deno is the one it uses by default.
        denoResolver = new ToolResolver(
            cacheDirectory: Path.Join(toolCachePath, "deno"),
            executableFileName: "deno.exe",
            DownloadResolvers.GitHubReleaseAsset(
                owner: "denoland",
                repo: "deno",
                assetName: "deno-x86_64-pc-windows-msvc.zip",
                userAgent,
                ToolInstallers.Unzip()));
    }

    public void Dispose()
    {
        ytDlpResolver.Dispose();
        ffmpegResolver.Dispose();
        denoResolver.Dispose();
    }

    public async Task CheckForToolUpdatesAsync(CancellationToken cancellationToken)
    {
        ytDlpResolver.PurgeOldVersions();
        ffmpegResolver.PurgeOldVersions();
        denoResolver.PurgeOldVersions();

        await Task.WhenAll(
            ytDlpResolver.CheckForUpdatesAsync(cancellationToken),
            ffmpegResolver.CheckForUpdatesAsync(cancellationToken),
            denoResolver.CheckForUpdatesAsync(cancellationToken)).ConfigureAwait(false);
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
        var denoResolution = denoResolver.LeaseToolAsync(cancellationToken);

        var downloadingTools = new List<string>();
        if (!ffmpegResolution.IsCompleted) downloadingTools.Add("ffmpeg");
        if (!ytDlpResolution.IsCompleted) downloadingTools.Add("yt-dlp");
        if (!denoResolution.IsCompleted) downloadingTools.Add("Deno");

        if (downloadingTools.Count > 0)
            status?.Report($"Downloading {FormatList(downloadingTools)}...");

        using var ffmpegLease = await ffmpegResolution;
        using var ytDlpLease = await ytDlpResolution;
        using var denoLease = await denoResolution;

        status?.Report(null);

        var ytDlp = new YTDlpTool(
            ytDlpLease.FilePath,
            ffmpegDirectory: Path.GetDirectoryName(ffmpegLease.FilePath)!,
            denoPath: denoLease.FilePath);

        return await ytDlp.DownloadToDirectoryAsync(url, destinationDirectory, audioOnly, cancellationToken, progress, status).ConfigureAwait(false);
    }

    private static string FormatList(IReadOnlyList<string> items)
    {
        return items.Count switch
        {
            1 => items[0],
            2 => $"{items[0]} and {items[1]}",
            _ => $"{string.Join(", ", items.SkipLast(1))}, and {items[^1]}",
        };
    }
}
