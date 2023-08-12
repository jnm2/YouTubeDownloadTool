namespace YouTubeDownloadTool;

public interface IDownloadDataAccess : IDisposable
{
    Task CheckForToolUpdatesAsync(CancellationToken cancellationToken);

    Task<DownloadResult> DownloadAsync(
        string url,
        string destinationDirectory,
        bool audioOnly,
        CancellationToken cancellationToken,
        IProgress<double?>? progress,
        IProgress<string?>? status);
}
