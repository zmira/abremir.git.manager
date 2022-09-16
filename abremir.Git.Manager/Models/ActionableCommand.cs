using Terminal.Gui;

namespace abremir.Git.Manager.Models
{
    internal record ActionableCommand(CommandType Type, Target Target, string Description, Key Shortcut, Action Action);
}
