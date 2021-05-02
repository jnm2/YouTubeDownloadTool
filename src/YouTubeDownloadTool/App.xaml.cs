using System;
using System.IO;
using System.Windows;

namespace YouTubeDownloadTool
{
    public partial class App : Application
    {
        public const string ProductName = "YouTube download tool";

        protected override void OnStartup(StartupEventArgs e)
        {
            var appDataDir = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ProductName);

            var dataAccess = new DownloadDataAccess(
                toolCachePath: appDataDir,
                userAgent: ProductName);

            var window = new MainWindow();

            window.DataContext = new MainViewModel(
                dataAccess,
                ViewUtils.CreateNotificationHandler(window));

            window.Closed += (sender, args) => dataAccess.Dispose();
            window.Show();
        }
    }
}
