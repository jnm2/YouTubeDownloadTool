using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Interop;

namespace YouTubeDownloadTool
{
    internal static class Extensions
    {
        public static IEnumerable<TResult> SelectWhere<T, TResult>(this IEnumerable<T> source, Func<T, (bool Success, TResult Result)> tryPatternSelector)
        {
            foreach (var value in source)
            {
                if (tryPatternSelector.Invoke(value) is (true, var result))
                {
                    yield return result;
                }
            }
        }

        public static WinErrorCode GetErrorCode(this Exception exception)
        {
            return (WinErrorCode)exception.HResult;
        }

        public static System.Windows.Forms.DialogResult ShowDialog(this System.Windows.Forms.CommonDialog dialog, Window owner)
        {
            return dialog.ShowDialog(owner.AsWindowsFormsWindow());
        }

        public static System.Windows.Forms.IWin32Window AsWindowsFormsWindow(this Window window)
        {
            return new WindowsFormsWindow(window);
        }

        private sealed class WindowsFormsWindow : System.Windows.Forms.IWin32Window
        {
            private readonly Window window;

            public WindowsFormsWindow(Window window)
            {
                this.window = window;
            }

            public IntPtr Handle => new WindowInteropHelper(window).Handle;
        }

        public static void UpdateBinding(this FrameworkElement frameworkElement, DependencyProperty boundProperty, object? newValue)
        {
            frameworkElement.SetCurrentValue(boundProperty, newValue);
            frameworkElement.GetBindingExpression(boundProperty).UpdateSource();
        }
    }
}
