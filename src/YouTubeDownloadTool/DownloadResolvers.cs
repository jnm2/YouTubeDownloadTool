using System.Net;
using System.Net.Http;

namespace YouTubeDownloadTool;

public static class DownloadResolvers
{
    public static Func<CancellationToken, Task<AvailableToolDownload>> GitHubReleaseAsset(
        string owner,
        string repo,
        string assetName,
        string userAgent,
        ToolInstaller installer)
    {
        if (string.IsNullOrWhiteSpace(owner))
            throw new ArgumentException("Owner must be specified.", nameof(owner));

        if (string.IsNullOrWhiteSpace(repo))
            throw new ArgumentException("Repository must be specified.", nameof(repo));

        if (string.IsNullOrWhiteSpace(assetName))
            throw new ArgumentException("Asset name must be specified.", nameof(assetName));

        if (string.IsNullOrWhiteSpace(userAgent))
            throw new ArgumentException("User agent must be specified.", nameof(userAgent));

        if (installer is null)
            throw new ArgumentNullException(nameof(installer));

        return async cancellationToken =>
        {
            using var client = OwnershipTracker.Create(
                new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
                {
                    BaseAddress = new Uri("https://github.com"),
                    DefaultRequestHeaders = { { "User-Agent", userAgent } }
                });

            var version = await GetLatestGitHubReleaseTagAsync(client.OwnedInstance, owner, repo, cancellationToken).ConfigureAwait(false);

            var downloadUrl = $"https://github.com/{owner}/{repo}/releases/download/{Uri.EscapeDataString(version)}/{Uri.EscapeDataString(assetName)}";

            return new AvailableToolDownload(version, client.ReleaseOwnership(), downloadUrl, installer);
        };
    }

    private static async Task<string> GetLatestGitHubReleaseTagAsync(HttpClient client, string owner, string repo, CancellationToken cancellationToken)
    {
        // The GitHub API only allows 60 unauthenticated requests per hour from each IP address, which is easy to exceed
        // because each launch of the app checks every tool for updates. This web page is not subject to that limit,
        // and it redirects to the page for the latest release's tag.
        using var request = new HttpRequestMessage(HttpMethod.Head, $"/{owner}/{repo}/releases/latest");

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        if (response.RequestMessage?.RequestUri?.Segments is [.., "tag/", var escapedTag])
            return Uri.UnescapeDataString(escapedTag);

        throw new InvalidOperationException(
            $"Unable to find the latest release of {owner}/{repo}. It redirected to {response.RequestMessage?.RequestUri}.");
    }
}
