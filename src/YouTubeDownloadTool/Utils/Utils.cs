using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace YouTubeDownloadTool;

internal static class Utils
{
    public static async Task<RefCountedFileLock> GetOrDownloadFileAsync(string filePath, Func<CancellationToken, Task<Stream>> downloadAsync, CancellationToken cancellationToken)
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

    private static readonly Func<Process, Process[]?, IReadOnlyList<Process>>? GetChildProcesses =
        typeof(Process).GetMethod("GetChildProcesses", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(Process[]) }, null)
            ?.CreateDelegate<Func<Process, Process[]?, IReadOnlyList<Process>>>();

    public static IReadOnlyList<Process>? TryFilterToChildProcesses(Process[] candidates, Process parentProcess)
    {
        return GetChildProcesses?.Invoke(parentProcess, candidates);
    }

    public static void TryKillImmediateChildrenWithProcessName(Process parentProcess, string childProcessName)
    {
        var allProcessesWithName = Process.GetProcessesByName(childProcessName);
        try
        {
            if (TryFilterToChildProcesses(allProcessesWithName, parentProcess) is { } childProcesses)
            {
                foreach (var childProcess in childProcesses)
                    childProcess.Kill();
            }
        }
        finally
        {
            foreach (var process in allProcessesWithName)
                process.Dispose();
        }
    }
}
