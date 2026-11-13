using System.IO;

namespace YouTubeDownloadTool;

public sealed partial class ToolResolver : IDisposable
{
    private readonly string cacheDirectory;
    private readonly string executableFileName;
    private readonly Func<CancellationToken, Task<AvailableToolDownload>> getLatestDownloadAsync;
    private readonly TaskDeduplicator<LeaseSource> resolveDeduplicator;
    private LeaseSource? currentSource;

    /// <param name="cacheDirectory">The directory in which a subdirectory is kept for each downloaded version.</param>
    /// <param name="executableFileName">
    /// The file within each version directory to lease. While leased, it is held open, which prevents the version
    /// directory from being purged.
    /// </param>
    /// <param name="getLatestDownloadAsync">Gets the latest available version of the tool.</param>
    public ToolResolver(
        string cacheDirectory,
        string executableFileName,
        Func<CancellationToken, Task<AvailableToolDownload>> getLatestDownloadAsync)
    {
        this.cacheDirectory = cacheDirectory;
        this.executableFileName = executableFileName;
        this.getLatestDownloadAsync = getLatestDownloadAsync;
        currentSource = GetCurrentCachedTool();

        resolveDeduplicator = new TaskDeduplicator<LeaseSource>(ResolveLatestToolAsync);
    }

    public void Dispose()
    {
        ReplaceCurrentSource(null);
    }

    private void ReplaceCurrentSource(LeaseSource? newSource)
    {
        var originalLease = currentSource?.Lease;
        currentSource = newSource;
        originalLease?.Dispose();
    }

    private LeaseSource? GetCurrentCachedTool()
    {
        foreach (var info in TryGetVersionDirectories().OrderByDescending(d => d.Version))
        {
            if (info is (_, var rawVersion, { } directory)
                && RefCountedFileLock.CreateIfExists(Path.Join(directory, executableFileName)) is { } fileLock)
            {
                return new LeaseSource(rawVersion, fileLock);
            }
        }

        return null;
    }

    public void PurgeOldVersions()
    {
        var versionDirectories = TryGetVersionDirectories();

        foreach (var (_, _, directory) in versionDirectories.OrderBy(d => d.Version).SkipLast(1))
        {
            try
            {
                // Try to delete the file first because an instance of this app may be locking it in order to invoke
                // the same version multiple times.
                File.Delete(Path.Join(directory, executableFileName));
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    public async Task<ToolLease> LeaseToolAsync(CancellationToken cancellationToken)
    {
        return (currentSource ?? await resolveDeduplicator.StartOrJoin().WithCancellation(cancellationToken)).CreateLease();
    }

    public async Task CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        await resolveDeduplicator.StartOrJoin().WithCancellation(cancellationToken);
    }

    private IEnumerable<(Version Version, string RawVersion, string Directory)> TryGetVersionDirectories()
    {
        string[] directories;
        try
        {
            directories = Directory.GetDirectories(cacheDirectory, "v*");
        }
        catch (DirectoryNotFoundException)
        {
            return Enumerable.Empty<(Version, string, string)>();
        }

        return directories.SelectWhere(directory =>
        {
            var rawVersion = Path.GetFileName(directory)[1..];
            return (
                Success: Version.TryParse(rawVersion, out var version),
                Result: (version!, rawVersion, directory));
        });
    }

    private async Task<LeaseSource> ResolveLatestToolAsync()
    {
        using var download = await getLatestDownloadAsync.Invoke(CancellationToken.None).ConfigureAwait(false);

        if (currentSource is not null && string.Equals(download.Version, currentSource.Version, StringComparison.OrdinalIgnoreCase))
            return currentSource;

        var fileLock = await GetOrDownloadFileAsync(
            Path.Join(cacheDirectory, "v" + download.Version, executableFileName),
            download.DownloadAsync,
            CancellationToken.None);

        var newSource = new LeaseSource(download.Version, fileLock);
        ReplaceCurrentSource(newSource);
        return newSource;
    }

    private static async Task<RefCountedFileLock> GetOrDownloadFileAsync(string filePath, Func<CancellationToken, Task<Stream>> downloadAsync, CancellationToken cancellationToken)
    {
        if (filePath is null || !Path.IsPathFullyQualified(filePath))
            throw new ArgumentException("The file path must be fully qualified.", nameof(filePath));

        if (downloadAsync is null) throw new ArgumentNullException(nameof(downloadAsync));

        var fileLock = RefCountedFileLock.CreateIfExists(filePath);

        if (fileLock is null)
        {
            var stream = await downloadAsync.Invoke(cancellationToken).ConfigureAwait(false);
            await using var _ = stream.ConfigureAwait(false);

            using var tempFile = new TempFile();

            await using (var file = tempFile.OpenStream())
                await stream.CopyToAsync(file, cancellationToken).ConfigureAwait(false);

            do
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                try
                {
                    File.Move(tempFile.Path, filePath);
                }
                catch (IOException ex) when (ex.GetErrorCode() == WinErrorCode.AlreadyExists)
                {
                }

                fileLock = RefCountedFileLock.CreateIfExists(filePath);
            } while (fileLock is null);
        }

        return fileLock;
    }
}
