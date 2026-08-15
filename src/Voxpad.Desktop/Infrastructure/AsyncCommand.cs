using System.Windows.Input;

namespace Voxpad.Desktop.Infrastructure;

public sealed class AsyncCommand : ICommand
{
    private readonly Func<Task> executeAsync;
    private bool isExecuting;

    public AsyncCommand(Func<Task> executeAsync)
    {
        this.executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => !isExecuting;

    public async void Execute(object? parameter)
    {
        if (isExecuting)
        {
            return;
        }

        try
        {
            isExecuting = true;
            RaiseCanExecuteChanged();
            await executeAsync();
        }
        finally
        {
            isExecuting = false;
            RaiseCanExecuteChanged();
        }
    }

    public void RaiseCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
