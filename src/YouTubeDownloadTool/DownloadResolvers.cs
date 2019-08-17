using LtGt;
using LtGt.Models;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace YouTubeDownloadTool
{
    public static class DownloadResolvers
    {
        public static Func<CancellationToken, Task<AvailableToolDownload>> DownloadPage(string pageUrl, string linkFileNamePattern)
        {
            if (string.IsNullOrWhiteSpace(pageUrl))
                throw new ArgumentException("Page URL must be specified.", nameof(pageUrl));

            if (string.IsNullOrWhiteSpace(linkFileNamePattern))
                throw new ArgumentException("Link URL pattern must be specified.", nameof(linkFileNamePattern));

            var parts = linkFileNamePattern.Split('*', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                throw new ArgumentException("There must be at least one asterisk as a wildcard to match the version part of the URL.", nameof(linkFileNamePattern));

            var regex = new Regex(
                @"(?:\A|\\|/)" + string.Join("(.*)", parts.Select(Regex.Escape)) + @"\Z",
                RegexOptions.IgnoreCase);

            return async cancellationToken =>
            {
                using var client = OwnershipTracker.Create(
                    new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All }));

                var page = await client.OwnedInstance.GetStringAsync(pageUrl).ConfigureAwait(false);

                var document = HtmlParser.Default.ParseDocument(page);

                var max = ((Version version, string rawVersion, string href)?)null;

                foreach (var link in document.GetElementsByTagName("a"))
                {
                    if (link.GetAttribute("href") is { Value: { } href }
                        && regex.Match(href) is { Success: true } match)
                    {
                        var current = ((Version version, string rawVersion)?)null;

                        foreach (Group? group in match.Groups)
                        {
                            if (Version.TryParse(group?.Value, out var versionCandidate))
                            {
                                if (current is { })
                                    throw new NotImplementedException("More than one wildcard match may represent a version.");
                                current = (versionCandidate, group.Value);
                            }
                        }

                        if (current is (var version, var rawVersion))
                            max = (version, rawVersion, href);
                    }
                }

                {
                    if (max is (_, var rawVersion, var href))
                    {
                        var downloadUrl = new Uri(baseUri: new Uri(pageUrl), relativeUri: href).AbsoluteUri;

                        return new AvailableToolDownload(client.ReleaseOwnership(), downloadUrl, rawVersion);
                    }
                }

                throw new NotImplementedException("No link on the page had a URL that matched the specified pattern with a wildcard matching a parseable version.");
            };
        }

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
