using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace YouTubeDownloadTool
{
    internal static class Utils
    {
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
