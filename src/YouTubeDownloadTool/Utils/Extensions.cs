using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace YouTubeDownloadTool
{
    internal static class Extensions
    {
        public static IEnumerable<TResult> SelectWhere<T, TResult>(this IEnumerable<T> source, Func<T, (bool success, TResult result)> tryPatternSelector)
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

            public ProcessExitAwaiter GetAwaiter() => new ProcessExitAwaiter(process);

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
    }
}
