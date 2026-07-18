using abremir.Git.Manager.Models;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace abremir.Git.Manager;

internal class ProgressTracker : Dialog
{
    public bool Processing = true;

    private ProgressBar _progressBar;

    private ProgressTracker(CommandType commandType)
    {
        Title = RepositoryManager.ActionableCommands.Find(command => command.Type == commandType)!.Description;
        Width = Dim.Percent(50);
        Height = Dim.Auto();
        ShadowStyle = ShadowStyles.None;

        _progressBar = new ProgressBar
        {
            Width = Dim.Fill(),
            Height = 1,
            Fraction = 0f
        };

        Add(_progressBar);
    }

    public ProgressTracker(CommandType commandType, Action<ProgressBar> trackerAction) : this(commandType)
    {
        Initialized += (s, _) =>
        {
            KeyBindings.Remove(Key.Esc);

            if (trackerAction is not null)
            {
                trackerAction(_progressBar);
            }
            _progressBar.Fraction = 1f;
            Processing = false;
        };
    }

    public ProgressTracker(CommandType commandType, Func<ProgressBar, Task> trackerAction) : this(commandType)
    {
        Initialized += async (s, _) =>
        {
            KeyBindings.Remove(Key.Esc);

            if (trackerAction is not null)
            {
                await trackerAction(_progressBar);
            }
            _progressBar.Fraction = 1f;
            Processing = false;
        };
    }
}
