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

            var appDataDir = Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), MainViewModel.ProductName);

            DataContext = new MainViewModel(toolCachePath: appDataDir);
        }
    }
}
