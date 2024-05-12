using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using abremir.Git.Manager.Models;
using Terminal.Gui;
using Terminal.Gui.Trees;

namespace abremir.Git.Manager
{
    internal class RepositoryManager : Toplevel
    {
        public static event EventHandler<EventArgs>? UiInitialized;

        private static Window? RepositoryWindow;
        private static TreeView? RepositoryTree;
        private static Window? LogsWindow;
        private static TextView? LogsView;
        private static readonly BlockingCollection<LogItem> Logs = new(new ConcurrentQueue<LogItem>());
        private static bool Processing;
        private static List<ActionableCommand> ActionableCommands = [];
        private static string? BasePath;
        private static Label? ProcessingLabel;
        private static Label? SpinnerLabel;
        private static ScrollBarView? TreeScrollBar;
        private static CheckBox? FilterByDirty;
        private static CheckBox? FilterByBehind;
        private static CheckBox? FilterByError;
        private static IEnumerable<ITreeNode> OriginalNodeList = [];

        private const string GitRepoManagerWindowTitle = "abremir.git.manager";

        public RepositoryManager()
        {
            LoadActionableCommands();

            ColorScheme = new ColorScheme
            {
                Focus = Application.Driver.MakeAttribute(Color.Black, Color.White),
                Normal = Application.Driver.MakeAttribute(Color.White, Color.Black),
                HotFocus = Application.Driver.MakeAttribute(Color.Black, Color.White),
                HotNormal = Application.Driver.MakeAttribute(Color.BrightGreen, Color.Black)
            };

            RepositoryWindow = new Window(GitRepoManagerWindowTitle)
            {
                Width = Dim.Fill(),
                Height = Dim.Fill(1),
                ColorScheme = ColorScheme
            };

            var filterFrame = new FrameView
            {
                Height = 1,
                Width = Dim.Fill(),
                ColorScheme = Colors.Menu
            };
            filterFrame.Border.BorderStyle = BorderStyle.None;
            filterFrame.Border.DrawMarginFrame = false;

            var filterLabel = new Label("Filter by:")
            {
                X = 2,
                ColorScheme = Colors.Menu
            };

            filterFrame.Add(filterLabel);

            FilterByDirty = new CheckBox("Dirty (*)")
            {
                X = Pos.Right(filterLabel) + 2,
                ColorScheme = Colors.Menu
            };

            filterFrame.Add(FilterByDirty);

            FilterByBehind = new CheckBox("Behind (↓)")
            {
                X = Pos.Right(FilterByDirty) + 2,
                ColorScheme = Colors.Menu
            };

            filterFrame.Add(FilterByBehind);

            FilterByError = new CheckBox("Error")
            {
                X = Pos.Right(FilterByBehind) + 2,
                ColorScheme = Colors.Menu
            };

            filterFrame.Add(FilterByError);

            RepositoryWindow.Add(filterFrame);

            var lineView = new LineView
            {
                Y = Pos.Bottom(filterFrame)
            };

            RepositoryWindow.Add(lineView);

            RepositoryTree = new TreeView
            {
                Y = Pos.Bottom(lineView),
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                DesiredCursorVisibility = CursorVisibility.Invisible,
                AllowLetterBasedNavigation = false
            };

            RepositoryTree.Style.LeaveLastRow = true;
            RepositoryTree.Style.ExpandableSymbol = '►';
            RepositoryTree.Style.CollapseableSymbol = '▼';

            var repositoryTreeContextMenu = new ContextMenu(RepositoryTree, new MenuBarItem());

            static MenuItem[] GetContextMenuItems()
            {
                var repositoryNode = RepositoryTree!.SelectedObject;
                var branchNodeIsSelected = repositoryNode is BranchNode && RepositoryTree.IsExpanded(RepositoryTree.GetParent(repositoryNode));
                var selectedNodeIsVisible = branchNodeIsSelected || repositoryNode is RepositoryNode;

                if (branchNodeIsSelected)
                {
                    repositoryNode = RepositoryTree.GetParent(repositoryNode);
                }

                var isDirty = selectedNodeIsVisible && (repositoryNode as RepositoryNode)!.Status?.IsDirty == true;

                return ActionableCommands.Where(
                        command => branchNodeIsSelected && command.Target.HasFlag(Target.BranchNode)).Select(
                            command => new MenuItem(null, command.Description, command.Action, shortcut: command.Shortcut))
                    .Concat(ActionableCommands.Where(
                        command => selectedNodeIsVisible && command.Target.HasFlag(Target.RepositoryNode) && command.Type is not CommandType.ViewChangesInSelectedRepository).Select(
                            command => new MenuItem(null, command.Description, command.Action, shortcut: command.Shortcut)))
                    .Concat(ActionableCommands.Where(
                        command => isDirty && command.Type is CommandType.ViewChangesInSelectedRepository).Select(
                            command => new MenuItem(null, command.Description, command.Action, shortcut: command.Shortcut)))
                    .Concat(ActionableCommands.Where(
                        command => command.Target.HasFlag(Target.RepositoryWindow)).Select(
                            command => new MenuItem(null, command.Description, command.Action, shortcut: command.Shortcut))).ToArray();
            }

            RepositoryTree.MouseClick += (e) =>
            {
                if (e.MouseEvent.Flags == repositoryTreeContextMenu.MouseFlags)
                {
                    repositoryTreeContextMenu = new ContextMenu(e.MouseEvent.X, e.MouseEvent.Y, new MenuBarItem(GetContextMenuItems()));
                    repositoryTreeContextMenu.Show();
                    e.Handled = true;
                }
            };

            RepositoryWindow.Add(RepositoryTree);

            var repositoryHeadColorScheme = new ColorScheme
            {
                Focus = Application.Driver.MakeAttribute(Color.BrightMagenta, RepositoryTree.ColorScheme.Focus.Background),
                Normal = Application.Driver.MakeAttribute(Color.BrightYellow, RepositoryTree.ColorScheme.Normal.Background)
            };

            RepositoryTree.ColorGetter = (node) => ((node as BranchNode)?.IsCurrentRepositoryHead ?? false) ? repositoryHeadColorScheme : null;

            TreeScrollBar = new ScrollBarView(RepositoryTree, true);

            TreeScrollBar.ChangedPosition += () =>
            {
                RepositoryTree.ScrollOffsetVertical = TreeScrollBar.Position;
                if (RepositoryTree.ScrollOffsetVertical != TreeScrollBar.Position)
                {
                    TreeScrollBar.Position = RepositoryTree.ScrollOffsetVertical;
                }
                RepositoryTree.SetNeedsDisplay();
            };

            RepositoryTree.DrawContent += _ =>
            {
                TreeScrollBar.Size = RepositoryTree.ContentHeight;
                TreeScrollBar.Position = RepositoryTree.ScrollOffsetVertical;
                TreeScrollBar.Refresh();
            };

            var processingLabelColorScheme = new ColorScheme
            {
                Normal = Application.Driver.MakeAttribute(Color.BrightGreen, Color.Black)
            };

            ProcessingLabel = new Label
            {
                AutoSize = true,
                Y = Pos.Top(RepositoryTree),
                Visible = false,
                ColorScheme = processingLabelColorScheme,
                Height = 1
            };

            SpinnerLabel = new Label
            {
                Visible = false,
                Y = ProcessingLabel.Y,
                X = Pos.Right(ProcessingLabel),
                AutoSize = false,
                Width = 1,
                Height = 1,
                ColorScheme = processingLabelColorScheme
            };

            RepositoryWindow.Add(ProcessingLabel);
            RepositoryWindow.Add(SpinnerLabel);

            Add(RepositoryWindow);

            var statusBarItems = new StatusItem[]
            {
                new StatusItem(Key.CtrlMask | Key.Q, "~^Q~ Quit", () => Application.RequestStop()),
                new StatusItem(Key.CtrlMask | Key.H, "~^H~ Show help", () => ShowHelp())
            };

            var statusBar = new StatusBar(statusBarItems);

            Add(statusBar);

            LogsWindow = new Window("logs")
            {
                Y = Pos.Bottom(RepositoryWindow),
                Width = Dim.Fill(),
                Height = 10,
                Visible = false,
                ColorScheme = ColorScheme
            };

            // TODO: revisit when https://github.com/gui-cs/Terminal.Gui/pull/1838 is merged
            LogsView = new TextView
            {
                ReadOnly = true,
                //DesiredCursorVisibility = CursorVisibility.Invisible, // TextView.MoveEnd() needs to "see" the cursor!
                Multiline = true,
                WordWrap = true,
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            LogsView.ContextMenu.MenuItems = new MenuBarItem(
                ActionableCommands.Where(
                    command => command.Target.HasFlag(Target.LogWindow)).Select(
                        command => new MenuItem(null, command.Description, command.Action, shortcut: command.Shortcut)).ToArray());

            LogsView.MouseClick += (e) =>
            {
                if (e.MouseEvent.Flags == LogsView.ContextMenu.MouseFlags)
                {
                    LogsView.ContextMenu.Position = new Point(e.MouseEvent.X, e.MouseEvent.Y + RepositoryWindow.Bounds.Height);
                    LogsView.ContextMenu.Show();
                    e.Handled = true;
                }
            };

            LogsWindow.Add(LogsView);

            Add(LogsWindow);

            var logsScrollBar = new ScrollBarView(LogsView, true);

            logsScrollBar.ChangedPosition += () =>
            {
                LogsView.TopRow = logsScrollBar.Position;
                if (LogsView.TopRow != logsScrollBar.Position)
                {
                    logsScrollBar.Position = LogsView.TopRow;
                }
                LogsWindow.SetNeedsDisplay();
            };

            LogsView.DrawContent += _ =>
            {
                logsScrollBar.Size = LogsView.Lines;
                logsScrollBar.Position = LogsView.TopRow;
                logsScrollBar.Refresh();
            };

            RepositoryWindow.KeyPress += (k) =>
            {
                var actionableCommand = ActionableCommands.Find(command => command.Shortcut == k.KeyEvent.Key
                    && (command.Target.HasFlag(Target.RepositoryNode)
                        || command.Target.HasFlag(Target.BranchNode)
                        || command.Target.HasFlag(Target.RepositoryWindow)));

                if (actionableCommand is null)
                {
                    return;
                }

                actionableCommand.Action();
                k.Handled = true;
            };

            LogsWindow.KeyPress += (k) =>
            {
                var actionableCommand = ActionableCommands.Find(command => command.Shortcut == k.KeyEvent.Key
                    && command.Target.HasFlag(Target.LogWindow));

                if (actionableCommand is null)
                {
                    return;
                }

                actionableCommand.Action();
                k.Handled = true;
            };

            FilterByDirty.Toggled += (_) => FilterRepositoryList();

            FilterByBehind.Toggled += (_) => FilterRepositoryList();

            FilterByError.Toggled += (_) => FilterRepositoryList();

            Task.Run(() => WriteLogs());

            OnInitialized(this);
        }

        private static void OnInitialized(RepositoryManager gitRepoManager)
        {
            UiInitialized?.Invoke(gitRepoManager, EventArgs.Empty);
        }

        public static async void LoadRepositories(string? path = null)
        {
            if (RepositoryWindow is null
                || RepositoryTree is null
                || FilterByDirty is null
                || FilterByBehind is null
                || FilterByError is null
                || (path is null && BasePath is null))
            {
                return;
            }

            if (path is not null && BasePath is null)
            {
                BasePath = path;
                RepositoryWindow.Title = GitRepoManagerWindowTitle + " - " + BasePath;
            }

            ResetLogWindow();

            StartProcessing(CommandType.LoadRepositories);

            if (!await RepositoryManagerHandlers.LoadRepos(RepositoryTree, BasePath!))
            {
                RepositoryTree.ClearObjects();

                ToggleLogWindowVisibility(true);
            }

            OriginalNodeList = RepositoryTree.Objects;

            if (FilterByDirty.Checked
                || FilterByBehind.Checked
                || FilterByError.Checked)
            {
                FilterRepositoryList();
            }

            RepositoryTree.SetFocus();

            EndProcessing();
        }

        internal static void LogInfo(string message)
        {
            Log(LogType.Information, message);
        }

        internal static void LogWarning(string message)
        {
            Log(LogType.Warning, message);
        }

        internal static void LogError(string message)
        {
            Log(LogType.Error, message);
        }

        private static void ResetLogWindow()
        {
            if (Processing
                || LogsView is null)
            {
                return;
            }

            while (Logs.Count > 0)
            {
                Logs.TryTake(out _);
            }

            LogsView.Text = string.Empty;
        }

        private static void WriteLogs()
        {
            if (LogsView is null
                || LogsWindow is null)
            {
                return;
            }

            foreach (var log in Logs.GetConsumingEnumerable())
            {
                // TODO: text color based on LogType  => revisit once https://github.com/gui-cs/Terminal.Gui/pull/1628 has been merged
                LogsView.Text += $"{log.Timestamp:yyyy\\-MM\\-dd HH\\:mm\\:ss\\.ffff} {log.Message}{Environment.NewLine}";
                LogsView.MoveEnd();

                if (LogsWindow.Visible)
                {
                    LogsView.SetNeedsDisplay();
                }
            }
        }

        private static void Log(LogType logType, string message)
        {
            if (Logs is null)
            {
                return;
            }

            Logs.Add(new(DateTimeOffset.Now, logType, message));
        }

        private static void ToggleLogWindowVisibility(bool? visible = null)
        {
            if (LogsWindow is null
                || RepositoryWindow is null
                || RepositoryTree is null)
            {
                return;
            }

            if (LogsWindow.Visible == visible)
            {
                return;
            }

            LogsWindow.Visible = visible ?? !LogsWindow.Visible;

            RepositoryWindow.Height = LogsWindow.Visible
                ? RepositoryWindow.Height - LogsWindow.Height
                : RepositoryWindow.Height + LogsWindow.Height;

            if (LogsWindow.Visible)
            {
                LogsWindow.SetFocus();
            }
            else
            {
                RepositoryTree.SetFocus();
            }
        }

        private static void ShowHelp()
        {
            StringBuilder aboutMessage = new();
            aboutMessage.AppendLine(string.Empty);

            ActionableCommands.ForEach(
                command => aboutMessage.AppendLine(
                    $"{ShortcutHelper.GetShortcutTag(command.Shortcut)} - {command.Description}".PadLeft(4).PadRight(60)));

            var border = new Border
            {
                Effect3D = false,
                BorderStyle = BorderStyle.Single
            };

            MessageBox.Query(aboutMessage.Length + 2, ActionableCommands.Count + 5, "Help", aboutMessage.ToString(), border: border, buttons: "_Close");
        }

        private static void CopySelectedRepositoryPathToClipboard()
        {
            if (RepositoryTree is null)
            {
                return;
            }

            RepositoryManagerHandlers.CopySelectedRepositoryPathToClipboard(RepositoryTree);
        }

        private static async void CheckoutSelectedBranch()
        {
            if (Processing
                || RepositoryTree is null)
            {
                return;
            }

            StartProcessing(CommandType.CheckoutSelectedBranch);

            await RepositoryManagerHandlers.CheckoutSelectedBranch(RepositoryTree);

            EndProcessing();
        }

        private static async void DeleteSelectedBranch()
        {
            if (Processing
                || RepositoryTree is null)
            {
                return;
            }

            StartProcessing(CommandType.DeleteSelectedBranch);

            await RepositoryManagerHandlers.DeleteSelectedBranch(RepositoryTree);

            EndProcessing();
        }

        private static void ToggleExpandSelectedRepositoryNode()
        {
            if (RepositoryTree is null)
            {
                return;
            }

            var repositoryNode = RepositoryTree.SelectedObject;

            if (repositoryNode is BranchNode)
            {
                repositoryNode = RepositoryTree.GetParent(repositoryNode);
            }

            if (RepositoryTree.IsExpanded(repositoryNode))
            {
                RepositoryTree.Collapse(repositoryNode);
            }
            else
            {
                RepositoryTree.Expand(repositoryNode);
            }
        }

        private static void ExpandAll()
        {
            if (RepositoryTree is null)
            {
                return;
            }

            RepositoryTree.SelectedObject = null;
            RepositoryTree.ExpandAll();
        }

        private static void CollapseAll()
        {
            if (RepositoryTree is null)
            {
                return;
            }

            RepositoryTree.SelectedObject = null;
            RepositoryTree.CollapseAll();
        }

        private static async void FetchForSelectedRepository()
        {
            if (Processing
                || RepositoryTree is null)
            {
                return;
            }

            StartProcessing(CommandType.FetchForSelectedRepository);

            await RepositoryManagerHandlers.FetchForSelectedRepository(RepositoryTree);

            EndProcessing();
        }

        private static async void FetchForAllRepositories()
        {
            if (Processing
                || RepositoryTree is null)
            {
                return;
            }

            StartProcessing(CommandType.FetchForAllRepositories);

            await RepositoryManagerHandlers.FetchForAllRepositories(RepositoryTree);

            EndProcessing();
        }

        private static async void PullForSelectedRepository()
        {
            if (Processing
                || RepositoryTree is null)
            {
                return;
            }

            StartProcessing(CommandType.PullForSelectedRepository);

            await RepositoryManagerHandlers.PullForSelectedRepository(RepositoryTree);

            if (FilterByBehind!.Checked)
            {
                FilterRepositoryList();
            }

            EndProcessing();
        }

        private static async void PullForAllRepositories()
        {
            if (Processing
                || RepositoryTree is null
                || FilterByBehind is null)
            {
                return;
            }

            StartProcessing(CommandType.PullForAllRepositories);

            await RepositoryManagerHandlers.PullForAllRepositories(RepositoryTree);

            if (FilterByBehind.Checked)
            {
                FilterRepositoryList();
            }

            EndProcessing();
        }

        private static async void ResetSelectedBranch()
        {
            if (Processing
                || RepositoryTree is null
                || FilterByDirty is null)
            {
                return;
            }

            StartProcessing(CommandType.ResetSelectedBranch);

            await RepositoryManagerHandlers.ResetSelectedBranch(RepositoryTree);

            if (FilterByDirty.Checked)
            {
                FilterRepositoryList();
            }

            EndProcessing();
        }

        private static async void RetrieveStatusForSelectedRepository()
        {
            if (Processing
                || RepositoryTree is null)
            {
                return;
            }

            StartProcessing(CommandType.RetrieveStatusForSelectedRepository);

            await RepositoryManagerHandlers.RetrieveStatusForSelectedRepository(RepositoryTree);

            EndProcessing();
        }

        private static async void RetrieveStatusForAllRepositories()
        {
            if (Processing
                || RepositoryTree is null)
            {
                return;
            }

            StartProcessing(CommandType.RetrieveStatusForAllRepositories);

            await RepositoryManagerHandlers.RetrieveStatusForAllRepositories(RepositoryTree);

            EndProcessing();
        }

        private static async void UpdateSelectedRepository()
        {
            if (Processing
                || RepositoryTree is null)
            {
                return;
            }

            StartProcessing(CommandType.UpdateSelectedRepository);

            await RepositoryManagerHandlers.FetchForSelectedRepository(RepositoryTree);
            await RepositoryManagerHandlers.PullForSelectedRepository(RepositoryTree);

            EndProcessing();
        }

        private static async void UpdateAllRepositories()
        {
            if (Processing
                || RepositoryTree is null)
            {
                return;
            }

            StartProcessing(CommandType.UpdateAllRepositories);

            ResetLogWindow();
            await RepositoryManagerHandlers.FetchForAllRepositories(RepositoryTree);
            await RepositoryManagerHandlers.PullForAllRepositories(RepositoryTree);

            EndProcessing();
        }

        private static void ViewChangesInSelectedRepository()
        {
            if (Processing
                || RepositoryTree is null
                || RepositoryWindow is null)
            {
                return;
            }

            StartProcessing(CommandType.ViewChangesInSelectedRepository);

            var repositoryNode = RepositoryTree.SelectedObject;

            if (repositoryNode is BranchNode)
            {
                repositoryNode = RepositoryTree.GetParent(repositoryNode);
            }

            var repository = (repositoryNode as RepositoryNode)!.Repository;

            var changedItems = RepositoryActions.GetChanges(repository);

            EndProcessing();

            Application.Run(new RepositoryChangesViewer((repositoryNode as RepositoryNode)!.RepositoryName, [.. changedItems.OrderBy(item => item.Path)]));
        }

        private static void ChangeBaseDirectory()
        {
            var directorySelection = new OpenDialog("Change base directory", "Select new base directory to search for git repositories", openMode: OpenDialog.OpenMode.Directory)
            {
                DirectoryPath = BasePath
            };

            Application.Run(directorySelection);

            if (!directorySelection.Canceled && !string.IsNullOrWhiteSpace(directorySelection.FilePath?.ToString()))
            {
                BasePath = null;

                LoadRepositories(directorySelection.FilePath.ToString());
            }
        }

        private static void FilterRepositoryList()
        {
            if (RepositoryTree is null
                || FilterByDirty is null
                || FilterByBehind is null
                || FilterByError is null)
            {
                return;
            }

            var objects = OriginalNodeList.Select(o => o as RepositoryNode);

            if (FilterByDirty.Checked)
            {
                objects = objects.Where(o => o?.Status?.IsDirty ?? false);
            }

            if (FilterByBehind.Checked)
            {
                objects = objects.Where(o => o?.CurrentRepositoryHeadIsBehind ?? false);
            }

            if (FilterByError.Checked)
            {
                objects = objects.Where(o => o?.HasError ?? false);
            }

            RepositoryTree.ClearObjects();
            RepositoryTree.AddObjects(objects);
        }

        private static void StartProcessing(CommandType commandType)
        {
            Processing = true;

            if (ProcessingLabel is not null
                && SpinnerLabel is not null
                && ProcessingLabel.SuperView.Frame.Width > 0)
            {
                var actionableCommand = ActionableCommands.Find(command => command.Type == commandType);

                Application.MainLoop.Invoke(() =>
                {
                    ProcessingLabel.Text = actionableCommand!.Description + " ";
                    ProcessingLabel.X = Pos.AnchorEnd(ProcessingLabel.Frame.Width + 2 + (TreeScrollBar?.Visible == true ? 1 : 0));
                    SpinnerLabel.Text = string.Empty;

                    ProcessingLabel.Visible = true;
                    SpinnerLabel.Visible = true;
                });

                Task.Run(() => RunSpinner());
            }
        }

        private static void RunSpinner()
        {
            if (SpinnerLabel is null)
            {
                return;
            }

            var spinnerCount = 0;
            var pattern = Kurukuru.Patterns.Line;

            while (Processing)
            {
                Application.MainLoop.Invoke(() => SpinnerLabel.Text = pattern.Frames[spinnerCount]);

                if (++spinnerCount > pattern.Frames.Length - 1)
                {
                    spinnerCount = 0;
                }

                Thread.Sleep(pattern.Interval);
            }
        }

        private static void EndProcessing()
        {
            if (ProcessingLabel is not null
                && SpinnerLabel is not null)
            {
                Application.MainLoop.Invoke(() =>
                {
                    ProcessingLabel.Visible = false;
                    SpinnerLabel.Visible = false;
                });
            }

            Processing = false;
        }

        private static void LoadActionableCommands()
        {
            ActionableCommands =
            [
                // branch specific commands
                new(CommandType.CheckoutSelectedBranch, Target.BranchNode, "Checkout selected branch", Key.c, () => CheckoutSelectedBranch()),
                new(CommandType.ResetSelectedBranch, Target.BranchNode, "Reset selected branch", Key.r, () => ResetSelectedBranch()),
                new(CommandType.DeleteSelectedBranch, Target.BranchNode, "Delete selected branch", Key.d, () => DeleteSelectedBranch()),

                // single repository commands
                new(CommandType.RetrieveStatusForSelectedRepository, Target.RepositoryNode, "Retrieve status for selected repository", Key.s, () => RetrieveStatusForSelectedRepository()),
                new(CommandType.FetchForSelectedRepository, Target.RepositoryNode, "Fetch for selected repository", Key.f, () => FetchForSelectedRepository()),
                new(CommandType.PullForSelectedRepository, Target.RepositoryNode, "Pull for selected repository", Key.p, () => PullForSelectedRepository()),
                new(CommandType.UpdateSelectedRepository, Target.RepositoryNode, "Update selected repository", Key.y, () => UpdateSelectedRepository()),
                new(CommandType.ViewChangesInSelectedRepository, Target.RepositoryNode, "View Changes in selected repository", Key.v, () => ViewChangesInSelectedRepository()),
                new(CommandType.ExpandAllNodes, Target.RepositoryNode, "Expand/collapse selected repository node", Key.e, () => ToggleExpandSelectedRepositoryNode()),

                // bulk repository commands
                new(CommandType.RetrieveStatusForAllRepositories, Target.RepositoryWindow, "Retrieve status for all repositories", Key.S, () => RetrieveStatusForAllRepositories()),
                new(CommandType.FetchForAllRepositories, Target.RepositoryWindow, "Fetch for all repositories", Key.F, () => FetchForAllRepositories()),
                new(CommandType.PullForAllRepositories, Target.RepositoryWindow, "Pull for all repositories", Key.P, () => PullForAllRepositories()),
                new(CommandType.UpdateAllRepositories, Target.RepositoryWindow, "Update all repositories", Key.Y, () => UpdateAllRepositories()),

                // generic commands
                new(CommandType.LoadRepositories, Target.RepositoryWindow, "Reload all repositories", Key.F5, () => LoadRepositories()),
                new(CommandType.CollapseAllNodes, Target.RepositoryWindow, "Expand all repository nodes", Key.CtrlMask | Key.E, () => ExpandAll()),
                new(CommandType.CollapseAllNodes, Target.RepositoryWindow, "Collapse all repository nodes", Key.CtrlMask | Key.ShiftMask | Key.E, () => CollapseAll()),
                new(CommandType.ToggleLogWindow, Target.RepositoryWindow | Target.LogWindow, "Toggle log window view", Key.CtrlMask | Key.L, () => ToggleLogWindowVisibility()),
                new(CommandType.ResetLogWindow, Target.RepositoryWindow | Target.LogWindow, "Reset Log Window", Key.CtrlMask | Key.J, () => ResetLogWindow()),
                new(CommandType.ChangeBaseDirectory, Target.RepositoryWindow | Target.LogWindow, "Change base directory", Key.CtrlMask | Key.O, () => ChangeBaseDirectory()),
                new(CommandType.ShowHelp, Target.RepositoryWindow | Target.LogWindow, "Show help", Key.CtrlMask | Key.H, () => ShowHelp())
            ];

            var key = Key.CtrlMask | (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? Key.C : Key.Y);
            ActionableCommands.Insert(0, new(CommandType.CopyPathToClipboard, Target.RepositoryNode, "Copy repository path to clipboard", key, () => CopySelectedRepositoryPathToClipboard()));
        }
    }
}
