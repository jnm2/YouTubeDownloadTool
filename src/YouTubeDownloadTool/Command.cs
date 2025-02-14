using System.Windows.Input;
using Techsola;

namespace YouTubeDownloadTool;

public sealed class Command : ICommand
{
    private readonly Action action;

    public Command(Action action)
    {
        this.action = action;
    }

    public Command(Func<Task> asyncAction)
    {
        action = () => AmbientTasks.Add(asyncAction);
    }

    public bool CanExecute
    {
        get;
        set
        {
            if (field == value) return;
            field = value;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    } = true;

    public event EventHandler? CanExecuteChanged;

    bool ICommand.CanExecute(object? parameter) => CanExecute;

    void ICommand.Execute(object? parameter) => action.Invoke();
}
