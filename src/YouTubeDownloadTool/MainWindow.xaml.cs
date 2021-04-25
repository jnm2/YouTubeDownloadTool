using System.Windows;
using Techsola;

namespace YouTubeDownloadTool
{
    public partial class MainWindow : Window
    {
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            viewModel.DownloadUrls = "https://youtu.be/xuCn8ux2gbs";

            AmbientTasks.Add(viewModel.Downloads[0].DownloadAsync(
                ViewUtils.CreateErrorMessageHandler(owner: this)));
        }
    }
}
