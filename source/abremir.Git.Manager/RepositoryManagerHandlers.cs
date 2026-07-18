using System.Diagnostics;
using abremir.Git.Manager.Models;
using Terminal.Gui.Views;

namespace abremir.Git.Manager;

internal partial class RepositoryManager
{
    private int LoadRepositoriesHandler(string path, ProgressBar progress)
    {
        progress.Fraction = 0.25f;

        _repositoryTree.ClearObjects();

        LogInfo($"Load all repositories from {path} - Started");

        progress.Fraction = 0.5f;

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        var repos = RepositoryLister.ListRepos(path)
            .ConvertAll(repo => new RepositoryNode(repo))
            .OrderBy(repo => repo.RepositoryName)
            .ToList();

        stopwatch.Stop();

        progress.Fraction = 0.75f;

        LogInfo($"Load repositories from {path} - Complete: {repos.Count} repositor{(repos.Count == 1 ? "y" : "ies")} in {stopwatch.Elapsed:hh\\:mm\\:ss\\.ffff}");

        _repositoryTree.AddObjects(repos);

        progress.Fraction = 1f;

        return repos.Count;
    }

    private Task<TimeSpan> PullForSelectedRepositoryHandler(ITreeNode? repositoryTreeNode = null, ProgressBar? progress = null, Action? filter = null, bool? split = false)
    {
        float fraction = 1f / (filter is not null ? 4 : 3) / (split is true ? 2 : 1);
        progress?.Fraction += fraction;

        var selected = repositoryTreeNode ?? _repositoryTree.SelectedObject;

        var repositoryNode = GetRepositoryNode(selected);

        if (repositoryNode is null
            || repositoryNode.Status?.IsDirty == true
            || !repositoryNode.CurrentRepositoryHeadIsBehind
            || repositoryNode.HasError)
        {
            if (repositoryNode?.Status?.IsDirty == true)
            {
                LogError($"Pull for {repositoryNode.RepositoryName} - Error: Repository is dirty");
            }

            if (split is false)
            {
                progress?.Fraction = 1f;
            }

            return Task.FromResult(TimeSpan.Zero);
        }

        progress?.Fraction += fraction;

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
            LogError($"Pull for {repositoryNode.RepositoryName} - Error: {ex.Message}");
        }
        finally
        {
            stopwatch.Stop();
            repositoryNode!.IsUpdating = false;

            progress?.Fraction += fraction;

            _repositoryTree.RefreshObject(repositoryNode);
        }

        if (!repositoryNode!.HasError)
        {
            LogInfo($"Pull for {repositoryNode.RepositoryName} - Complete: {stopwatch.Elapsed:hh\\:mm\\:ss\\.ffff}");
        }

        if (filter is not null)
        {
            progress?.Fraction += fraction;

            filter();
        }

        if (split is false)
        {
            progress?.Fraction = 1f;
        }

        return Task.FromResult(stopwatch.Elapsed);
    }

    private async Task PullForAllRepositoriesHandler(ProgressBar progress, Action? filter = null, bool? split = false)
    {
        float fraction = 0.2f / (filter is not null ? 3 : 2) / (split is true ? 2 : 1);
        progress.Fraction += fraction;

        LogInfo("Pull for all repositories - Started");

        List<Task> tasks = [];

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        foreach (var repositoryNode in _repositoryTree.Objects ?? [])
        {
            if (repositoryNode is null
                || repositoryNode is not RepositoryNode)
            {
                continue;
            }

            tasks.Add(Task.Run(async () => await PullForSelectedRepositoryHandler(repositoryNode)));
        }

        progress.Fraction += fraction;

        if (tasks.Count is 0)
        {
            LogInfo("Pull for all repositories - Skipped");

            if (split is false)
            {
                progress.Fraction = 1f;
            }

            return;
        }

        float fraction2 = ((1f / (split is true ? 2 : 1)) - (fraction * 2) - (filter is not null ? fraction : 0f)) / tasks.Count;

        try
        {
            await foreach (Task<TimeSpan> completedTask in Task.WhenEach(tasks))
            {
                _ = await completedTask;
                progress.Fraction += fraction2;
            }

            stopwatch.Stop();

            LogInfo($"Pull for all repositories - Complete: {_repositoryTree.Objects?.Count() ?? 0} repositories in {stopwatch.Elapsed:hh\\:mm\\:ss\\.ffff}");
        }
        catch (Exception ex)
        {
            LogError($"Pull for all repositories - Error: {ex.Message}");
        }

        if (filter is not null)
        {
            progress.Fraction += fraction;

            filter();
        }

        if (split is false)
        {
            progress.Fraction = 1f;
        }
    }

    private Task<TimeSpan> FetchForSelectedRepositoryHandler(ITreeNode? repositoryTreeNode = null, ProgressBar? progress = null, bool? split = false)
    {
        float fraction = 1f / 3 / (split is true ? 2 : 1);
        progress?.Fraction += fraction;

        var selected = repositoryTreeNode ?? _repositoryTree.SelectedObject;

        var repositoryNode = GetRepositoryNode(selected);

        if (repositoryNode is null)
        {
            if (split is false)
            {
                progress?.Fraction = 1f;
            }

            return Task.FromResult(TimeSpan.Zero);
        }

        progress?.Fraction += fraction;

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
            LogError($"Fetch for {repositoryNode.RepositoryName} - Error: {ex.Message}");
        }
        finally
        {
            stopwatch.Stop();
            repositoryNode!.IsUpdating = false;

            progress?.Fraction += fraction;

            _repositoryTree.RefreshObject(repositoryNode);
        }

        if (!repositoryNode!.HasError)
        {
            LogInfo($"Fetch for {repositoryNode.RepositoryName} - Complete: {stopwatch.Elapsed:hh\\:mm\\:ss\\.ffff}");
        }

        if (split is false)
        {
            progress?.Fraction = 1f;
        }

        return Task.FromResult(stopwatch.Elapsed);
    }

    private async Task FetchForAllRepositoriesHandler(ProgressBar progress, bool? split = false)
    {
        float fraction = 0.2f / (split is true ? 2 : 1);
        progress.Fraction += fraction;

        LogInfo("Fetch for all repositories - Started");

        List<Task> tasks = [];

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        foreach (var repositoryNode in _repositoryTree.Objects ?? [])
        {
            if (repositoryNode is null
                || repositoryNode is not RepositoryNode)
            {
                continue;
            }

            tasks.Add(Task.Run(async () => await FetchForSelectedRepositoryHandler(repositoryNode)));
        }

        progress.Fraction += fraction;

        if (tasks.Count is 0)
        {
            LogInfo("Fetch for all repositories - Skipped");

            if (split is false)
            {
                progress.Fraction = 1f;
            }

            return;
        }

        fraction = ((1f / (split is true ? 2 : 1)) - progress.Fraction) / tasks.Count;

        try
        {
            await foreach (Task<TimeSpan> completedTask in Task.WhenEach(tasks))
            {
                _ = await completedTask;
                progress.Fraction += fraction;
            }

            stopwatch.Stop();

            LogInfo($"Fetch for all repositories - Complete: {_repositoryTree.Objects?.Count() ?? 0} repositories in {stopwatch.Elapsed:hh\\:mm\\:ss\\.ffff}");
        }
        catch (Exception ex)
        {
            LogError($"Fetch for all repositories - Error: {ex.Message}");
        }

        if (split is false)
        {
            progress.Fraction = 1f;
        }
    }

    private Task<TimeSpan> RetrieveStatusForSelectedRepositoryHandler(ITreeNode? repositoryTreeNode = null, ProgressBar? progress = null)
    {
        const float fraction = 1f / 3;
        progress?.Fraction += fraction;

        var selected = repositoryTreeNode ?? _repositoryTree.SelectedObject;

        var repositoryNode = GetRepositoryNode(selected);

        if (repositoryNode is null)
        {
            progress?.Fraction = 1f;

            return Task.FromResult(TimeSpan.Zero);
        }

        progress?.Fraction += fraction;

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
            LogError($"Retrieve status for {repositoryNode.RepositoryName} - Error: {ex.Message}");
        }
        finally
        {
            stopwatch.Stop();
            repositoryNode!.IsUpdating = false;

            progress?.Fraction += fraction;

            _repositoryTree.RefreshObject(repositoryNode);
        }

        if (!repositoryNode!.HasError)
        {
            LogInfo($"Retrieve status for {repositoryNode.RepositoryName} - Completed: {stopwatch.Elapsed:hh\\:mm\\:ss\\.ffff}");
        }

        progress?.Fraction = 1f;

        return Task.FromResult(stopwatch.Elapsed);
    }

    private async Task RetrieveStatusForAllRepositoriesHandler(ProgressBar progress)
    {
        float fraction = 0.1f;
        progress.Fraction += fraction;

        LogInfo("Retrieve status for all repositories - Started");

        List<Task> tasks = [];

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        foreach (var repositoryNode in _repositoryTree.Objects ?? [])
        {
            if (repositoryNode is null
                || repositoryNode is not RepositoryNode)
            {
                continue;
            }

            tasks.Add(Task.Run(async () => await RetrieveStatusForSelectedRepositoryHandler(repositoryNode)));
        }

        progress.Fraction += fraction;

        if (tasks.Count is 0)
        {
            LogInfo("Retrieve status for all repositories - Skipped");

            progress.Fraction = 1f;

            return;
        }

        fraction = (1f - progress.Fraction) / tasks.Count;

        try
        {
            await foreach (Task<TimeSpan> completedTask in Task.WhenEach(tasks))
            {
                _ = await completedTask;
                progress.Fraction += fraction;
            }

            stopwatch.Stop();

            LogInfo($"Retrieve status for all repositories - Complete: {_repositoryTree.Objects?.Count() ?? 0} repositories in {stopwatch.Elapsed:hh\\:mm\\:ss\\.ffff}");
        }
        catch (Exception ex)
        {
            LogError($"Retrieve status for all repositories - Error: {ex.Message}");
        }

        progress.Fraction = 1f;
    }

    private Task CheckoutSelectedBranchHandler(ProgressBar? progress)
    {
        const float fraction = 1f / 5;
        progress?.Fraction += fraction;

        var selectedNode = _repositoryTree.SelectedObject;

        if (selectedNode is not BranchNode
            || !_repositoryTree.IsExpanded(_repositoryTree.GetParent(selectedNode))
            || (selectedNode as BranchNode)!.IsCurrentRepositoryHead)
        {
            if (selectedNode is BranchNode { IsCurrentRepositoryHead: true })
            {
                LogError($"Checkout branch {(selectedNode as BranchNode)!.FriendlyName} - Error: Branch already checked-out");
            }

            progress?.Fraction = 1f;

            return Task.CompletedTask;
        }

        progress?.Fraction += fraction;

        var repositoryNode = GetRepositoryNode(selectedNode);

        if (repositoryNode is null)
        {
            progress?.Fraction = 1f;

            return Task.CompletedTask;
        }

        progress?.Fraction += fraction;

        repositoryNode!.HasError = false;

        var branch = repositoryNode.Repository.Branches.First(branch => branch.FriendlyName == (selectedNode as BranchNode)!.FriendlyName);

        progress?.Fraction += fraction;

        try
        {
            LibGit2Sharp.Commands.Checkout(repositoryNode.Repository, branch);

            LogInfo($"Checkout branch {branch.FriendlyName} - Completed");
        }
        catch (Exception ex)
        {
            repositoryNode!.HasError = true;
            repositoryNode!.ErrorMessage = GetTrimmedErrorMessage(ex.Message);
            LogError($"Checkout branch {branch.FriendlyName} - Error: {ex.Message}");
        }
        finally
        {
            progress?.Fraction += fraction;
            _repositoryTree.RefreshObject(repositoryNode);
        }

        progress?.Fraction = 1f;

        return Task.CompletedTask;
    }

    private Task DeleteSelectedBranchHandler(ProgressBar progress)
    {
        const float fraction = 1f / 5;
        progress.Fraction += fraction;

        var selectedNode = _repositoryTree.SelectedObject;

        if (selectedNode is not BranchNode
            || _repositoryTree.IsExpanded(_repositoryTree.GetParent(selectedNode)) == false
            || (selectedNode as BranchNode)!.IsCurrentRepositoryHead)
        {
            if (selectedNode is BranchNode { IsCurrentRepositoryHead: true })
            {
                LogError($"Delete branch {(selectedNode as BranchNode)!.FriendlyName} - Error: Cannot delete branch while it is the repository HEAD");
            }

            progress.Fraction = 1f;

            return Task.CompletedTask;
        }

        progress.Fraction += fraction;

        var repositoryNode = GetRepositoryNode(selectedNode);

        if (repositoryNode is null)
        {
            progress.Fraction = 1f;

            return Task.CompletedTask;
        }

        progress.Fraction += fraction;

        repositoryNode!.HasError = false;

        var branch = repositoryNode.Repository.Branches.First(branch => branch.FriendlyName == (selectedNode as BranchNode)!.FriendlyName);

        progress.Fraction += fraction;

        try
        {
            repositoryNode.Repository.Branches.Remove(branch);

            LogInfo($"Delete branch {branch.FriendlyName} - Completed");
        }
        catch (Exception ex)
        {
            repositoryNode!.HasError = true;
            repositoryNode!.ErrorMessage = GetTrimmedErrorMessage(ex.Message);
            LogError($"Delete branch {branch.FriendlyName} - Error: {ex.Message}");
        }
        finally
        {
            progress.Fraction += fraction;

            _repositoryTree.RefreshObject(repositoryNode);
        }

        progress.Fraction = 1f;

        return Task.CompletedTask;
    }

    private Task ResetSelectedBranchHandler(ProgressBar progress, Action? filter = null)
    {
        float fraction = 1f / (filter is null ? 5 : 6);
        progress.Fraction += fraction;

        var selectedNode = _repositoryTree.SelectedObject;

        if (selectedNode is not BranchNode
            || !_repositoryTree.IsExpanded(_repositoryTree.GetParent(selectedNode))
            || !(selectedNode as BranchNode)!.IsCurrentRepositoryHead)
        {
            progress.Fraction = 1f;

            return Task.CompletedTask;
        }

        progress.Fraction += fraction;

        var repositoryNode = GetRepositoryNode(selectedNode);

        if (repositoryNode is null)
        {
            progress.Fraction = 1f;

            return Task.CompletedTask;
        }

        progress.Fraction += fraction;

        repositoryNode!.HasError = false;

        var branch = repositoryNode.Repository.Branches.First(branch => branch.FriendlyName == (selectedNode as BranchNode)!.FriendlyName);

        progress.Fraction += fraction;

        try
        {
            RepositoryActions.Reset(repositoryNode.Repository, branch.Tip);
            repositoryNode.Status = RepositoryActions.RetrieveStatus(repositoryNode.Repository);

            LogInfo($"Reset branch {branch.FriendlyName} - Completed");
        }
        catch (Exception ex)
        {
            repositoryNode!.HasError = true;
            repositoryNode!.ErrorMessage = GetTrimmedErrorMessage(ex.Message);
            LogError($"Reset branch {branch.FriendlyName} - Error: {ex.Message}");
        }
        finally
        {
            progress.Fraction += fraction;

            _repositoryTree.RefreshObject(repositoryNode);
        }

        if (filter is not null)
        {
            progress.Fraction += fraction;

            filter();
        }

        progress.Fraction = 1f;

        return Task.CompletedTask;
    }

    private void CopySelectedRepositoryPathToClipboardHandler()
    {
        var repositoryNode = GetRepositoryNode(_repositoryTree.SelectedObject);

        if (repositoryNode is null)
        {
            return;
        }

        App!.Clipboard?.TrySetClipboardData(Path.GetDirectoryName(repositoryNode.Repository.Info.WorkingDirectory)!);
    }

    private static string GetTrimmedErrorMessage(string errorMessage)
    {
        return errorMessage.Length <= 50
            ? errorMessage
            : errorMessage[..47] + "...";
    }

    private RepositoryNode? GetRepositoryNode(ITreeNode? treeNode)
    {
        if (treeNode is RepositoryNode)
        {
            return treeNode as RepositoryNode;
        }

        if (treeNode is not BranchNode
            || !_repositoryTree.IsExpanded(_repositoryTree.GetParent(treeNode)))
        {
            return null;
        }

        var parent = _repositoryTree.GetParent(treeNode);
        return parent as RepositoryNode;
    }
}
