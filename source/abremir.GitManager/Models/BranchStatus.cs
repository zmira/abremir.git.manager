namespace abremir.GitManager.Models;

internal record struct BranchStatus(bool IsDirty, int Added, int Modified, int Removed);
