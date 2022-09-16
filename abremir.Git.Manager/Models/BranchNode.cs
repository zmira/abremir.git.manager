using Terminal.Gui.Trees;

namespace abremir.Git.Manager.Models
{
    internal class BranchNode : TreeNode
    {
        public BranchNode(string friendlyName)
        {
            FriendlyName = friendlyName;
        }

        public string FriendlyName { get; }
        public BranchStatus? Status { get; set; }
        public BranchTrackingDetails TrackingDetails { get; set; } = new BranchTrackingDetails();
        public bool IsGone { get; set; }
        public bool IsCurrentRepositoryHead { get; set; }

        public override string Text => GetBranchText();

        private string GetBranchText()
        {
            var status = IsCurrentRepositoryHead && Status?.IsDirty is true
                ? $" +{Status.Added} ~{Status.Modified} -{Status.Removed}"
                : string.Empty;
            var statusSymbol = IsGone
                ? " ≠"
                : IsCurrentRepositoryHead && Status?.IsDirty is false
                    ? " ≡"
                    : string.Empty;
            var trackingDetails = $"↓{TrackingDetails.BehindBy} ↑{TrackingDetails.AheadBy}";

            return $"{FriendlyName}{status}{statusSymbol} {trackingDetails}";
        }
    }
}
