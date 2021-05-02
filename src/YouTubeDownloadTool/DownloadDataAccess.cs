using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace YouTubeDownloadTool
{
    public sealed class DownloadDataAccess : IDownloadDataAccess
    {
        private readonly ToolResolver youTubeDLResolver;
        private readonly ToolResolver ffmpegResolver;

        public DownloadDataAccess(string toolCachePath, string userAgent)
        {
            youTubeDLResolver = new ToolResolver(
                cacheDirectory: Path.Join(toolCachePath, "youtube-dl"),
                fileName: "youtube-dl.exe",
                DownloadResolvers.GitHubReleaseAsset(
                    owner: "ytdl-org",
                    repo: "youtube-dl",
                    assetName: "youtube-dl.exe",
                    userAgent));

            ffmpegResolver = new ToolResolver(
                cacheDirectory: Path.Join(toolCachePath, "ffmpeg"),
                fileName: "ffmpeg.exe",
                getLatestDownloadAsync: async cancellationToken =>
                {
                    using var client = OwnershipTracker.Create(
                        new HttpClient(new HttpClientHandler {AutomaticDecompression = DecompressionMethods.All}));

                    var version = await client.OwnedInstance.GetStringAsync("https://www.gyan.dev/ffmpeg/builds/release-version", cancellationToken).ConfigureAwait(false);

                    return new AvailableToolDownload(
                        version,
                        client.ReleaseOwnership(),
                        downloadUrl: "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip",
                        StreamTransforms.UnzipSingleFile($"ffmpeg-{version}-essentials_build/bin/ffmpeg.exe"));
                });
        }

        public void Dispose()
        {
            youTubeDLResolver.Dispose();
            ffmpegResolver.Dispose();
        }

        public async Task CheckForToolUpdatesAsync(CancellationToken cancellationToken)
        {
            youTubeDLResolver.PurgeOldVersions();
            ffmpegResolver.PurgeOldVersions();

            await Task.WhenAll(
                youTubeDLResolver.CheckForUpdatesAsync(cancellationToken),
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
            using var ffmpegLease = await ffmpegResolver.LeaseToolAsync(cancellationToken);
            using var youTubeDLLease = await youTubeDLResolver.LeaseToolAsync(cancellationToken);

            var youTubeDL = new YouTubeDLTool(
                youTubeDLLease.FilePath,
                ffmpegDirectory: Path.GetDirectoryName(ffmpegLease.FilePath)!);

            return await youTubeDL.DownloadToDirectoryAsync(url, destinationDirectory, audioOnly, progress, status).ConfigureAwait(false);
        }
    }
}
