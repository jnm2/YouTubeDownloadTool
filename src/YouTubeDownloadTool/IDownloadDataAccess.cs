using System;
using System.Threading;
using System.Threading.Tasks;

namespace YouTubeDownloadTool
{
    public interface IDownloadDataAccess : IDisposable
    {
        Task CheckForToolUpdatesAsync(CancellationToken cancellationToken);
        Task<DownloadDetails?> GetDetailsIfDownloadableAsync(string url, CancellationToken cancellationToken);
        Task<DownloadResult> DownloadAsync(string url, string destinationDirectory, bool audioOnly, CancellationToken cancellationToken, IProgress<double>? progress);
    }
}
