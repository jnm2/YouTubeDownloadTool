using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace YouTubeDownloadTool
{
    public sealed partial class YouTubeDLToolResolver : IDisposable
    {
        private const string ExecutableFileName = "youtube-dl.exe";

        private readonly string cacheDirectory;
        private readonly TaskDeduplicator<LeaseSource> resolveDeduplicator;
        private LeaseSource? currentSource;

        public YouTubeDLToolResolver(string cacheDirectory)
        {
            this.cacheDirectory = cacheDirectory;
            currentSource = GetCurrentCachedTool();

            resolveDeduplicator = new TaskDeduplicator<LeaseSource>(ResolveLatestToolAsync);
        }

        public void Dispose()
        {
            ReplaceCurrentSource(null);
        }

        private void ReplaceCurrentSource(LeaseSource? newSource)
        {
            var originalLease = currentSource?.Lease;
            currentSource = newSource;
            originalLease?.Dispose();
        }

        public string? AvailableVersion { get; private set; }

        private LeaseSource? GetCurrentCachedTool()
        {
            foreach (var info in TryGetVersionDirectories().OrderByDescending(d => d.version))
            {
                if (info is (_, var rawVersion, { } directory)
                    && RefCountedFileLock.CreateIfExists(Path.Join(directory, ExecutableFileName)) is { } fileLock)
                {
                    return new LeaseSource(rawVersion, fileLock);
                }
            }

            return null;
        }

        public void PurgeOldVersions()
        {
            var versionDirectories = TryGetVersionDirectories();

            foreach (var (_, _, directory) in versionDirectories.OrderBy(d => d.version).SkipLast(1))
            {
                try
                {
                    // Try to delete the file first because an instance of this app may be locking it in order to invoke
                    // the same version multiple times.
                    File.Delete(Path.Join(directory, ExecutableFileName));
                    Directory.Delete(directory, recursive: true);
                }
                catch (IOException)
                {
                }
            }
        }

        public async Task<YouTubeDLToolLease> LeaseToolAsync(CancellationToken cancellationToken)
        {
            return (currentSource ?? await resolveDeduplicator.StartOrJoin(cancellationToken)).CreateLease();
        }

        public async Task CheckForUpdatesAsync(CancellationToken cancellationToken)
        {
            await resolveDeduplicator.StartOrJoin(cancellationToken);
        }

        private IEnumerable<(Version version, string rawVersion, string directory)> TryGetVersionDirectories()
        {
            string[] directories;
            try
            {
                directories = Directory.GetDirectories(cacheDirectory, "v*");
            }
            catch (DirectoryNotFoundException)
            {
                return Enumerable.Empty<(Version, string, string)>();
            }

            return directories.SelectWhere(directory =>
            {
                var rawVersion = Path.GetFileName(directory).Substring(1);
                return (
                    success: Version.TryParse(rawVersion, out var version),
                    result: (version: version!, rawVersion, directory));
            });
        }

        private async Task<LeaseSource> ResolveLatestToolAsync(CancellationToken cancellationToken)
        {
            using var client = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
            {
                BaseAddress = new Uri("https://api.github.com"),
                DefaultRequestHeaders =
                {
                    Accept = { new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json") }
                }
            };
            client.DefaultRequestHeaders.Add("User-Agent", "YouTube download tool");

            const string assetToDownload = "youtube-dl.exe";

            var (version, downloadUrl) = await Utils.GetLatestGitHubReleaseAssetAsync(
                client,
                owner: "ytdl-org",
                repo: "youtube-dl",
                assetToDownload,
                cancellationToken);

            if (downloadUrl is null)
                throw new NotImplementedException($"Unable to find {assetToDownload} in latest GitHub release.");

            AvailableVersion = version;

            if (currentSource is { } && string.Equals(version, currentSource.Tool.Version, StringComparison.OrdinalIgnoreCase))
                return currentSource;

            var fileLock = await Utils.GetOrDownloadFileAsync(
                Path.Join(cacheDirectory, "v" + version, ExecutableFileName),
                client,
                downloadUrl,
                cancellationToken);

            var newSource = new LeaseSource(version, fileLock);
            ReplaceCurrentSource(newSource);
            return newSource;
        }
    }
}
