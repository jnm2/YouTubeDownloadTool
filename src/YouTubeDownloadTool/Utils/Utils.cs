using System.Diagnostics;
using System.Reflection;

namespace YouTubeDownloadTool;

internal static class Utils
{
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
