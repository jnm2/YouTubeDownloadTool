using System;
using System.Net.Http;

namespace YouTubeDownloadTool
{
    public readonly struct AvailableToolDownload
    {
        public AvailableToolDownload(HttpClient httpClient, string downloadUrl, string version)
        {
            if (string.IsNullOrEmpty(downloadUrl))
                throw new ArgumentException("Download URL must be specified.", nameof(downloadUrl));

            if (string.IsNullOrEmpty(version))
                throw new ArgumentException("Version must be specified.", nameof(version));

            HttpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            DownloadUrl = downloadUrl;
            Version = version;
        }

        public HttpClient HttpClient { get; }
        public string DownloadUrl { get; }
        public string Version { get; }
    }
}
