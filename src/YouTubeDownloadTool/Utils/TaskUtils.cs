using System;
using System.Threading;
using System.Threading.Tasks;

namespace YouTubeDownloadTool;

internal static class TaskUtils
{
    public static Task<T> InvokeWithExceptionsWrapped<T>(this Func<Task<T>> func)
    {
        if (func is null) throw new ArgumentNullException(nameof(func));

        try
        {
            return func.Invoke();
        }
        catch (OperationCanceledException ex)
        {
            return Task.FromCanceled<T>(ex.CancellationToken);
        }
        catch (Exception ex)
        {
            return Task.FromException<T>(ex);
        }
    }

    public static void MirrorExistingTask<T>(TaskCompletionSource<T> taskCompletionSource, Task<T> existingTask)
    {
        if (taskCompletionSource is null) throw new ArgumentNullException(nameof(taskCompletionSource));
        if (existingTask is null) throw new ArgumentNullException(nameof(existingTask));

        existingTask.ContinueWith(OnMirroredTaskComplete, state: taskCompletionSource, TaskContinuationOptions.ExecuteSynchronously);
    }

    private static void OnMirroredTaskComplete<T>(Task<T> completedTask, object? state)
    {
        TrySetFromCompletedTask((TaskCompletionSource<T>)state!, completedTask);
    }

    public static Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken)
    {
        if (!cancellationToken.CanBeCanceled || task.IsCompleted)
            return task;

        var source = new TaskCompletionSource<T>();

        var registration = cancellationToken.Register(() => source.TrySetCanceled(cancellationToken));

        task.ContinueWith(
            OnInnerTaskComplete,
            state: (source, registration),
            cancellationToken,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Current);

        return source.Task;
    }

    private static void OnInnerTaskComplete<T>(Task<T> completedTask, object? state)
    {
        var (taskCompletionSource, registration) = ((TaskCompletionSource<T> source, CancellationTokenRegistration registration))state!;
        TrySetFromCompletedTask(taskCompletionSource, completedTask);
        _ = registration.DisposeAsync();
    }

    private static void TrySetFromCompletedTask<T>(TaskCompletionSource<T> taskCompletionSource, Task<T> completedTask)
    {
        switch (completedTask.Status)
        {
            case TaskStatus.RanToCompletion:
                taskCompletionSource.TrySetResult(completedTask.Result);
                break;
            case TaskStatus.Faulted:
                taskCompletionSource.TrySetException(completedTask.Exception!.InnerExceptions);
                break;
            case TaskStatus.Canceled:
                taskCompletionSource.TrySetCanceled();
                break;
            default:
                throw new ArgumentException("The task was not completed.", nameof(completedTask));
        }
    }
}
