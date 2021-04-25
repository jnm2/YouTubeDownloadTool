using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Techsola;

namespace YouTubeDownloadTool
{
    public sealed class MainViewModel : ViewModel
    {
        private readonly IDownloadDataAccess dataAccess;
        private readonly Action<string> showError;

        private string? downloadUrl;
        public string? DownloadUrl { get => downloadUrl; set => Set(ref downloadUrl, value); }

        private bool audioOnly;
        public bool AudioOnly { get => audioOnly; set => Set(ref audioOnly, value); }

        private string? destinationFolder;
        public string? DestinationFolder { get => destinationFolder; set => Set(ref destinationFolder, value); }

        private double? progressFraction;
        public double? ProgressFraction { get => progressFraction; set => Set(ref progressFraction, value); }

        private bool isEditable = true;
        public bool IsEditable { get => isEditable; set => Set(ref isEditable, value); }

        private bool isProgressBarVisible;
        public bool IsProgressBarVisible { get => isProgressBarVisible; set => Set(ref isProgressBarVisible, value); }

        public Command Start { get; }

        public MainViewModel(IDownloadDataAccess dataAccess, Action<string> showError)
        {
            this.dataAccess = dataAccess;
            this.showError = showError;

            audioOnly = Properties.Settings.Default.AudioOnly;

            destinationFolder = Directory.Exists(Properties.Settings.Default.DestinationFolder)
                ? Properties.Settings.Default.DestinationFolder
                : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

            Start = new Command(StartAsync);

            AmbientTasks.Add(dataAccess.CheckForToolUpdatesAsync(CancellationToken.None));
        }

        public async Task StartAsync()
        {
            Trace.Assert(IsEditable, "StartAsync calls should not be able to overlap.");

            DownloadUrl = DownloadUrl?.Trim();
            if (string.IsNullOrEmpty(DownloadUrl))
            {
                showError.Invoke("Please provide a source page URL.");
                return;
            }

            if (!Uri.TryCreate(DownloadUrl, UriKind.Absolute, out _))
            {
                showError.Invoke("The source page URL was not recognized as a complete URL.");
                return;
            }

            DestinationFolder = DestinationFolder?.Trim();
            if (string.IsNullOrEmpty(DestinationFolder))
            {
                showError.Invoke("Please choose a destination folder.");
                return;
            }

            if (!Directory.Exists(DestinationFolder))
            {
                showError.Invoke("The chosen destination folder could not be accessed.");
                return;
            }

            IsEditable = false;
            ProgressFraction = null;
            IsProgressBarVisible = true;
            Start.CanExecute = false;
            try
            {
                Properties.Settings.Default.AudioOnly = AudioOnly;
                Properties.Settings.Default.DestinationFolder = DestinationFolder;
                Properties.Settings.Default.Save();

                var result = await dataAccess.DownloadAsync(
                    DownloadUrl,
                    DestinationFolder,
                    AudioOnly,
                    CancellationToken.None,
                    new Progress<double?>(value => ProgressFraction = value));

                if (result.IsError(out var message, exitCode: out _))
                    showError.Invoke(message);
                else
                    DownloadUrl = null;
            }
            finally
            {
                IsEditable = true;
                IsProgressBarVisible = false;
                Start.CanExecute = true;
            }
        }
    }
}
