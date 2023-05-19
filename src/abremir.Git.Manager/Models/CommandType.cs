namespace abremir.Git.Manager.Models
{
    internal enum CommandType
    {
        CopyPathToClipboard,
        CheckoutSelectedBranch,
        DeleteSelectedBranch,
        ExpandAllNodes,
        CollapseAllNodes,
        FetchForSelectedRepository,
        FetchForAllRepositories,
        ShowHelp,
        ToggleLogWindow,
        PullForSelectedRepository,
        PullForAllRepositories,
        ResetSelectedBranch,
        RetrieveStatusForSelectedRepository,
        RetrieveStatusForAllRepositories,
        UpdateSelectedRepository,
        UpdateAllRepositories,
        ResetLogWindow,
        LoadRepositories,
        ViewChangesInSelectedRepository,
        ChangeBaseDirectory
    }
}
