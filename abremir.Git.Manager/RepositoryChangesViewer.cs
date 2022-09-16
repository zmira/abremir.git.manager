using abremir.Git.Manager.Models;
using Terminal.Gui;

namespace abremir.Git.Manager
{
    internal class RepositoryChangesViewer : Dialog
    {
        public RepositoryChangesViewer(string repositoryName, IReadOnlyCollection<ChangedItem> changedItems)
        {
            Title = $"Changes in {repositoryName}";
            Border.Effect3D = false;

            var treeChangesFrame = new FrameView
            {
                Width = Dim.Fill(),
                Height = Dim.Percent(35)
            };

            var treeChangesList = new ListView
            {
                Width = Dim.Fill(),
                Height = Dim.Fill()
            };

            treeChangesFrame.Add(treeChangesList);

            Add(treeChangesFrame);

            var treeChangesScrollBar = new ScrollBarView(treeChangesList, true);

            treeChangesScrollBar.ChangedPosition += () =>
            {
                treeChangesList.TopItem = treeChangesScrollBar.Position;
                if (treeChangesList.TopItem != treeChangesScrollBar.Position)
                {
                    treeChangesScrollBar.Position = treeChangesList.TopItem;
                }
                treeChangesList.SetNeedsDisplay();
            };

            treeChangesScrollBar.OtherScrollBarView.ChangedPosition += () =>
            {
                treeChangesList.LeftItem = treeChangesScrollBar.OtherScrollBarView.Position;
                if (treeChangesList.LeftItem != treeChangesScrollBar.OtherScrollBarView.Position)
                {
                    treeChangesScrollBar.OtherScrollBarView.Position = treeChangesList.LeftItem;
                }
                treeChangesList.SetNeedsDisplay();
            };

            treeChangesList.DrawContent += (_) =>
            {
                treeChangesScrollBar.Size = treeChangesList.Source.Count - 1;
                treeChangesScrollBar.Position = treeChangesList.TopItem;
                treeChangesScrollBar.OtherScrollBarView.Size = treeChangesList.Maxlength - 1;
                treeChangesScrollBar.OtherScrollBarView.Position = treeChangesList.LeftItem;
                treeChangesScrollBar.Refresh();
            };

            treeChangesList.SetSource(changedItems.ToList());

            var patchFrame = new FrameView
            {
                Y = Pos.Bottom(treeChangesFrame),
                Width = Dim.Fill(),
                Height = Dim.Fill(1),
            };

            var patchView = new TextView
            {
                ReadOnly = true,
                Multiline = true,
                Width = Dim.Fill(),
                Height = Dim.Fill(),
                Text = changedItems.FirstOrDefault()?.Patch ?? string.Empty
            };

            patchFrame.Add(patchView);

            Add(patchFrame);

            var patchViewScrollBar = new ScrollBarView(patchView, true);

            patchViewScrollBar.ChangedPosition += () =>
            {
                patchView.TopRow = patchViewScrollBar.Position;
                if (patchView.TopRow != patchViewScrollBar.Position)
                {
                    patchViewScrollBar.Position = patchView.TopRow;
                }
                patchView.SetNeedsDisplay();
            };

            patchViewScrollBar.OtherScrollBarView.ChangedPosition += () =>
            {
                patchView.LeftColumn = patchViewScrollBar.OtherScrollBarView.Position;
                if (patchView.LeftColumn != patchViewScrollBar.OtherScrollBarView.Position)
                {
                    patchViewScrollBar.OtherScrollBarView.Position = patchView.LeftColumn;
                }
                patchView.SetNeedsDisplay();
            };

            patchViewScrollBar.VisibleChanged += () =>
            {
                if (patchViewScrollBar.Visible && patchView.RightOffset == 0)
                {
                    patchView.RightOffset = 1;
                }
                else if (!patchViewScrollBar.Visible && patchView.RightOffset == 1)
                {
                    patchView.RightOffset = 0;
                }
            };

            patchViewScrollBar.OtherScrollBarView.VisibleChanged += () =>
            {
                if (patchViewScrollBar.OtherScrollBarView.Visible && patchView.BottomOffset == 0)
                {
                    patchView.BottomOffset = 1;
                }
                else if (!patchViewScrollBar.OtherScrollBarView.Visible && patchView.BottomOffset == 1)
                {
                    patchView.BottomOffset = 0;
                }
            };

            patchView.DrawContent += (_) =>
            {
                patchViewScrollBar.Size = patchView.Lines;
                patchViewScrollBar.Position = patchView.TopRow;
                if (patchViewScrollBar.OtherScrollBarView != null)
                {
                    patchViewScrollBar.OtherScrollBarView.Size = patchView.Maxlength;
                    patchViewScrollBar.OtherScrollBarView.Position = patchView.LeftColumn;
                }
                patchViewScrollBar.LayoutSubviews();
                patchViewScrollBar.Refresh();
            };

            var closeButton = new Button("Close", true);

            AddButton(closeButton);

            closeButton.Clicked += () => Application.RequestStop(this);

            treeChangesList.SelectedItemChanged += (selectedListItem) => patchView.Text = ((ChangedItem)selectedListItem.Value).Patch;
        }
    }
}
