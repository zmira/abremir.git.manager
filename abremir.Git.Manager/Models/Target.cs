namespace abremir.Git.Manager.Models
{
    [Flags]
    internal enum Target
    {
        None = 0,
        RepositoryWindow = 1,
        RepositoryNode = 2,
        BranchNode = 4,
        LogWindow = 8
    }
}
