using LibGit2Sharp;
using Terminal.Gui.Trees;

namespace abremir.Git.Manager.Models
{
    internal class RepositoryNode : TreeNode
    {
        public RepositoryNode(Repository repository)
        {
            RepositoryName = Path.GetFileName(Path.GetDirectoryName(repository.Info.WorkingDirectory))!;
            Repository = repository;
        }

        public string RepositoryName { get; }
        public Repository Repository { get; }
        public RepositoryStatus? Status { get; set; }
        public bool IsUpdating { get; set; }
        public bool HasError { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;

        public bool CurrentRepositoryHeadIsBehind => Repository.Branches.FirstOrDefault(branch => branch.IsCurrentRepositoryHead)?.TrackingDetails.BehindBy > 0;

        public override IList<ITreeNode> Children => GetBranches().Cast<ITreeNode>().ToList();
        public override string Text => $"{RepositoryName}{(CurrentRepositoryHeadIsBehind ? " ↓" : string.Empty)}{(Status?.IsDirty == true ? " *" : string.Empty)}{(IsUpdating ? " ♦" : string.Empty)}{(HasError ? $" [{ErrorMessage}]" : string.Empty)}";

        private List<BranchNode> GetBranches()
        {
            return Repository?.Branches
                .Where(branch => !branch.IsRemote)
                .OrderByDescending(branch => branch.IsCurrentRepositoryHead ? 1 : 0)
                .ThenBy(branch => branch.FriendlyName)
                .Select(branch =>
                {
                    return new BranchNode(branch.FriendlyName)
                    {
                        IsCurrentRepositoryHead = branch.IsCurrentRepositoryHead,
                        IsGone = branch.TrackedBranch?.Tip is null,
                        Status = branch.IsCurrentRepositoryHead && Status is not null
                            ? new BranchStatus(Status.IsDirty, Status.Added.Count(), Status.Modified.Count(), Status.Removed.Count())
                            : default,
                        TrackingDetails = new BranchTrackingDetails(branch.TrackingDetails.BehindBy, branch.TrackingDetails.AheadBy)
                    };
                })
                .ToList() ?? [];
        }
    }
}
