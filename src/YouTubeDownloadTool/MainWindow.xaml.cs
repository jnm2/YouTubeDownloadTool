using System;
using System.IO;
using System.Threading;
using System.Windows;

namespace YouTubeDownloadTool
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            var appDataDir = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YouTube download tool");
            var toolCacheDir = Path.Join(appDataDir, "youtube-dl");

            var resolver = new YouTubeDLToolResolver(toolCacheDir);
            resolver.PurgeOldVersions();
            _ = resolver.CheckForUpdatesAsync(CancellationToken.None);
        }
    }
}
