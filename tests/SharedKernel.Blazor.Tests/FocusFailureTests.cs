using Microsoft.Extensions.DependencyInjection;
using static PandaBear.Blazor.Tests.ComponentFixtures;

namespace PandaBear.Blazor.Tests;

/// <summary>Tests that a focus call failing because the circuit is gone never fails a component.</summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class FocusFailureTests : BunitContext
{
    private const string FocusIdentifier = "Blazor._internal.domWrapper.focus";

    [Theory]
    [InlineData("disconnected")]
    [InlineData("canceled")]
    [InlineData("disposed")]
    public void Error_summary_survives_a_focus_failure(string kind)
    {
        // Arrange
        JSInterop.SetupVoid(FocusIdentifier, _ => true).SetException(FocusFailure(kind));

        // Act
        var cut = Render<ErrorSummary>(parameters => parameters
            .Add(component => component.Title, "There is a problem")
            .Add(component => component.Items, Items(("a", "b"))));

        // Assert
        cut.FindAll("li").Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("disconnected")]
    [InlineData("canceled")]
    [InlineData("disposed")]
    public void Upload_status_survives_a_focus_failure(string kind)
    {
        // Arrange
        JSInterop.SetupVoid(FocusIdentifier, _ => true).SetException(FocusFailure(kind));
        var state = Started();
        var cut = Render<UploadStatus>(parameters => parameters
            .Add(component => component.State, state)
            .Add(component => component.Label, "Upload"));
        cut.Find("button").Focus();

        // Act
        state.Cancel(state.OperationId);
        cut.Render();

        // Assert
        cut.FindAll("button").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("disconnected")]
    [InlineData("canceled")]
    [InlineData("disposed")]
    public void Error_boundary_survives_a_focus_failure(string kind)
    {
        // Arrange
        Services.AddSingleton<IUiFailureReporter>(new RecordingFailureReporter());
        JSInterop.SetupVoid(FocusIdentifier, _ => true).SetException(FocusFailure(kind));

        // Act
        var cut = Render<RecoveryErrorBoundary>(parameters => parameters
            .Add(component => component.RecoveryMessage, "Something went wrong.")
            .Add(component => component.ChildContent, ThrowingFragment(() => true)));

        // Assert
        cut.FindAll("[role=alert]").Count.ShouldBe(1);
    }
}
