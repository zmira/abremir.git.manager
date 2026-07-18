using Terminal.Gui.Input;

namespace abremir.GitManager.Models;

internal record struct ActionableCommand(CommandType Type, Target Target, string Description, Key Shortcut, Action Action);
