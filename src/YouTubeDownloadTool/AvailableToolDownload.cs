using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace YouTubeDownloadTool;

public sealed class AvailableToolDownload : IDisposable
{
    private readonly HttpClient httpClient;
    private readonly string downloadUrl;
    private readonly Func<Stream, Stream>? streamTransform;

    public string Version { get; }

    public AvailableToolDownload(string version, HttpClient httpClient, string downloadUrl, Func<Stream, Stream>? streamTransform = null)
    {
        Version = version;
        this.httpClient = httpClient;
        this.downloadUrl = downloadUrl;
        this.streamTransform = streamTransform;
    }

    public void Dispose()
    {
        httpClient.Dispose();
    }

    public async Task<Stream> DownloadAsync(CancellationToken cancellationToken)
    {
        var response = await httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        return streamTransform is { }
            ? streamTransform.Invoke(stream)
            : stream;
    }
}
