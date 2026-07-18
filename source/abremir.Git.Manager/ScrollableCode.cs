using Terminal.Gui.Input;
using Terminal.Gui.Views;

namespace abremir.Git.Manager;

internal class ScrollableCode : Code
{
    public ScrollableCode()
    {
        AddCommand(Command.ScrollDown, () => ScrollVertical(1));
        AddCommand(Command.ScrollUp, () => ScrollVertical(-1));
        AddCommand(Command.ScrollLeft, () => ScrollHorizontal(-1));
        AddCommand(Command.ScrollRight, () => ScrollHorizontal(1));

        MouseBindings.Add(MouseFlags.WheeledUp, Command.ScrollUp);
        MouseBindings.Add(MouseFlags.WheeledDown, Command.ScrollDown);
        MouseBindings.Add(MouseFlags.WheeledLeft, Command.ScrollLeft);
        MouseBindings.Add(MouseFlags.WheeledRight, Command.ScrollRight);

        KeyBindings.Add(Key.CursorDown, Command.ScrollDown);
        KeyBindings.Add(Key.CursorUp, Command.ScrollUp);
        KeyBindings.Add(Key.CursorLeft, Command.ScrollLeft);
        KeyBindings.Add(Key.CursorRight, Command.ScrollRight);
    }
}
