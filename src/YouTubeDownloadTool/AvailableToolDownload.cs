using System.Net.Http;

namespace YouTubeDownloadTool;

public sealed class AvailableToolDownload : IDisposable
{
    private readonly HttpClient httpClient;
    private readonly string downloadUrl;
    private readonly ToolInstaller installer;

    public string Version { get; }

    public AvailableToolDownload(string version, HttpClient httpClient, string downloadUrl, ToolInstaller installer)
    {
        Version = version;
        this.httpClient = httpClient;
        this.downloadUrl = downloadUrl;
        this.installer = installer ?? throw new ArgumentNullException(nameof(installer));
    }

    public void Dispose()
    {
        httpClient.Dispose();
    }

    public async Task InstallAsync(string targetDirectory, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        // Download fully before installing so that installers get a seekable stream (e.g. zip archives) without
        // buffering the whole download in memory.
        using var tempFile = new TempFile();

        var file = tempFile.OpenStream();
        await using (file.ConfigureAwait(false))
        {
            await response.Content.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
            file.Position = 0;

            await installer.Invoke(file, targetDirectory, cancellationToken).ConfigureAwait(false);
        }
    }
}
