using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace YouTubeDownloadTool
{
    public sealed partial class ToolResolver : IDisposable
    {
        private readonly string cacheDirectory;
        private readonly string fileName;
        private readonly Func<CancellationToken, Task<AvailableToolDownload>> getLatestDownloadAsync;
        private readonly TaskDeduplicator<LeaseSource> resolveDeduplicator;
        private LeaseSource? currentSource;

        public ToolResolver(
            string cacheDirectory,
            string fileName,
            Func<CancellationToken, Task<AvailableToolDownload>> getLatestDownloadAsync)
        {
            this.cacheDirectory = cacheDirectory;
            this.fileName = fileName;
            this.getLatestDownloadAsync = getLatestDownloadAsync;
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

        private LeaseSource? GetCurrentCachedTool()
        {
            foreach (var info in TryGetVersionDirectories().OrderByDescending(d => d.version))
            {
                if (info is (_, var rawVersion, { } directory)
                    && RefCountedFileLock.CreateIfExists(Path.Join(directory, fileName)) is { } fileLock)
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
                    File.Delete(Path.Join(directory, fileName));
                    Directory.Delete(directory, recursive: true);
                }
                catch (IOException)
                {
                }
            }
        }

        public async Task<ToolLease> LeaseToolAsync(CancellationToken cancellationToken)
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
                var rawVersion = Path.GetFileName(directory)[1..];
                return (
                    success: Version.TryParse(rawVersion, out var version),
                    result: (version: version!, rawVersion, directory));
            });
        }

        private async Task<LeaseSource> ResolveLatestToolAsync(CancellationToken cancellationToken)
        {
            using var download = await getLatestDownloadAsync.Invoke(cancellationToken).ConfigureAwait(false);

            if (currentSource is { } && string.Equals(download.Version, currentSource.Version, StringComparison.OrdinalIgnoreCase))
                return currentSource;

            var fileLock = await Utils.GetOrDownloadFileAsync(
                Path.Join(cacheDirectory, "v" + download.Version, fileName),
                download.DownloadAsync,
                cancellationToken);

            var newSource = new LeaseSource(download.Version, fileLock);
            ReplaceCurrentSource(newSource);
            return newSource;
        }
    }
}
