using LibGit2Sharp;

namespace abremir.GitManager.Models;

internal record struct ChangedItem(string Path, ChangeKind Status, string Patch)
{
    public override readonly string ToString() =>
        GetChangeKindSymbol() + Path;

    private readonly string GetChangeKindSymbol() =>
        Status switch
        {
            ChangeKind.Renamed => "(R) ",
            ChangeKind.Deleted => "(D) ",
            ChangeKind.Modified => "(M) ",
            ChangeKind.Added => "(A) ",
            ChangeKind.Copied => "(C) ",
            ChangeKind.Ignored => "(I) ",
            ChangeKind.TypeChanged => "(T) ",
            _ => string.Empty
        };
}
