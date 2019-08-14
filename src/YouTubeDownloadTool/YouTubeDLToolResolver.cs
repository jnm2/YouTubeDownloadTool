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
    public sealed class YouTubeDLToolResolver : IDisposable
    {
        private const string ExecutableFileName = "youtube-dl.exe";

        private readonly string cacheDirectory;
        private FileStream? currentFileLock;

        public YouTubeDLToolResolver(string cacheDirectory)
        {
            this.cacheDirectory = cacheDirectory;

            if (TryGetVersionDirectories()
                .OrderByDescending(d => d.version)
                .FirstOrDefault() is (_, var rawVersion, { } directory))
            {
                currentFileLock = GetLockIfExists(Path.Join(directory, ExecutableFileName));
                if (currentFileLock is { }) CurrentVersion = rawVersion;
            }
        }

        public void Dispose()
        {
            currentFileLock?.Dispose();
        }

        public string? CurrentVersion { get; private set; }
        public string? AvailableVersion { get; private set; }

        private FileStream? GetLockIfExists(string executablePath)
        {
            try
            {
                return new FileStream(executablePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch (Exception ex) when (ex is FileNotFoundException || ex is DirectoryNotFoundException)
            {
                return null;
            }
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

        public async Task<YouTubeDLTool> GetToolAsync(CancellationToken cancellationToken)
        {
            var fileLock = currentFileLock
                ?? await ResolveLatestToolAsync(cancellationToken);

            return new YouTubeDLTool(fileLock.Name);
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

        private async Task<FileStream> ResolveLatestToolAsync(CancellationToken cancellationToken)
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

            if (string.Equals(version, CurrentVersion, StringComparison.OrdinalIgnoreCase))
                return currentFileLock!; // CurrentVersion is always null if currentFileLock is. TODO: Join CurrentVersion and currentFileLock

            var executablePath = Path.Join(cacheDirectory, "v" + version, ExecutableFileName);

            var newFileLock = GetLockIfExists(executablePath);
            if (newFileLock is null)
            {
                using var tempFile = await DownloadToTempFileAsync(client, downloadUrl, cancellationToken);

                do
                {
                    try
                    {
                        File.Move(tempFile.Path, executablePath);
                    }
                    catch (IOException)
                    {
                    }

                    newFileLock = GetLockIfExists(executablePath);
                } while (newFileLock is null);
            }

            var oldFileLock = currentFileLock;
            currentFileLock = newFileLock;
            CurrentVersion = version;
            oldFileLock?.Dispose();

            return newFileLock;
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
