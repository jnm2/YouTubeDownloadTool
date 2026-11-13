using System.IO;

namespace YouTubeDownloadTool;

public sealed partial class ToolResolver : IDisposable
{
    // Must not start with "v" so that it can never be mistaken for a version directory.
    private const string StagingDirectoryPrefix = "staging_";
    private const int MaxInstallAttempts = 10;

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
                // Try to delete the executable first because an instance of this app may be locking it in order to
                // invoke the same version multiple times. A version directory is not used without its executable,
                // so it does not matter if the rest of the directory is only partially deleted.
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

    private string[] TryGetDirectories(string searchPattern)
    {
        try
        {
            return Directory.GetDirectories(cacheDirectory, searchPattern);
        }
        catch (DirectoryNotFoundException)
        {
            return [];
        }
    }

    private IEnumerable<(Version Version, string RawVersion, string Directory)> TryGetVersionDirectories()
    {
        return TryGetDirectories("v*").SelectWhere(directory =>
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

        var fileLock = await GetOrInstallVersionAsync(
            Path.Join(cacheDirectory, "v" + download.Version),
            download.InstallAsync,
            CancellationToken.None);

        var newSource = new LeaseSource(download.Version, fileLock);
        ReplaceCurrentSource(newSource);
        return newSource;
    }

    private async Task<RefCountedFileLock> GetOrInstallVersionAsync(
        string versionDirectory,
        Func<string, CancellationToken, Task> installAsync,
        CancellationToken cancellationToken)
    {
        var executablePath = Path.Join(versionDirectory, executableFileName);

        if (RefCountedFileLock.CreateIfExists(executablePath) is { } existingLock)
            return existingLock;

        // Installing into a staging directory and then moving it into place means that a version directory never has
        // partial contents. Staging inside the cache directory keeps the move on the same volume.
        var stagingDirectory = Path.Join(cacheDirectory, StagingDirectoryPrefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingDirectory);
        try
        {
            await installAsync.Invoke(stagingDirectory, cancellationToken).ConfigureAwait(false);

            if (!File.Exists(Path.Join(stagingDirectory, executableFileName)))
                throw new InvalidOperationException($"The downloaded tool does not contain {executableFileName}.");

            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    Directory.Move(stagingDirectory, versionDirectory);
                }
                catch (IOException ex) when (ex.GetErrorCode() == WinErrorCode.AlreadyExists)
                {
                    // Another instance of this app may have installed the same version first.
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException && attempt < MaxInstallAttempts)
                {
                    // Moving a directory fails while any file in it is open, for example by an antivirus scan of the
                    // newly extracted files.
                    await Task.Delay(TimeSpan.FromMilliseconds(100 * attempt), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                if (RefCountedFileLock.CreateIfExists(executablePath) is { } fileLock)
                    return fileLock;

                if (attempt >= MaxInstallAttempts)
                    throw new IOException($"Unable to install to {versionDirectory}.");

                // The version directory exists without its executable, so it is being deleted or was left behind by
                // an incomplete purge.
                TryDeleteDirectory(versionDirectory);
            }
        }
        finally
        {
            TryDeleteDirectory(stagingDirectory);
        }
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
