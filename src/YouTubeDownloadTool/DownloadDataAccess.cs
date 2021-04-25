using System;
using System.IO;
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
                DownloadResolvers.DownloadPage(
                    pageUrl: "https://ffmpeg.zeranoe.com/builds/win64/static/",
                    linkFileNamePattern: "ffmpeg-*-win64-static.zip",
                    version => StreamTransforms.UnzipSingleFile($"ffmpeg-{version}-win64-static/bin/ffmpeg.exe")));
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

        public Task<DownloadDetails?> GetDetailsIfDownloadableAsync(string url, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public async Task<DownloadResult> DownloadAsync(string url, string destinationDirectory, bool audioOnly, CancellationToken cancellationToken, IProgress<double>? progress)
        {
            using var ffmpegLease = await ffmpegResolver.LeaseToolAsync(CancellationToken.None);
            using var youTubeDLLease = await youTubeDLResolver.LeaseToolAsync(CancellationToken.None);

            var youTubeDL = new YouTubeDLTool(
                youTubeDLLease.FilePath,
                ffmpegDirectory: Path.GetDirectoryName(ffmpegLease.FilePath)!);

            return await youTubeDL.DownloadToDirectoryAsync(url, destinationDirectory, audioOnly).ConfigureAwait(false);
        }
    }
}
