using Terminal.Gui.Input;

namespace abremir.Git.Manager.Models;

internal record struct ActionableCommand(CommandType Type, Target Target, string Description, Key Shortcut, Action Action);
