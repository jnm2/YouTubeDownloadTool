using System;
using System.Windows;
using System.Windows.Interop;

namespace YouTubeDownloadTool
{
    internal static class ViewUtils
    {
        public static Action<string> CreateErrorMessageHandler(Window owner)
        {
            return errorMessage =>
            {
                System.Windows.Forms.TaskDialog.ShowDialog(
                    new WindowInteropHelper(owner).Handle,
                    new()
                    {
                        Caption = owner.Title,
                        Text = errorMessage,
                        Icon = System.Windows.Forms.TaskDialogIcon.Error,
                    });
            };
        }
    }
}
