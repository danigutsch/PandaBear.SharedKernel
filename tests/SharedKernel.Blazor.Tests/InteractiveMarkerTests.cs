using Microsoft.AspNetCore.Components;

namespace PandaBear.Blazor.Tests;

/// <summary>Tests the hidden marker that tells browser tests when a page is interactive.</summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class InteractiveMarkerTests : BunitContext
{
    /// <summary>Declares an interactive renderer, as the Blazor Server circuit does.</summary>
    public InteractiveMarkerTests() => SetRendererInfo(new RendererInfo("Server", isInteractive: true));

    [Fact]
    public void The_marker_is_hidden_and_reports_an_interactive_page()
    {
        // Act
        var cut = Render<InteractiveMarker>();

        // Assert
        var marker = cut.Find(InteractiveMarker.ReadySelector);
        marker.HasAttribute("hidden").ShouldBeTrue();
        marker.GetAttribute(InteractiveMarker.AttributeName).ShouldBe("true");
    }

    [Fact]
    public void The_ready_selector_names_the_attribute_the_marker_renders()
    {
        // Assert
        InteractiveMarker.ReadySelector.ShouldBe($"[{InteractiveMarker.AttributeName}='true']");
    }
}
