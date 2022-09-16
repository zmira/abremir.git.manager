namespace abremir.Git.Manager.Models
{
    internal record BranchStatus(bool IsDirty, int Added, int Modified, int Removed);
}
