using System;
using System.Windows;
using System.Windows.Interop;

namespace YouTubeDownloadTool
{
    internal static class ViewUtils
    {
        public static Action<(string Message, bool IsError)> CreateNotificationHandler(Window owner)
        {
            return args =>
            {
                System.Windows.Forms.TaskDialog.ShowDialog(
                    new WindowInteropHelper(owner).Handle,
                    new()
                    {
                        Caption = owner.Title,
                        Text = args.Message,
                        Icon = args.IsError
                            ? System.Windows.Forms.TaskDialogIcon.Error
                            : System.Windows.Forms.TaskDialogIcon.Information,
                    });
            };
        }
    }
}
