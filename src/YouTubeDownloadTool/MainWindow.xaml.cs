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

            using var lease = await youTubeDLResolver.LeaseToolAsync(CancellationToken.None);

            var tool = new YouTubeDLTool(lease.FilePath);

            var result = await tool.DownloadToDirectoryAsync(
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
