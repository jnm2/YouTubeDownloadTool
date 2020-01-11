using System;
using System.Windows;

namespace YouTubeDownloadTool
{
    internal static class ViewUtils
    {
        public static Action<string> CreateErrorMessageHandler(Window owner)
        {
            return errorMessage =>
            {
                MessageBox.Show(
                    owner,
                    errorMessage,
                    owner.Title,
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            };
        }
    }
}
