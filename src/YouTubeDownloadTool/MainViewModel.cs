using System.Collections.ObjectModel;
using System.Threading;
using Techsola;

namespace YouTubeDownloadTool
{
    public sealed class MainViewModel : ViewModel
    {
        private readonly IDownloadDataAccess dataAccess;

        private string downloadUrls = string.Empty;
        public string DownloadUrls { get => downloadUrls; set => Set(ref downloadUrls, value); }

        public ObservableCollection<DownloadViewModel> Downloads { get; } = new ObservableCollection<DownloadViewModel>();

        public MainViewModel(IDownloadDataAccess dataAccess)
        {
            this.dataAccess = dataAccess;

            AmbientTasks.Add(dataAccess.CheckForToolUpdatesAsync(CancellationToken.None));
        }
    }
}
