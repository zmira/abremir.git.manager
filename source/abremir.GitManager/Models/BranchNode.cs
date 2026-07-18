using Terminal.Gui.Views;

namespace abremir.GitManager.Models;

internal class BranchNode(string friendlyName) : TreeNode
{
    public string FriendlyName { get; } = friendlyName;
    public BranchStatus? Status { get; set; }
    public BranchTrackingDetails TrackingDetails { get; set; } = new BranchTrackingDetails();
    public bool IsGone { get; set; }
    public bool IsCurrentRepositoryHead { get; set; }

    public override string Text =>
        GetBranchText();

    private string GetBranchText()
    {
        var status = IsCurrentRepositoryHead && Status?.IsDirty is true
            ? $" +{Status.Value.Added} ~{Status.Value.Modified} -{Status.Value.Removed}"
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
