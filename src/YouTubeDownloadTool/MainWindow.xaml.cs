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

            using var resolver = new YouTubeDLToolResolver(toolCacheDir);
            resolver.PurgeOldVersions();

            AmbientTasks.Add(resolver.CheckForUpdatesAsync(CancellationToken.None));

            using var tool = await resolver.LeaseToolAsync(CancellationToken.None);

            await tool.Tool.DownloadToDirectoryAsync(
                "https://youtu.be/xuCn8ux2gbs",
                @"C:\Users\Joseph\Desktop\Test download destination");
        }
    }
}
