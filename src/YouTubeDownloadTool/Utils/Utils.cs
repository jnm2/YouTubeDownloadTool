using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace YouTubeDownloadTool
{
    internal static class Utils
    {
        public static async Task<(string version, string? downloadUrl)> GetLatestGitHubReleaseAssetAsync(HttpClient client, string owner, string repo, string? assetName, CancellationToken cancellationToken)
        {
            using var response = await client.GetAsync(
               $"/repos/{owner}/{repo}/releases/latest",
               HttpCompletionOption.ResponseHeadersRead,
               cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

            await using (stream.ConfigureAwait(false))
            {
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

                var version = document.RootElement.GetProperty("tag_name").GetString();

                if (assetName is { })
                {
                    foreach (var asset in document.RootElement.GetProperty("assets").EnumerateArray())
                    {
                        if (assetName.Equals(asset.GetProperty("name").GetString(), StringComparison.OrdinalIgnoreCase))
                        {
                            var downloadUrl = asset.GetProperty("browser_download_url").GetString();
                            return (version, downloadUrl);
                        }
                    }
                }

                return (version, null);
            }
        }

        public static async Task<RefCountedFileLock> GetOrDownloadFileAsync(string filePath, HttpClient client, string downloadUrl, CancellationToken cancellationToken)
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

        public static async Task<TempFile> DownloadToTempFileAsync(HttpClient client, string downloadUrl, CancellationToken cancellationToken)
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
