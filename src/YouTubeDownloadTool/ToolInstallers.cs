using System.IO;
using System.IO.Compression;

namespace YouTubeDownloadTool;

/// <summary>
/// Writes the contents of a downloaded tool into an empty directory.
/// </summary>
public delegate Task ToolInstaller(Stream download, string targetDirectory, CancellationToken cancellationToken);

public static class ToolInstallers
{
    public static ToolInstaller SaveAs(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name must be specified.", nameof(fileName));

        return async (download, targetDirectory, cancellationToken) =>
        {
            var file = new FileStream(Path.Join(targetDirectory, fileName), FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await using (file.ConfigureAwait(false))
                await download.CopyToAsync(file, cancellationToken).ConfigureAwait(false);
        };
    }

    public static ToolInstaller UnzipSingleFile(string zippedPath, string fileName)
    {
        if (string.IsNullOrWhiteSpace(zippedPath))
            throw new ArgumentException("Zipped path must be specified.", nameof(zippedPath));

        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("File name must be specified.", nameof(fileName));

        return async (download, targetDirectory, cancellationToken) =>
        {
            var archive = await ZipArchive.CreateAsync(download, ZipArchiveMode.Read, leaveOpen: true, entryNameEncoding: null, cancellationToken).ConfigureAwait(false);
            await using (archive.ConfigureAwait(false))
            {
                var entry = archive.GetEntry(zippedPath)
                    ?? throw new InvalidOperationException("No entry was found with the following path: " + zippedPath);

                await entry.ExtractToFileAsync(Path.Join(targetDirectory, fileName), cancellationToken).ConfigureAwait(false);
            }
        };
    }
}
