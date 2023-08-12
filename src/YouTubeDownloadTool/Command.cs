using System.Windows.Input;
using Techsola;

namespace YouTubeDownloadTool;

public sealed class Command : ICommand
{
    private readonly Action action;
    private bool canExecute = true;

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
        get => canExecute;
        set
        {
            if (canExecute == value) return;
            canExecute = value;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? CanExecuteChanged;

    bool ICommand.CanExecute(object? parameter) => canExecute;

    void ICommand.Execute(object? parameter) => action.Invoke();
}
