using System.Diagnostics;
using System.IO;
using Techsola;

namespace YouTubeDownloadTool;

public sealed class MainViewModel : ObservableObject
{
    private CancellationTokenSource? cancelSource;

    private readonly IDownloadDataAccess dataAccess;
    private readonly Action<(string Message, bool IsError)> showNotification;

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

    private string? status;
    public string? Status { get => status; set => Set(ref status, value); }

    public Command Start { get; }

    public Command Cancel { get; }

    public MainViewModel(IDownloadDataAccess dataAccess, Action<(string Message, bool IsError)> showNotification)
    {
        this.dataAccess = dataAccess;
        this.showNotification = showNotification;

        audioOnly = Properties.Settings.Default.AudioOnly;

        destinationFolder = Directory.Exists(Properties.Settings.Default.DestinationFolder)
            ? Properties.Settings.Default.DestinationFolder
            : Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

        Start = new Command(StartAsync);
        Cancel = new Command(() => cancelSource!.Cancel()) { CanExecute = false };

        AmbientTasks.Add(dataAccess.CheckForToolUpdatesAsync(CancellationToken.None));
    }

    public async Task StartAsync()
    {
        Trace.Assert(IsEditable, "StartAsync calls should not be able to overlap.");

        DownloadUrl = DownloadUrl?.Trim();
        if (string.IsNullOrEmpty(DownloadUrl))
        {
            showNotification.Invoke(("Please provide a source page URL.", IsError: true));
            return;
        }

        if (!Uri.TryCreate(DownloadUrl, UriKind.Absolute, out _))
        {
            showNotification.Invoke(("The source page URL was not recognized as a complete URL.", IsError: true));
            return;
        }

        DestinationFolder = DestinationFolder?.Trim();
        if (string.IsNullOrEmpty(DestinationFolder))
        {
            showNotification.Invoke(("Please choose a destination folder.", IsError: true));
            return;
        }

        if (!Directory.Exists(DestinationFolder))
        {
            showNotification.Invoke(("The chosen destination folder could not be accessed.", IsError: true));
            return;
        }

        IsEditable = false;
        ProgressFraction = null;
        IsProgressBarVisible = true;
        Start.CanExecute = false;
        cancelSource = new();
        Cancel.CanExecute = true;
        try
        {
            Properties.Settings.Default.AudioOnly = AudioOnly;
            Properties.Settings.Default.DestinationFolder = DestinationFolder;
            Properties.Settings.Default.Save();

            var result = await dataAccess.DownloadAsync(
                DownloadUrl,
                DestinationFolder,
                AudioOnly,
                cancelSource.Token,
                new Progress<double?>(value => ProgressFraction = value),
                new Progress<string?>(value => Status = value));

            Status = null;

            if (result.IsSuccess)
                DownloadUrl = null;

            if (result.Message is not null)
                showNotification.Invoke((result.Message, IsError: !result.IsSuccess));
        }
        finally
        {
            Status = null;
            IsEditable = true;
            IsProgressBarVisible = false;
            Start.CanExecute = true;
            Cancel.CanExecute = false;
            cancelSource = null;
        }
    }
}
