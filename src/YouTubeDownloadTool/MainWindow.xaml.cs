﻿using System;
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
            var appDataDir = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YouTube download tool");
            var toolCacheDir = Path.Join(appDataDir, "youtube-dl");

            using var resolver = new ToolResolver(toolCacheDir, "youtube-dl.exe");
            resolver.PurgeOldVersions();

            AmbientTasks.Add(resolver.CheckForUpdatesAsync(CancellationToken.None));

            using var lease = await resolver.LeaseToolAsync(CancellationToken.None);

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
