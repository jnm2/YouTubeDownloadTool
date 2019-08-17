using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Techsola;

namespace YouTubeDownloadTool
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            AmbientTasks.Add(TestAsync());
        }

        private async Task TestAsync()
        {
            const string productName = "YouTube download tool";

            var appDataDir = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), productName);


            using var youTubeDLResolver = new ToolResolver(
                cacheDirectory: Path.Join(appDataDir, "youtube-dl"),
                fileName: "youtube-dl.exe",
                DownloadResolvers.GitHubReleaseAsset(
                    owner: "ytdl-org",
                    repo: "youtube-dl",
                    assetName: "youtube-dl.exe",
                    userAgent: productName));

            youTubeDLResolver.PurgeOldVersions();

            AmbientTasks.Add(youTubeDLResolver.CheckForUpdatesAsync(CancellationToken.None));


            using var ffmpegResolver = new ToolResolver(
                cacheDirectory: Path.Join(appDataDir, "ffmpeg"),
                fileName: "ffmpeg.exe",
                DownloadResolvers.DownloadPage(
                    pageUrl: "https://ffmpeg.zeranoe.com/builds/win64/static/",
                    linkFileNamePattern: "ffmpeg-*-win64-static.zip",
                    version => StreamTransforms.UnzipSingleFile($"ffmpeg-{version}-win64-static/bin/ffmpeg.exe")));

            ffmpegResolver.PurgeOldVersions();

            AmbientTasks.Add(ffmpegResolver.CheckForUpdatesAsync(CancellationToken.None));


            using var ffmpegLease = await ffmpegResolver.LeaseToolAsync(CancellationToken.None);
            using var youTubeDLLease = await youTubeDLResolver.LeaseToolAsync(CancellationToken.None);

            var youTubeDL = new YouTubeDLTool(youTubeDLLease.FilePath);

            var result = await youTubeDL.DownloadToDirectoryAsync(
                "https://youtu.be/xuCn8ux2gbs",
                @"C:\Users\Joseph\Desktop\Test download destination",
                audioOnly: true);

            if (result.IsError(out var message, out var exitCode))
            {
                MessageBox.Show(
                    this,
                    $"Youtube-dl.exe exited with code {exitCode} and this message:" + Environment.NewLine
                        + Environment.NewLine
                        + message,
                    "Download error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }
}
