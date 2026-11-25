using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace SharedKernel.Terminal.Gui;

/// <summary>Positions resizable list and detail panes with a one-line heading and two footer rows.</summary>
public static class ListDetailLayout
{
    private const int ListWidthPercent = 35;
    private const int FooterRows = 2;

    /// <summary>Positions sibling views in a window. The caller owns the views and adds them to the same parent.</summary>
    /// <param name="list">The navigation pane.</param>
    /// <param name="heading">The detail heading.</param>
    /// <param name="detail">The detail pane.</param>
    /// <exception cref="ArgumentNullException">Thrown when any view is <see langword="null"/>.</exception>
    public static void Position(FrameView list, Label heading, View detail)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(heading);
        ArgumentNullException.ThrowIfNull(detail);

        list.X = 0;
        list.Y = 0;
        list.Width = Dim.Percent(ListWidthPercent);
        list.Height = Dim.Fill(FooterRows);
        heading.X = Pos.Right(list) + 1;
        heading.Y = 0;
        heading.Width = Dim.Fill();
        heading.Height = 1;
        detail.X = Pos.Right(list);
        detail.Y = 1;
        detail.Width = Dim.Fill();
        detail.Height = Dim.Fill(FooterRows);
    }
}
