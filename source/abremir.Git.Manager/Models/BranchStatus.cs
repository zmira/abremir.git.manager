namespace abremir.Git.Manager.Models;

internal record struct BranchStatus(bool IsDirty, int Added, int Modified, int Removed);
