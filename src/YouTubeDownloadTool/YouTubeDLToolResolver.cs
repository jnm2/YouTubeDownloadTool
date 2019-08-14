using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace YouTubeDownloadTool
{
    public sealed partial class YouTubeDLToolResolver : IDisposable
    {
        private const string ExecutableFileName = "youtube-dl.exe";

        private readonly string cacheDirectory;
        private LeaseSource? currentSource;

        public YouTubeDLToolResolver(string cacheDirectory)
        {
            this.cacheDirectory = cacheDirectory;
            currentSource = GetCurrentCachedTool();
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
            return (currentSource ?? await ResolveLatestToolAsync(cancellationToken)).CreateLease();
        }

        public async Task CheckForUpdatesAsync(CancellationToken cancellationToken)
        {
            await ResolveLatestToolAsync(cancellationToken);
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

            var (version, downloadUrl) = await GetLatestGitHubReleaseAsync(client, cancellationToken);
            AvailableVersion = version;

            if (currentSource is { } && string.Equals(version, currentSource.Tool.Version, StringComparison.OrdinalIgnoreCase))
                return currentSource;

            var fileLock = await GetOrDownloadFileAsync(
                Path.Join(cacheDirectory, "v" + version, ExecutableFileName),
                client,
                downloadUrl,
                cancellationToken);

            var newSource = new LeaseSource(version, fileLock);
            ReplaceCurrentSource(newSource);
            return newSource;
        }

        private async Task<(string version, string downloadUrl)> GetLatestGitHubReleaseAsync(HttpClient client, CancellationToken cancellationToken)
        {
            using var response = await client.GetAsync(
               "/repos/ytdl-org/youtube-dl/releases/latest",
               HttpCompletionOption.ResponseHeadersRead,
               cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

            await using (stream.ConfigureAwait(false))
            {
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

                var version = document.RootElement.GetProperty("tag_name").GetString();

                foreach (var asset in document.RootElement.GetProperty("assets").EnumerateArray())
                {
                    if ("youtube-dl.exe".Equals(asset.GetProperty("name").GetString(), StringComparison.OrdinalIgnoreCase))
                    {
                        var downloadUrl = asset.GetProperty("browser_download_url").GetString();
                        return (version, downloadUrl);
                    }
                }

                throw new NotImplementedException("Unable to find youtube-dl.exe in latest GitHub release.");
            }
        }

        private async Task<RefCountedFileLock> GetOrDownloadFileAsync(string filePath, HttpClient client, string downloadUrl, CancellationToken cancellationToken)
        {
            if (filePath is null || !Path.IsPathFullyQualified(filePath))
                throw new ArgumentException("The file path must be fully qualified.", nameof(filePath));

            if (client is null) throw new ArgumentNullException(nameof(client));

            if (string.IsNullOrWhiteSpace(downloadUrl))
                throw new ArgumentException("A download URL must be specified.", nameof(downloadUrl));

            var fileLock = RefCountedFileLock.CreateIfExists(filePath);

            if (fileLock is null)
            {
                using var tempFile = await DownloadToTempFileAsync(client, downloadUrl, cancellationToken).ConfigureAwait(false);

                do
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                    try
                    {
                        File.Move(tempFile.Path, filePath);
                    }
                    catch (IOException ex) when (ex.GetErrorCode() == WinErrorCode.AlreadyExists)
                    {
                    }

                    fileLock = RefCountedFileLock.CreateIfExists(filePath);
                } while (fileLock is null);
            }

            return fileLock;
        }

        private async Task<TempFile> DownloadToTempFileAsync(HttpClient client, string downloadUrl, CancellationToken cancellationToken)
        {
            using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            await using var _ = stream.ConfigureAwait(false);

            using var tempFile = OwnershipTracker.Create(new TempFile());

            using var file = tempFile.OwnedInstance.OpenStream();
            await stream.CopyToAsync(file, cancellationToken).ConfigureAwait(false);

            return tempFile.ReleaseOwnership();
        }
    }
}
