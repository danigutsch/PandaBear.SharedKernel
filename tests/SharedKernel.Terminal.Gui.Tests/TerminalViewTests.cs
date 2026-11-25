using System.Collections.ObjectModel;
using SharedKernel.Terminal.Gui.Testing;
using Terminal.Gui.App;
using Terminal.Gui.Views;

namespace SharedKernel.Terminal.Gui.Tests;

/// <summary>Exercises the controls and layout in a separate screen built without any Vault types.</summary>
[Collection(HeadlessTerminal.Collection)]
public sealed class TerminalViewTests
{
    [Fact]
    public void A_compact_file_browser_composes_a_list_and_masked_detail_without_vault_types()
    {
        // Arrange
        using IApplication app = HeadlessTerminal.Start();
        app.Driver!.SetScreenSize(42, 12);
        using var browser = new Window();
        var files = new FrameView { Title = "Files" };
        var heading = new Label { Text = "Recipients" };
        ListView list = TerminalViews.CreateList();
        list.SetSource(new ObservableCollection<string>(["sample.sops.yaml"]));
        files.Add(list);
        TableView detail = TerminalViews.CreateDetail();
        detail.Table = new RowTableSource(["Field", "Value"], [["Key", TerminalText.Value("synthetic value", masked: true)]]);
        ListDetailLayout.Position(files, heading, detail);
        browser.Add(files, heading, detail);

        // Act
        SessionToken token = app.Begin(browser).ShouldNotBeNull();
        int width = files.Frame.Width;
        int detailPosition = detail.Frame.X;
        app.RequestStop(browser);
        app.End(token);

        // Assert
        width.ShouldBeGreaterThan(0);
        detailPosition.ShouldBe(width);
        detail.FullRowSelect.ShouldBeTrue();
        detail.Table[0, 1].ShouldBe(TerminalText.Mask);
    }

    [Fact]
    public void A_diff_table_navigates_individual_values()
    {
        // Act
        using TableView diff = TerminalViews.CreateDiff();

        // Assert
        diff.FullRowSelect.ShouldBeFalse();
    }
}
