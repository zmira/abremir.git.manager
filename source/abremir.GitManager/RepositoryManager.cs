using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using System.Text;
using abremir.GitManager.Models;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace abremir.GitManager;

internal partial class RepositoryManager : Window
{
    public static event EventHandler<EventArgs>? UiInitialized;

    private readonly FrameView _repositoryWindow;
    private readonly TreeView _repositoryTree;
    private readonly ScrollableCode _logWindow;
    private readonly ConcurrentQueue<LogItem> _logQueue = [];
    private bool _processing;
    private string? _basePath;
    private readonly CheckBox _filterByDirty;
    private readonly CheckBox _filterByBehind;
    private readonly CheckBox _filterByError;
    private IEnumerable<ITreeNode> _originalNodeList = [];

    private const string GitRepoManagerWindowTitle = "abremir.GitManager";

    internal static List<ActionableCommand> ActionableCommands = [];

    public RepositoryManager()
    {
        LoadActionableCommands();

        _repositoryWindow = new()
        {
            Title = GitRepoManagerWindowTitle,
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            BorderStyle = LineStyle.Rounded,
        };

        var filterFrame = new FrameView
        {
            Height = 1,
            Width = Dim.Fill(),
            BorderStyle = LineStyle.None,
        };
        filterFrame.Margin.Thickness = Thickness.Empty;

        var filterLabel = new Label
        {
            Text = "Filter by:",
            X = 2,
        };

        filterFrame.Add(filterLabel);

        _filterByDirty = new CheckBox
        {
            Text = "Dirty (*)",
            X = Pos.Right(filterLabel) + 2,
        };

        filterFrame.Add(_filterByDirty);

        _filterByBehind = new CheckBox
        {
            Text = "Behind (↓)",
            X = Pos.Right(_filterByDirty) + 2,
        };

        filterFrame.Add(_filterByBehind);

        _filterByError = new CheckBox
        {
            Text = "Error",
            X = Pos.Right(_filterByBehind) + 2,
        };

        filterFrame.Add(_filterByError);

        _repositoryWindow.Add(filterFrame);

        var line = new Line
        {
            Y = Pos.Bottom(filterFrame)
        };

        _repositoryWindow.Add(line);

        _repositoryTree = new TreeView
        {
            Y = Pos.Bottom(line),
            Width = Dim.Fill(),
            Height = Dim.Fill(),
            AllowLetterBasedNavigation = false,
        };
        _repositoryTree.ViewportSettings |= ViewportSettingsFlags.HasScrollBars;
        _repositoryTree.KeyBindings.Remove(Key.CursorRight);
        _repositoryTree.KeyBindings.Remove(Key.CursorLeft);
        _repositoryTree.KeyBindings.Remove(Key.CursorRight.WithCtrl);
        _repositoryTree.KeyBindings.Remove(Key.CursorLeft.WithCtrl);

        _repositoryTree.Style.ExpandableSymbol = new Rune('►');
        _repositoryTree.Style.CollapseableSymbol = new Rune('▼');

        var repositoryTreeContextMenu = new PopoverMenu([]);

        MenuItem[] GetContextMenuItems()
        {
            var repositoryNode = _repositoryTree!.SelectedObject;
            var branchNodeIsSelected = repositoryNode is BranchNode && _repositoryTree.IsExpanded(_repositoryTree.GetParent(repositoryNode));
            var selectedNodeIsVisible = branchNodeIsSelected || repositoryNode is RepositoryNode;

            if (branchNodeIsSelected)
            {
                repositoryNode = _repositoryTree.GetParent(repositoryNode);
            }

            var isDirty = selectedNodeIsVisible && (repositoryNode as RepositoryNode)!.Status?.IsDirty == true;

            return
            [
                .. ActionableCommands.Where(
                    command => branchNodeIsSelected && command.Target.HasFlag(Target.BranchNode)).Select(
                        command => new MenuItem(command.Description, null, command.Action, command.Shortcut)),
                .. ActionableCommands.Where(
                    command => selectedNodeIsVisible && command.Target.HasFlag(Target.RepositoryNode) && command.Type is not CommandType.ViewChangesInSelectedRepository).Select(
                        command => new MenuItem(command.Description, null, command.Action, command.Shortcut)),
                .. ActionableCommands.Where(
                    command => isDirty && command.Type is CommandType.ViewChangesInSelectedRepository).Select(
                        command => new MenuItem(command.Description, null, command.Action, command.Shortcut)),
                .. ActionableCommands.Where(
                    command => command.Target.HasFlag(Target.RepositoryWindow)).Select(
                        command => new MenuItem(command.Description, null, command.Action, command.Shortcut)),
            ];
        }

        _repositoryTree.MouseEvent += (_, e) =>
        {
            if (e.Flags.HasFlag(MouseFlags.RightButtonPressed))
            {
                repositoryTreeContextMenu.Root = new Menu(GetContextMenuItems());
                repositoryTreeContextMenu.MakeVisible(e.ScreenPosition);
                e.Handled = true;
            }
        };

        _repositoryWindow.Add(_repositoryTree);

        Add(_repositoryWindow);

        var statusBarItems = new Shortcut[]
        {
            new(Key.Q.WithAlt, "_Quit", () => App!.RequestStop()),
            new(Key.H.WithAlt, "_Help", () => ShowHelp())
        };

        var statusBar = new StatusBar(statusBarItems);

        Add(statusBar);

        _logWindow = new()
        {
            Title = "logs",
            Y = Pos.Bottom(_repositoryWindow),
            Width = Dim.Fill(),
            Height = 10,
            Visible = false,
            BorderStyle = LineStyle.Rounded,
            SyntaxHighlighter = null,
        };

        var logsViewContextMenu = new PopoverMenu([
            .. ActionableCommands.Where(
                command => command.Target.HasFlag(Target.LogWindow)).Select(
                    command => new MenuItem(command.Description, null, command.Action, command.Shortcut))]);

        _logWindow.MouseEvent += (_, e) =>
        {
            if (e.Flags.HasFlag(MouseFlags.RightButtonPressed))
            {
                logsViewContextMenu.MakeVisible(e.ScreenPosition);
                e.Handled = true;
            }
        };

        Add(_logWindow);

        _repositoryWindow.KeyDown += (_, k) =>
        {
            Action? action = ActionableCommands.Find(command => command.Shortcut == k
                && (command.Target.HasFlag(Target.RepositoryNode)
                    || command.Target.HasFlag(Target.BranchNode)
                    || command.Target.HasFlag(Target.RepositoryWindow))).Action;

            action?.Invoke();

            k.Handled = action is not null;
        };

        _logWindow.KeyDown += (_, k) =>
        {
            Action? action = ActionableCommands.Find(command => command.Shortcut == k
                && command.Target.HasFlag(Target.LogWindow)).Action;
            action?.Invoke();

            k.Handled = action is not null;
        };

        _filterByDirty.Activated += (_, _) => FilterRepositoryList();

        _filterByBehind.Activated += (_, _) => FilterRepositoryList();

        _filterByError.Activated += (_, _) => FilterRepositoryList();

        Initialized += (s, _) =>
        {
            (s as View)?.App!.Popovers?.Register(repositoryTreeContextMenu);
            (s as View)?.App!.Popovers?.Register(logsViewContextMenu);

            App!.AddTimeout(TimeSpan.FromMilliseconds(100), () =>
            {
                if (!_logQueue.IsEmpty)
                {
                    string logs = string.Empty;
                    while (_logQueue.TryDequeue(out var log))
                    {
                        // TODO: text color based on LogType  => revisit once https://github.com/gui-cs/Terminal.Gui/pull/1628 has been merged
                        logs += $"{log.Timestamp:yyyy\\-MM\\-dd HH\\:mm\\:ss\\.ffff} {log.Message}{Environment.NewLine}";
                    }

                    _logWindow.Text += logs;
                    _logWindow.VerticalScrollBar.Value = _logWindow.VerticalScrollBar.ScrollableContentSize;

                    if (_logWindow.Visible)
                    {
                        _logWindow.SetNeedsDraw();
                    }
                }

                return true;
            });

            UiInitialized?.Invoke(s, EventArgs.Empty);
        };
    }

    public void LoadRepositories(string? path = null, bool getStatus = true)
    {
        if (path is null && _basePath is null)
        {
            return;
        }

        if (path is not null && _basePath is null)
        {
            _basePath = path;
            _repositoryWindow.Title = GitRepoManagerWindowTitle + " - " + _basePath;
        }

        int repositoryCount = 0;
        ResetLogWindow();

        WrapProgress(CommandType.LoadRepositories, progress => repositoryCount = LoadRepositoriesHandler(_basePath!, progress));

        if (repositoryCount is 0)
        {
            ToggleLogWindowVisibility(true);
        }
        else
        {
            if (getStatus)
            {
                RetrieveStatusForAllRepositories();
            }
        }

        _originalNodeList = _repositoryTree.Objects ?? [];

        UpdateFilterStatus();

        FilterRepositoryList();

        _repositoryTree.SetFocus();

    }

    private void LogInfo(string message) =>
        Log(LogType.Information, message);

    private void LogWarning(string message) =>
        Log(LogType.Warning, message);

    private void LogError(string message) =>
        Log(LogType.Error, message);

    private void ResetLogWindow()
    {
        _logQueue.Clear();
        _logWindow.Text = string.Empty;
    }

    private void Log(LogType logType, string message) =>
        _logQueue.Enqueue(new(DateTimeOffset.Now, logType, message));

    private void ToggleLogWindowVisibility(bool? visible = null)
    {
        if (_logWindow.Visible == visible)
        {
            return;
        }

        _logWindow.Visible = visible ?? !_logWindow.Visible;

        _repositoryWindow.Height = _logWindow.Visible
            ? _repositoryWindow.Height - _logWindow.Height
            : _repositoryWindow.Height + _logWindow.Height;

        if (_logWindow.Visible)
        {
            _logWindow.SetFocus();
        }
        else
        {
            _repositoryTree.SetFocus();
        }
    }

    private void ShowHelp()
    {
        StringBuilder aboutMessage = new();
        aboutMessage.AppendLine(string.Empty);

        ActionableCommands.ForEach(
            command => aboutMessage.AppendLine(
                $"{command.Shortcut} - {command.Description}".PadLeft(4).PadRight(60)));

        MessageBox.Query(App!, "Help", aboutMessage.ToString(), buttons: "_Close");
    }

    private void CopySelectedRepositoryPathToClipboard() =>
        CopySelectedRepositoryPathToClipboardHandler();

    private void CheckoutSelectedBranch() =>
        WrapProgress(CommandType.CheckoutSelectedBranch, CheckoutSelectedBranchHandler);

    private void DeleteSelectedBranch() =>
        WrapProgress(CommandType.DeleteSelectedBranch, DeleteSelectedBranchHandler);

    private void ToggleExpandSelectedRepositoryNode()
    {
        var repositoryNode = _repositoryTree.SelectedObject;

        if (repositoryNode is BranchNode)
        {
            repositoryNode = _repositoryTree.GetParent(repositoryNode);
        }

        if (_repositoryTree.IsExpanded(repositoryNode))
        {
            _repositoryTree.Collapse(repositoryNode);
        }
        else
        {
            _repositoryTree.Expand(repositoryNode);
        }
    }

    private void ExpandAll()
    {
        _repositoryTree.SelectedObject = null;
        _repositoryTree.ExpandAll();
    }

    private void CollapseAll()
    {
        _repositoryTree.SelectedObject = null;
        _repositoryTree.CollapseAll();
    }

    private void FetchForSelectedRepository() =>
        WrapProgress(CommandType.FetchForSelectedRepository, async progress => await FetchForSelectedRepositoryHandler(progress: progress));

    private void FetchForAllRepositories() =>
        WrapProgress(CommandType.FetchForAllRepositories, async progress => await FetchForAllRepositoriesHandler(progress));

    private void PullForSelectedRepository() =>
        WrapProgress(CommandType.PullForSelectedRepository, async progress => await PullForSelectedRepositoryHandler(progress: progress, filter: FilterRepositoryList));

    private void PullForAllRepositories() =>
        WrapProgress(CommandType.PullForAllRepositories, async progress => await PullForAllRepositoriesHandler(progress, FilterRepositoryList));

    private void ResetSelectedBranch() =>
        WrapProgress(CommandType.ResetSelectedBranch, async progress => await ResetSelectedBranchHandler(progress, FilterRepositoryList));

    private void RetrieveStatusForSelectedRepository() =>
        WrapProgress(CommandType.RetrieveStatusForSelectedRepository, async progress => await RetrieveStatusForSelectedRepositoryHandler(progress: progress));

    private void RetrieveStatusForAllRepositories() =>
        WrapProgress(CommandType.RetrieveStatusForAllRepositories, RetrieveStatusForAllRepositoriesHandler);

    private void UpdateSelectedRepository() =>
        WrapProgress(CommandType.UpdateSelectedRepository, async progress =>
        {
            await FetchForSelectedRepositoryHandler(progress: progress, split: true);
            await PullForSelectedRepositoryHandler(progress: progress, split: true);
            progress.Fraction = 1f;
        });

    private void UpdateAllRepositories() =>
        WrapProgress(CommandType.UpdateAllRepositories, async progress =>
        {
            await FetchForAllRepositoriesHandler(progress, split: true);
            await PullForAllRepositoriesHandler(progress, split: true);
            progress.Fraction = 1f;
        });

    private void ViewChangesInSelectedRepository()
    {
        ObservableCollection<ChangedItem> changedItems = [];
        var repositoryNode = _repositoryTree.SelectedObject;

        WrapProgress(CommandType.ViewChangesInSelectedRepository, (progress) =>
        {
            progress.Fraction = 0.25f;
            if (repositoryNode is BranchNode)
            {
                repositoryNode = _repositoryTree.GetParent(repositoryNode);
            }

            progress.Fraction = 0.5f;
            var repository = (repositoryNode as RepositoryNode)!.Repository;

            progress.Fraction = 0.75f;
            changedItems = [.. RepositoryActions.GetChanges(repository).OrderBy(item => item.Path)];

            progress.Fraction = 1f;
        });

        App!.Run(new RepositoryChangesViewer((repositoryNode as RepositoryNode)!.RepositoryName, changedItems));
    }

    private void WrapProgress(CommandType commandType, Action<ProgressBar> trackerAction)
    {
        if (_processing)
        {
            return;
        }

        _processing = true;

        var tracker = new ProgressTracker(commandType, trackerAction);

        var timeout = App!.AddTimeout(TimeSpan.FromMilliseconds(100), () =>
        {
            if (!tracker.Processing)
            {
                App!.Invoke((app) => app.RequestStop(tracker));
            }
            return tracker.Processing;
        });

        App!.Run(tracker);

        tracker.Dispose();

        UpdateFilterStatus();

        _processing = false;
    }

    private void WrapProgress(CommandType commandType, Func<ProgressBar, Task> trackerAction)
    {
        if (_processing)
        {
            return;
        }

        _processing = true;

        var tracker = new ProgressTracker(commandType, trackerAction);

        var timeout = App!.AddTimeout(TimeSpan.FromMilliseconds(100), () =>
        {
            if (!tracker.Processing)
            {
                App!.Invoke((app) => app.RequestStop(tracker));
            }
            return tracker.Processing;
        });

        App!.Run(tracker);

        tracker.Dispose();

        UpdateFilterStatus();

        _processing = false;
    }

    private void ChangeBaseDirectory()
    {
        if (_processing)
        {
            return;
        }

        var directorySelection = new OpenDialog
        {
            Title = "Change base directory",
            Text = "Select new base directory to search for git repositories",
            OpenMode = OpenMode.Directory,
            Path = _basePath ?? string.Empty,
            ShadowStyle = ShadowStyles.None,
        };

        App!.Run(directorySelection);

        directorySelection.Disposing += async (s, _) =>
        {
            var dialog = (OpenDialog?)s;
            if (dialog?.Canceled is not true && !string.IsNullOrWhiteSpace(dialog?.Path ?? string.Empty))
            {
                _basePath = null;

                LoadRepositories(dialog!.Path);
            }
        };

        directorySelection.Dispose();
    }

    private void FilterRepositoryList()
    {
        IEnumerable<RepositoryNode> objects = _originalNodeList.Select(o => o as RepositoryNode).Where(o => o is not null)!;

        if (_filterByDirty.Enabled && _filterByDirty.Value is CheckState.Checked)
        {
            objects = objects.Where(o => o?.Status?.IsDirty ?? false);
        }

        if (_filterByBehind.Enabled && _filterByBehind.Value is CheckState.Checked)
        {
            objects = objects.Where(o => o?.CurrentRepositoryHeadIsBehind ?? false);
        }

        if (_filterByError.Enabled && _filterByError.Value is CheckState.Checked)
        {
            objects = objects.Where(o => o?.HasError ?? false);
        }

        if (objects.Count() != (_repositoryTree.Objects?.Count() ?? 0))
        {
            _repositoryTree.SelectedObject = null;
            _repositoryTree.ClearObjects();
            _repositoryTree.AddObjects(objects);
        }
    }

    private void UpdateFilterStatus()
    {
        _filterByDirty.Enabled = _originalNodeList.Any(o => (o as RepositoryNode)?.Status?.IsDirty ?? false);
        _filterByBehind.Enabled = _originalNodeList.Any(o => (o as RepositoryNode)?.CurrentRepositoryHeadIsBehind ?? false);
        _filterByError.Enabled = _originalNodeList.Any(o => (o as RepositoryNode)?.HasError ?? false);
    }

    private void LoadActionableCommands()
    {
        ActionableCommands =
        [
            // branch specific commands
            new(CommandType.CheckoutSelectedBranch, Target.BranchNode, "Checkout selected branch", Key.C, CheckoutSelectedBranch),
            new(CommandType.ResetSelectedBranch, Target.BranchNode, "Reset selected branch", Key.R, ResetSelectedBranch),
            new(CommandType.DeleteSelectedBranch, Target.BranchNode, "Delete selected branch", Key.D, DeleteSelectedBranch),

            // single repository commands
            new(CommandType.RetrieveStatusForSelectedRepository, Target.RepositoryNode, "Retrieve status for selected repository", Key.S, RetrieveStatusForSelectedRepository),
            new(CommandType.FetchForSelectedRepository, Target.RepositoryNode, "Fetch for selected repository", Key.F, FetchForSelectedRepository),
            new(CommandType.PullForSelectedRepository, Target.RepositoryNode, "Pull for selected repository", Key.P, PullForSelectedRepository),
            new(CommandType.UpdateSelectedRepository, Target.RepositoryNode, "Update selected repository", Key.Y, UpdateSelectedRepository),
            new(CommandType.ViewChangesInSelectedRepository, Target.RepositoryNode, "View Changes in selected repository", Key.V, ViewChangesInSelectedRepository),
            new(CommandType.ExpandAllNodes, Target.RepositoryNode, "Expand selected repository node", Key.CursorRight, ToggleExpandSelectedRepositoryNode),
            new(CommandType.ExpandAllNodes, Target.RepositoryNode, "Collapse selected repository node", Key.CursorLeft, ToggleExpandSelectedRepositoryNode),

            // bulk repository commands
            new(CommandType.RetrieveStatusForAllRepositories, Target.RepositoryWindow, "Retrieve status for all repositories", Key.S.WithShift, RetrieveStatusForAllRepositories),
            new(CommandType.FetchForAllRepositories, Target.RepositoryWindow, "Fetch for all repositories", Key.F.WithShift, FetchForAllRepositories),
            new(CommandType.PullForAllRepositories, Target.RepositoryWindow, "Pull for all repositories", Key.P.WithShift, PullForAllRepositories),
            new(CommandType.UpdateAllRepositories, Target.RepositoryWindow, "Update all repositories", Key.Y.WithShift, UpdateAllRepositories),

            // generic commands
            new(CommandType.LoadRepositories, Target.RepositoryWindow, "Reload all repositories", Key.F5, () => LoadRepositories()),
            new(CommandType.CollapseAllNodes, Target.RepositoryWindow, "Expand all repository nodes", Key.CursorRight.WithCtrl, ExpandAll),
            new(CommandType.CollapseAllNodes, Target.RepositoryWindow, "Collapse all repository nodes", Key.CursorLeft.WithCtrl, CollapseAll),
            new(CommandType.ToggleLogWindow, Target.RepositoryWindow | Target.LogWindow, "Toggle log window view", Key.L.WithCtrl, () => ToggleLogWindowVisibility()),
            new(CommandType.ResetLogWindow, Target.RepositoryWindow | Target.LogWindow, "Reset Log Window", Key.K.WithCtrl, ResetLogWindow),
            new(CommandType.ChangeBaseDirectory, Target.RepositoryWindow | Target.LogWindow, "Change base directory", Key.O.WithCtrl, ChangeBaseDirectory),
            new(CommandType.ShowHelp, Target.RepositoryWindow | Target.LogWindow, "Show help", Key.H.WithAlt, ShowHelp)
        ];

        var key = (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? Key.C : Key.Y).WithCtrl;
        ActionableCommands.Insert(0, new(CommandType.CopyPathToClipboard, Target.RepositoryNode, "Copy repository path to clipboard", key, CopySelectedRepositoryPathToClipboard));
    }
}
