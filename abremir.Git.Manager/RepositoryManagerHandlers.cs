using System.Diagnostics;
using abremir.Git.Manager.Models;
using Terminal.Gui;
using Terminal.Gui.Trees;

namespace abremir.Git.Manager
{
    internal static class RepositoryManagerHandlers
    {
        internal static async Task<bool> LoadRepos(TreeView tree, string path)
        {
            RepositoryManager.LogInfo($"Load all repositories from {path} - Started");

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var repos = RepositoryLister.ListRepos(path);

            stopwatch.Stop();

            if (repos.Count == 0)
            {
                RepositoryManager.LogWarning($"Load repositories from {path} - No repositories found!");
                return false;
            }

            RepositoryManager.LogInfo($"Load repositories from {path} - Complete: {repos.Count} repositor{(repos.Count == 1 ? "y" : "ies")} in {stopwatch.Elapsed:hh\\:mm\\:ss\\.ffff}");

            tree.ClearObjects();
            tree.AddObjects(repos
                .ConvertAll(repo => new RepositoryNode(repo))
                .OrderBy(repo => repo.RepositoryName));

            await RetrieveStatusForAllRepositories(tree);

            return true;
        }

        internal static Task<TimeSpan> PullForSelectedRepository(TreeView tree, ITreeNode? repositoryTreeNode = null)
        {
            var selected = repositoryTreeNode ?? tree.SelectedObject;

            var repositoryNode = GetRepositoryNode(tree, selected);

            if (repositoryNode is null
                || repositoryNode.Status?.IsDirty == true
                || !repositoryNode.CurrentRepositoryHeadIsBehind
                || repositoryNode.HasError)
            {
                if (repositoryNode?.Status?.IsDirty == true)
                {
                    RepositoryManager.LogError($"Pull for {repositoryNode.RepositoryName} - Error: Repository is dirty");
                }

                return Task.FromResult(TimeSpan.Zero);
            }

            repositoryNode!.HasError = false;
            repositoryNode!.IsUpdating = true;

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            try
            {
                RepositoryActions.Pull(repositoryNode!.Repository);
            }
            catch (Exception ex)
            {
                repositoryNode!.HasError = true;
                repositoryNode!.ErrorMessage = GetTrimmedErrorMessage(ex.Message);
                RepositoryManager.LogError($"Pull for {repositoryNode.RepositoryName} - Error: {ex.Message}");
            }
            finally
            {
                stopwatch.Stop();
                repositoryNode!.IsUpdating = false;

                Application.MainLoop.Invoke(() => tree.RefreshObject(repositoryNode));
            }

            if (!repositoryNode!.HasError)
            {
                RepositoryManager.LogInfo($"Pull for {repositoryNode.RepositoryName} - Complete: {stopwatch.Elapsed:hh\\:mm\\:ss\\.ffff}");
            }

            return Task.FromResult(stopwatch.Elapsed);
        }

        internal static async Task PullForAllRepositories(TreeView tree)
        {
            RepositoryManager.LogInfo("Pull for all repositories - Started");

            var tasks = new List<Task>();

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            foreach (var repositoryNode in tree.Objects)
            {
                if (repositoryNode is null
                    || repositoryNode is not RepositoryNode)
                {
                    continue;
                }

                tasks.Add(Task.Run(() => PullForSelectedRepository(tree, repositoryNode)));
            }

            if (tasks.Count is 0)
            {
                return;
            }

            try
            {
                await Task.WhenAll(tasks);

                stopwatch.Stop();

                RepositoryManager.LogInfo($"Pull for all repositories - Complete: {tree.Objects.Count()} repositories in {stopwatch.Elapsed:hh\\:mm\\:ss\\.ffff}");
            }
            catch (Exception ex)
            {
                RepositoryManager.LogError($"Pull for all repositories - Error: {ex.Message}");
            }
        }

        internal static Task<TimeSpan> FetchForSelectedRepository(TreeView tree, ITreeNode? repositoryTreeNode = null)
        {
            var selected = repositoryTreeNode ?? tree.SelectedObject;

            var repositoryNode = GetRepositoryNode(tree, selected);

            if (repositoryNode is null)
            {
                return Task.FromResult(TimeSpan.Zero);
            }

            repositoryNode!.HasError = false;
            repositoryNode!.IsUpdating = true;

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            try
            {
                RepositoryActions.Fetch(repositoryNode!.Repository);
            }
            catch (Exception ex)
            {
                repositoryNode!.HasError = true;
                repositoryNode!.ErrorMessage = GetTrimmedErrorMessage(ex.Message);
                RepositoryManager.LogError($"Fetch for {repositoryNode.RepositoryName} - Error: {ex.Message}");
            }
            finally
            {
                stopwatch.Stop();
                repositoryNode!.IsUpdating = false;

                Application.MainLoop.Invoke(() => tree.RefreshObject(repositoryNode));
            }

            if (!repositoryNode!.HasError)
            {
                RepositoryManager.LogInfo($"Fetch for {repositoryNode.RepositoryName} - Complete: {stopwatch.Elapsed:hh\\:mm\\:ss\\.ffff}");
            }

            return Task.FromResult(stopwatch.Elapsed);
        }

        internal static async Task FetchForAllRepositories(TreeView tree)
        {
            RepositoryManager.LogInfo("Fetch for all repositories - Started");

            var tasks = new List<Task>();

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            foreach (var repositoryNode in tree.Objects)
            {
                if (repositoryNode is null
                    || repositoryNode is not RepositoryNode)
                {
                    continue;
                }

                tasks.Add(Task.Run(() => FetchForSelectedRepository(tree, repositoryNode)));
            }

            if (tasks.Count is 0)
            {
                return;
            }

            try
            {
                await Task.WhenAll(tasks);

                stopwatch.Stop();

                RepositoryManager.LogInfo($"Fetch for all repositories - Complete: {tree.Objects.Count()} repositories in {stopwatch.Elapsed:hh\\:mm\\:ss\\.ffff}");
            }
            catch (Exception ex)
            {
                RepositoryManager.LogError($"Fetch for all repositories - Error: {ex.Message}");
            }
        }

        internal static Task<TimeSpan> RetrieveStatusForSelectedRepository(TreeView tree, ITreeNode? repositoryTreeNode = null)
        {
            var selected = repositoryTreeNode ?? tree.SelectedObject;

            var repositoryNode = GetRepositoryNode(tree, selected);

            if (repositoryNode is null)
            {
                return Task.FromResult(TimeSpan.Zero);
            }

            repositoryNode!.HasError = false;
            repositoryNode!.IsUpdating = true;

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            try
            {
                repositoryNode.Status = RepositoryActions.RetrieveStatus(repositoryNode.Repository);
            }
            catch (Exception ex)
            {
                repositoryNode!.HasError = true;
                repositoryNode!.ErrorMessage = GetTrimmedErrorMessage(ex.Message);
                RepositoryManager.LogError($"Retrieve status for {repositoryNode.RepositoryName} - Error: {ex.Message}");
            }
            finally
            {
                stopwatch.Stop();
                repositoryNode!.IsUpdating = false;

                Application.MainLoop.Invoke(() => tree.RefreshObject(repositoryNode));
            }

            if (!repositoryNode!.HasError)
            {
                RepositoryManager.LogInfo($"Retrieve status for {repositoryNode.RepositoryName} - Completed: {stopwatch.Elapsed:hh\\:mm\\:ss\\.ffff}");
            }

            return Task.FromResult(stopwatch.Elapsed);
        }

        internal static async Task RetrieveStatusForAllRepositories(TreeView tree)
        {
            RepositoryManager.LogInfo("Retrieve status for all repositories - Started");

            var tasks = new List<Task>();

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            foreach (var repositoryNode in tree.Objects)
            {
                if (repositoryNode is null
                    || repositoryNode is not RepositoryNode)
                {
                    continue;
                }

                tasks.Add(Task.Run(() => RetrieveStatusForSelectedRepository(tree, repositoryNode)));
            }

            if (tasks.Count is 0)
            {
                return;
            }

            try
            {
                await Task.WhenAll(tasks);

                stopwatch.Stop();

                RepositoryManager.LogInfo($"Retrieve status for all repositories - Complete: {tree.Objects.Count()} repositories in {stopwatch.Elapsed:hh\\:mm\\:ss\\.ffff}");
            }
            catch (Exception ex)
            {
                RepositoryManager.LogError($"Retrieve status for all repositories - Error: {ex.Message}");
            }
        }

        internal static Task CheckoutSelectedBranch(TreeView tree)
        {
            var selectedNode = tree.SelectedObject;

            if (selectedNode is not BranchNode
                || !tree.IsExpanded(tree.GetParent(selectedNode))
                || (selectedNode as BranchNode)!.IsCurrentRepositoryHead)
            {
                if ((selectedNode as BranchNode)?.IsCurrentRepositoryHead == true)
                {
                    RepositoryManager.LogError($"Checkout branch {(selectedNode as BranchNode)!.FriendlyName} - Error: Branch already checked-out");
                }

                return Task.CompletedTask;
            }

            var repositoryNode = GetRepositoryNode(tree, selectedNode);

            if (repositoryNode is null)
            {
                return Task.CompletedTask;
            }

            repositoryNode!.HasError = false;

            var branch = repositoryNode.Repository.Branches.First(branch => branch.FriendlyName == (selectedNode as BranchNode)!.FriendlyName);

            try
            {
                LibGit2Sharp.Commands.Checkout(repositoryNode.Repository, branch);

                RepositoryManager.LogInfo($"Checkout branch {branch.FriendlyName} - Completed");
            }
            catch (Exception ex)
            {
                repositoryNode!.HasError = true;
                repositoryNode!.ErrorMessage = GetTrimmedErrorMessage(ex.Message);
                RepositoryManager.LogError($"Checkout branch {branch.FriendlyName} - Error: {ex.Message}");
            }
            finally
            {
                Application.MainLoop.Invoke(() => tree.RefreshObject(repositoryNode));
            }

            return Task.CompletedTask;
        }

        internal static Task DeleteSelectedBranch(TreeView tree)
        {
            var selectedNode = tree.SelectedObject;

            if (selectedNode is not BranchNode
                || !tree.IsExpanded(tree.GetParent(selectedNode))
                || (selectedNode as BranchNode)!.IsCurrentRepositoryHead)
            {
                if ((selectedNode as BranchNode)?.IsCurrentRepositoryHead == true)
                {
                    RepositoryManager.LogError($"Delete branch {(selectedNode as BranchNode)!.FriendlyName} - Error: Cannot delete branch while it is the repository HEAD");
                }

                return Task.CompletedTask;
            }

            var repositoryNode = GetRepositoryNode(tree, selectedNode);

            if (repositoryNode is null)
            {
                return Task.CompletedTask;
            }

            repositoryNode!.HasError = false;

            var branch = repositoryNode.Repository.Branches.First(branch => branch.FriendlyName == (selectedNode as BranchNode)!.FriendlyName);

            try
            {
                repositoryNode.Repository.Branches.Remove(branch);

                RepositoryManager.LogInfo($"Delete branch {branch.FriendlyName} - Completed");
            }
            catch (Exception ex)
            {
                repositoryNode!.HasError = true;
                repositoryNode!.ErrorMessage = GetTrimmedErrorMessage(ex.Message);
                RepositoryManager.LogError($"Delete branch {branch.FriendlyName} - Error: {ex.Message}");
            }
            finally
            {
                Application.MainLoop.Invoke(() => tree.RefreshObject(repositoryNode));
            }

            return Task.CompletedTask;
        }

        internal static Task ResetSelectedBranch(TreeView tree)
        {
            var selectedNode = tree.SelectedObject;

            if (selectedNode is not BranchNode
                || !tree.IsExpanded(tree.GetParent(selectedNode))
                || !(selectedNode as BranchNode)!.IsCurrentRepositoryHead)
            {
                return Task.CompletedTask;
            }

            var repositoryNode = GetRepositoryNode(tree, selectedNode);

            if (repositoryNode is null)
            {
                return Task.CompletedTask;
            }

            repositoryNode!.HasError = false;

            var branch = repositoryNode.Repository.Branches.First(branch => branch.FriendlyName == (selectedNode as BranchNode)!.FriendlyName);

            try
            {
                RepositoryActions.Reset(repositoryNode.Repository, branch.Tip);
                repositoryNode.Status = RepositoryActions.RetrieveStatus(repositoryNode.Repository);

                RepositoryManager.LogInfo($"Reset branch {branch.FriendlyName} - Completed");
            }
            catch (Exception ex)
            {
                repositoryNode!.HasError = true;
                repositoryNode!.ErrorMessage = GetTrimmedErrorMessage(ex.Message);
                RepositoryManager.LogError($"Reset branch {branch.FriendlyName} - Error: {ex.Message}");
            }
            finally
            {
                Application.MainLoop.Invoke(() => tree.RefreshObject(repositoryNode));
            }

            return Task.CompletedTask;
        }

        internal static void CopySelectedRepositoryPathToClipboard(TreeView tree)
        {
            var repositoryNode = GetRepositoryNode(tree, tree.SelectedObject);

            if (repositoryNode is null)
            {
                return;
            }

            Clipboard.TrySetClipboardData(Path.GetDirectoryName(repositoryNode.Repository.Info.WorkingDirectory)!);
        }

        private static string GetTrimmedErrorMessage(string errorMessage)
        {
            return errorMessage.Length <= 50
                ? errorMessage
                : errorMessage[..47] + "...";
        }

        private static RepositoryNode? GetRepositoryNode(TreeView tree, ITreeNode? treeNode)
        {
            if (treeNode is RepositoryNode)
            {
                return treeNode as RepositoryNode;
            }

            if (treeNode is not BranchNode
                || !tree.IsExpanded(tree.GetParent(treeNode)))
            {
                return null;
            }

            var parent = tree!.GetParent(treeNode);
            return parent as RepositoryNode;
        }
    }
}
