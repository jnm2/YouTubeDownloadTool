using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace YouTubeDownloadTool
{
    public static class DownloadResolvers
    {
        public static Func<CancellationToken, Task<AvailableToolDownload>> GitHubReleaseAsset(string owner, string repo, string assetName, string userAgent)
        {
            if (string.IsNullOrWhiteSpace(owner))
                throw new ArgumentException("Owner must be specified.", nameof(owner));

            if (string.IsNullOrWhiteSpace(repo))
                throw new ArgumentException("Repository must be specified.", nameof(repo));

            if (string.IsNullOrWhiteSpace(assetName))
                throw new ArgumentException("Asset name must be specified.", nameof(assetName));

            if (string.IsNullOrWhiteSpace(userAgent))
                throw new ArgumentException("User agent must be specified.", nameof(userAgent));

            return async cancellationToken =>
            {
                using var client = OwnershipTracker.Create(
                    new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
                    {
                        BaseAddress = new Uri("https://api.github.com"),
                        DefaultRequestHeaders = { { "User-Agent", userAgent } }
                    });

                var (version, downloadUrl) = await GetLatestGitHubReleaseAssetAsync(client.OwnedInstance, owner, repo, assetName, cancellationToken);

                if (downloadUrl is null)
                    throw new NotImplementedException($"Unable to find {assetName} in latest GitHub release.");

                return new AvailableToolDownload(client.ReleaseOwnership(), downloadUrl, version);
            };
        }

        private static async Task<(string version, string? downloadUrl)> GetLatestGitHubReleaseAssetAsync(HttpClient client, string owner, string repo, string? assetName, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage
            {
                RequestUri = new Uri($"/repos/{owner}/{repo}/releases/latest", UriKind.Relative),
                Headers =
                {
                    Accept = { new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json") }
                }
            };

            using var response = await client.SendAsync(
               request,
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
    }
}
