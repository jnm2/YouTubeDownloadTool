using System;
using System.Threading;
using System.Threading.Tasks;
using Techsola;

namespace YouTubeDownloadTool
{
    public sealed class DownloadViewModel : ViewModel
    {
        private readonly IDownloadDataAccess dataAccess;
        private readonly CancellationTokenSource downloadRemoved = new();

        public DownloadViewModel(string url, IDownloadDataAccess dataAccess)
        {
            Url = url;
            name = url;
            this.dataAccess = dataAccess;

            AmbientTasks.Add(InitializeAsync());
        }

        private async Task InitializeAsync()
        {
            Status = "Obtaining details...";

            var result = await dataAccess.GetDetailsIfDownloadableAsync(Url, downloadRemoved.Token);

            if (result is { } details)
            {
                if (!string.IsNullOrWhiteSpace(details.Name))
                    Name = details.Name;

                UploadDate = details.UploadDate;
                Duration = details.Duration;
                Author = details.Author;
                Status = null;
            }
            else
            {
                IsInvalid = true;
                Status = "Not supported";
            }
        }

        public string Url { get; }

        private bool isInvalid;
        public bool IsInvalid { get => isInvalid; private set => Set(ref isInvalid, value); }

        private string name;
        public string Name { get => name; private set => Set(ref name, value); }

        private DateTimeOffset? uploadDate;
        public DateTimeOffset? UploadDate { get => uploadDate; private set => Set(ref uploadDate, value); }

        private TimeSpan? duration;
        public TimeSpan? Duration { get => duration; private set => Set(ref duration, value); }

        private string? author;
        public string? Author { get => author; private set => Set(ref author, value); }

        private string? status;
        public string? Status { get => status; private set => Set(ref status, value); }

        private double? progress;
        public double? Progress { get => progress; private set => Set(ref progress, value); }

        public async Task DownloadAsync(Action<string> showErrorMessage)
        {
            var result = await dataAccess.DownloadAsync(
                Url,
                destinationDirectory: @"C:\Users\Joseph\Desktop\Test download destination",
                audioOnly: true,
                cancellationToken: downloadRemoved.Token,
                progress: null);

            if (result.IsError(out var message, out var exitCode))
            {
                showErrorMessage.Invoke(
                    $"Youtube-dl.exe exited with code {exitCode} and this message:" + Environment.NewLine
                    + Environment.NewLine
                    + message);
            }
        }

        public void Remove()
        {
            downloadRemoved.Cancel();
        }
    }
}
