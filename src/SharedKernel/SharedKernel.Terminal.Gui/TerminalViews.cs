using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace SharedKernel.Terminal.Gui;

/// <summary>Fills list and detail panes with views that leave letter shortcuts available to the caller.</summary>
public static class TerminalViews
{
    /// <summary>Creates a list that fills its pane below any heading or search field.</summary>
    /// <param name="top">Rows reserved above the list.</param>
    /// <returns>The list view.</returns>
    public static ListView CreateList(int top = 0) =>
        new() { X = 0, Y = top, Width = Dim.Fill(), Height = Dim.Fill(), KeystrokeNavigator = null! };

    /// <summary>Creates a table that displays details one row at a time.</summary>
    /// <returns>The detail table.</returns>
    public static TableView CreateDetail() =>
        new() { Width = Dim.Fill(), Height = Dim.Fill(), CollectionNavigator = null!, FullRowSelect = true };

    /// <summary>Creates a comparison table that lets the user navigate and select individual cells.</summary>
    /// <returns>The diff table.</returns>
    public static TableView CreateDiff() =>
        new() { Width = Dim.Fill(), Height = Dim.Fill(), CollectionNavigator = null!, FullRowSelect = false };
}
