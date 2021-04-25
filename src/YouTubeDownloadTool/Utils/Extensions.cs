using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
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

        public static ProcessExitAwaitable WaitForExitAsync(this Process process)
        {
            return new ProcessExitAwaitable(process);
        }

        public readonly struct ProcessExitAwaitable
        {
            private readonly Process process;

            public ProcessExitAwaitable(Process process)
            {
                this.process = process ?? throw new ArgumentNullException(nameof(process));
            }

            public ProcessExitAwaiter GetAwaiter() => new(process);

            public readonly struct ProcessExitAwaiter : ICriticalNotifyCompletion
            {
                private readonly Process process;

                public ProcessExitAwaiter(Process process)
                {
                    this.process = process ?? throw new ArgumentNullException(nameof(process));
                }

                public bool IsCompleted => process.HasExited;

                public void OnCompleted(Action continuation)
                {
                    if (continuation is null) return;

                    var closure = new OnCompletedClosure(continuation);

                    process.EnableRaisingEvents = true;
                    process.Exited += closure.OnProcessExited;
                    if (process.HasExited) closure.InvokeContinuation();
                }

                private sealed class OnCompletedClosure
                {
                    private Action? continuation;

                    public OnCompletedClosure(Action continuation)
                    {
                        this.continuation = continuation;
                    }

                    public void OnProcessExited(object? sender, EventArgs e)
                    {
                        InvokeContinuation();
                    }

                    public void InvokeContinuation()
                    {
                        Interlocked.Exchange(ref continuation, null)?.Invoke();
                    }
                }

                public void UnsafeOnCompleted(Action continuation)
                {
                    OnCompleted(continuation);
                }

                public void GetResult()
                {
                    if (!process.HasExited)
                        throw new InvalidOperationException("GetResult may only be called after IsCompleted returns true or OnCompleted invokes a callback.");
                }
            }
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
