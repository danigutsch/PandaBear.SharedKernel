using Microsoft.AspNetCore.Components;
using static PandaBear.Blazor.Tests.ComponentFixtures;

namespace PandaBear.Blazor.Tests;

/// <summary>Tests the public focus helper that pages call after a render.</summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class FocusHelperTests : BunitContext
{
    private const string FocusIdentifier = "Blazor._internal.domWrapper.focus";

    [Fact]
    public async Task A_captured_element_is_focused()
    {
        // Act
        await FocusHelper.TryFocus(this.Element("target"));

        // Assert
        this.FocusedIds().ShouldBe(["target"]);
    }

    [Fact]
    public async Task An_element_reference_that_was_never_captured_is_skipped()
    {
        // Act
        await FocusHelper.TryFocus(default(ElementReference));

        // Assert
        JSInterop.Invocations.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("disconnected")]
    [InlineData("removed")]
    [InlineData("disposed")]
    public async Task A_focus_call_that_fails_because_the_circuit_or_the_element_is_gone_is_ignored(string kind)
    {
        // Arrange
        JSInterop.SetupVoid(FocusIdentifier, _ => true).SetException(FocusFailure(kind));

        // Act
        await FocusHelper.TryFocus(this.Element("target"));

        // Assert
        this.FocusedIds().ShouldBe(["target"]);
    }
}
