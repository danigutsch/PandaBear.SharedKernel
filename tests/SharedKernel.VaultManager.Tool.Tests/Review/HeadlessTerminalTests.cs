using Terminal.Gui.App;
using Terminal.Gui.Drivers;

namespace PandaBear.VaultManager.Tool.Tests.Review;

/// <summary>Checks that headless view tests stay away from the developer's terminal and clipboard.</summary>
[Collection(HeadlessTerminal.Collection)]
public sealed class HeadlessTerminalTests
{
    /// <summary>A headless application uses a fake clipboard instead of the developer's real one.</summary>
    [Fact]
    public void Headless_applications_copy_and_paste_with_a_fake_clipboard()
    {
        // Act
        using IApplication app = HeadlessTerminal.Start();

        // Assert
        app.Clipboard.ShouldBeOfType<FakeClipboard>();
    }
}
