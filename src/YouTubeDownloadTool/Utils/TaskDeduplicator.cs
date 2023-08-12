using System;
using System.Threading;
using System.Threading.Tasks;

namespace YouTubeDownloadTool;

internal sealed class TaskDeduplicator<T>
{
    private readonly Func<Task<T>> taskInvoker;
    private Task<T>? currentTask;

    public TaskDeduplicator(Func<Task<T>> taskInvoker)
    {
        this.taskInvoker = taskInvoker ?? throw new ArgumentNullException(nameof(taskInvoker));
    }

    public Task<T> StartOrJoin()
    {
        var lastSeenTask = Volatile.Read(ref currentTask);
        if (lastSeenTask is { IsCompleted: false }) return lastSeenTask;

        // By using this task instead of the task returned from taskInvoker, it is possible to make sure that racing
        // threads do not invoke overlapping calls to taskInvoker which is the whole point of this class.
        // The alternative of taking a lock would also require an extra task completion source to avoid calling user
        // code from within a lock.
        var source = new TaskCompletionSource<T>();

        while (true)
        {
            var taskToReplace = lastSeenTask;
            lastSeenTask = Interlocked.CompareExchange(ref currentTask, source.Task, taskToReplace);
            if (lastSeenTask == taskToReplace) break;

            if (lastSeenTask is not null)
            {
                // Return even if the new task is complete because it’s so close to having both just started and
                // just finished that it’s just as good. There’s no way to avoid returning a task that was not
                // completed when this method sees it but is completed by the time the calling method sees it, so we
                // had better be good and comfortable with such races.
                source.Task.Dispose();
                return lastSeenTask;
            }
        }

        var startedTask = TaskUtils.InvokeWithExceptionsWrapped(() => taskInvoker.Invoke());
        TaskUtils.MirrorExistingTask(source, startedTask);

        return source.Task;
    }
}
