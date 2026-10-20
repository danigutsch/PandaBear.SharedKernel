using System.Diagnostics;

namespace PandaBear.Observability.Tests;

public sealed class ActivityPrivacyProcessorTests
{
    [Fact]
    public void Additional_policy_cannot_disable_baseline_redaction()
    {
        // Arrange
        using var activity = new Activity("operation");
        activity.SetTag("authorization", "secret");
        activity.SetTag("operation.result", "success");
        using var processor = new ActivityPrivacyProcessor(_ => false);

        // Act
        processor.OnEnd(activity);

        // Assert
        activity.GetTagItem("authorization").ShouldBeNull();
        activity.GetTagItem("operation.result").ShouldBe("success");
    }

    [Fact]
    public void Application_policy_adds_redaction_without_changing_safe_attributes()
    {
        // Arrange
        using var activity = new Activity("operation");
        activity.SetTag("private.identifier", "secret");
        activity.SetTag("operation.result", "success");
        using var processor = new ActivityPrivacyProcessor(name => name == "private.identifier");

        // Act
        processor.OnEnd(activity);

        // Assert
        activity.GetTagItem("private.identifier").ShouldBeNull();
        activity.GetTagItem("operation.result").ShouldBe("success");
    }
}
