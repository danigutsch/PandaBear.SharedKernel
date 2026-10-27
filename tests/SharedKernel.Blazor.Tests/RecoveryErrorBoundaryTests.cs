using Microsoft.Extensions.DependencyInjection;
using static PandaBear.Blazor.Tests.ComponentFixtures;

namespace PandaBear.Blazor.Tests;

/// <summary>Tests the logging error boundary.</summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class RecoveryErrorBoundaryTests : BunitContext
{
    private readonly RecordingFailureReporter _reporter = new();

    /// <summary>Initializes the context with a recording reporter.</summary>
    public RecoveryErrorBoundaryTests()
    {
        Services.AddSingleton<IUiFailureReporter>(_reporter);
        ThrowingChild.ShouldThrow = true;
    }

    [Fact]
    public void Faulting_child_is_reported_and_replaced_by_fixed_recovery_copy_only()
    {
        // Arrange & Act
        var cut = Render<RecoveryErrorBoundary>(parameters => parameters
            .Add(component => component.RecoveryMessage, "Something went wrong. Reload the page.")
            .Add(component => component.OperationName, "app.root")
            .Add(component => component.ChildContent, Child<ThrowingChild>()));

        // Assert
        var report = _reporter.Reports.ShouldHaveSingleItem();
        report.Operation.ShouldBe("app.root");
        report.Exception.ShouldBeOfType<InvalidOperationException>();
        var alert = cut.Find("[role=alert]");
        alert.TextContent.Trim().ShouldBe("Something went wrong. Reload the page.");
        cut.Markup.ShouldNotContain(ThrowingChild.SecretMessage, StringComparison.Ordinal);
        cut.Markup.ShouldNotContain("at Contoso", StringComparison.Ordinal);
    }

    [Fact]
    public void Healthy_child_is_rendered_without_a_report()
    {
        // Arrange
        ThrowingChild.ShouldThrow = false;

        // Act
        var cut = Render<RecoveryErrorBoundary>(parameters => parameters
            .Add(component => component.RecoveryMessage, "Something went wrong.")
            .Add(component => component.ChildContent, Child<ThrowingChild>()));

        // Assert
        _reporter.Reports.ShouldBeEmpty();
        cut.FindAll("[role=alert]").ShouldBeEmpty();
    }

    [Fact]
    public void Recover_button_is_rendered_only_with_a_label_and_resets_the_boundary()
    {
        // Arrange
        var cut = Render<RecoveryErrorBoundary>(parameters => parameters
            .Add(component => component.RecoveryMessage, "Something went wrong.")
            .Add(component => component.RecoverLabel, "Try again")
            .Add(component => component.ChildContent, Child<ThrowingChild>()));
        ThrowingChild.ShouldThrow = false;

        // Act
        cut.Find("button").Click();

        // Assert
        cut.FindAll("[role=alert]").ShouldBeEmpty();
    }

    [Fact]
    public void Without_a_recover_label_no_button_is_rendered()
    {
        // Arrange & Act
        var cut = Render<RecoveryErrorBoundary>(parameters => parameters
            .Add(component => component.RecoveryMessage, "Something went wrong.")
            .Add(component => component.ChildContent, Child<ThrowingChild>()));

        // Assert
        cut.FindAll("button").ShouldBeEmpty();
    }
}
