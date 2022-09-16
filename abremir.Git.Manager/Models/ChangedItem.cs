using LibGit2Sharp;

namespace abremir.Git.Manager.Models
{
    internal class ChangedItem
    {
        public readonly string Path;
        public readonly string Patch;
        public readonly ChangeKind Status;

        public ChangedItem(string path, ChangeKind status, string patch)
        {
            Path = path;
            Patch = patch;
            Status = status;
        }

        public override string ToString()
        {
            var changeKind = Status switch
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

            return changeKind + Path;
        }
    }
}
