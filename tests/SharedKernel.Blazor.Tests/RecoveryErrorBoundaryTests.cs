using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using static PandaBear.Blazor.Tests.ComponentFixtures;

namespace PandaBear.Blazor.Tests;

/// <summary>Tests the logging error boundary.</summary>
[Trait(Testing.SharedKernelTestTraitNames.ScopeName, Testing.SharedKernelTestTraitNames.UnitScope)]
public sealed class RecoveryErrorBoundaryTests : BunitContext
{
    private readonly RecordingFailureReporter _reporter = new();

    /// <summary>Initializes the context with a recording reporter.</summary>
    public RecoveryErrorBoundaryTests() => Services.AddSingleton<IUiFailureReporter>(_reporter);

    [Fact]
    public void Faulting_child_is_reported_and_replaced_by_fixed_recovery_copy_only()
    {
        // Arrange & Act
        var cut = Render<RecoveryErrorBoundary>(parameters => parameters
            .Add(component => component.RecoveryMessage, "Something went wrong. Reload the page.")
            .Add(component => component.OperationName, "app.root")
            .Add(component => component.ChildContent, ThrowingFragment(() => true)));

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
        // Arrange & Act
        var cut = Render<RecoveryErrorBoundary>(parameters => parameters
            .Add(component => component.RecoveryMessage, "Something went wrong.")
            .Add(component => component.ChildContent, ThrowingFragment(() => false)));

        // Assert
        _reporter.Reports.ShouldBeEmpty();
        cut.FindAll("[role=alert]").ShouldBeEmpty();
    }

    [Fact]
    public void Recover_button_resets_the_boundary_and_renders_the_child_again()
    {
        // Arrange
        var fail = true;
        var cut = Render<RecoveryErrorBoundary>(parameters => parameters
            .Add(component => component.RecoveryMessage, "Something went wrong.")
            .Add(component => component.RecoverLabel, "Try again")
            .Add(component => component.ChildContent, ThrowingFragment(() => fail)));
        fail = false;

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
            .Add(component => component.ChildContent, ThrowingFragment(() => true)));

        // Assert
        cut.FindAll("button").ShouldBeEmpty();
    }

    [Fact]
    public void Focus_moves_to_the_alert_after_a_fault()
    {
        // Arrange & Act
        var cut = Render<RecoveryErrorBoundary>(parameters => parameters
            .Add(component => component.RecoveryMessage, "Something went wrong.")
            .Add(component => component.ChildContent, ThrowingFragment(() => true)));

        // Assert
        var alert = cut.Find("[role=alert]");
        alert.GetAttribute("tabindex").ShouldBe("-1");
        var target = JSInterop.VerifyFocusAsyncInvoke(1)[0].Arguments[0].ShouldBeOfType<ElementReference>();
        target.Id.ShouldBe(alert.GetAttribute("blazor:elementreference"));
    }

    [Fact]
    public void Focus_moves_to_a_stable_anchor_after_try_again()
    {
        // Arrange
        var fail = true;
        var cut = Render<RecoveryErrorBoundary>(parameters => parameters
            .Add(component => component.RecoveryMessage, "Something went wrong.")
            .Add(component => component.RecoverLabel, "Try again")
            .Add(component => component.ChildContent, ThrowingFragment(() => fail)));
        fail = false;

        // Act
        cut.Find("button").Click();

        // Assert
        var anchor = cut.Find("[data-recovery-anchor]");
        anchor.GetAttribute("tabindex").ShouldBe("-1");
        var invocations = JSInterop.VerifyFocusAsyncInvoke(2);
        var target = invocations[1].Arguments[0].ShouldBeOfType<ElementReference>();
        target.Id.ShouldBe(anchor.GetAttribute("blazor:elementreference"));
    }
}
