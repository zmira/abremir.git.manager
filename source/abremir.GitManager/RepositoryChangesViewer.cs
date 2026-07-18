using System.Collections.ObjectModel;
using abremir.GitManager.Models;
using Terminal.Gui.Drawing;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace abremir.GitManager;

internal class RepositoryChangesViewer : Dialog
{
    public RepositoryChangesViewer(string repositoryName, ObservableCollection<ChangedItem> changedItems)
    {
        Title = $"Changes in {repositoryName}";
        Width = Dim.Percent(90);
        Height = Dim.Percent(90);
        ShadowStyle = ShadowStyles.None;

        var treeChangesList = new ListView
        {
            Width = Dim.Fill(),
            Height = Dim.Percent(35),
            BorderStyle = LineStyle.Rounded,
        };
        treeChangesList.ViewportSettings |= ViewportSettingsFlags.HasScrollBars;

        Add(treeChangesList);

        treeChangesList.SetSource(changedItems);

        var patchView = new ScrollableCode
        {
            Y = Pos.Bottom(treeChangesList),
            Width = Dim.Fill(),
            Height = Dim.Fill(1),
            Text = changedItems.Any() ? changedItems.First().Patch : string.Empty,
            Language = "diff",
            BorderStyle = LineStyle.Rounded,
            SchemeName = "Base",
        };

        Add(patchView);

        var closeButton = new Button
        {
            Text = "Close",
            IsDefault = true,
        };

        AddButton(closeButton);

        closeButton.Accepting += (s, _) => (s as View)?.App!.RequestStop();
        treeChangesList.ValueChanged += (_, valueChangedEvent) => patchView.Text = changedItems[valueChangedEvent.NewValue ?? 0].Patch;
    }
}
