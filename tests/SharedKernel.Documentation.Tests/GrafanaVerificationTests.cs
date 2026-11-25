namespace PandaBear.Documentation.Tests;

/// <summary>Checks the verification rules against a fictional venue operations dashboard.</summary>
[Trait(Testing.TestTraitNames.CapabilityName, TestTraits.DocumentationGenerationCapability)]
[Trait(Testing.TestTraitNames.CategoryName, TestTraits.CoreBehaviorCategory)]
public sealed class GrafanaVerificationTests
{
    [Fact]
    public void Evaluates_rendered_panels_and_reports_missing_required_panels()
    {
        // Arrange
        string[] requiredTitles = ["Open rooms", "Workshop attendance", "Published shows"];
        GrafanaPanelObservation[] observations =
        [
            new("Open rooms", Drawn: true, NoData: false, QueryError: false),
            new("Workshop attendance", Drawn: true, NoData: true, QueryError: false),
            new("Door events", Drawn: true, NoData: true, QueryError: false),
            new("Projection health", Drawn: false, NoData: false, QueryError: true),
        ];

        // Act
        var results = GrafanaVerification.CheckPanels(requiredTitles, observations);

        // Assert
        results.Select(result => result.Title).ShouldBe(
            ["Open rooms", "Workshop attendance", "Door events", "Projection health", "Published shows"]);
        results.Select(result => result.Issues).ShouldBe(
        [
            GrafanaPanelIssues.None,
            GrafanaPanelIssues.NoData,
            GrafanaPanelIssues.None,
            GrafanaPanelIssues.NotDrawn | GrafanaPanelIssues.QueryError,
            GrafanaPanelIssues.Missing,
        ]);
    }

    [Fact]
    public void Checks_ordered_provisioned_targets_and_accepts_idle_series_only_when_requested()
    {
        // Arrange
        var openRooms = new GrafanaQueryTarget(3, "Open rooms", "A", "prometheus", "venue-metrics", "venue_rooms_open", "data");
        var doorEvents = new GrafanaQueryTarget(8, "Door events", "B", "loki", "venue-logs", "{event=\"door\"}", "idle-ok");
        GrafanaQueryTarget[] expected = [openRooms, doorEvents];

        // Act
        var unprovisioned = GrafanaVerification.CheckProvisioning(false, expected, expected);
        var matching = GrafanaVerification.CheckProvisioning(true, expected, [openRooms with { }, doorEvents with { }]);
        var reordered = GrafanaVerification.CheckProvisioning(true, expected, [doorEvents, openRooms]);
        var edited = GrafanaVerification.CheckProvisioning(true, expected, [openRooms with { DatasourceUid = "other" }, doorEvents]);
        var idle = new GrafanaQueryObservation(true, 0);

        // Assert
        unprovisioned.ShouldBe(GrafanaProvisioningStatus.NotProvisioned);
        matching.ShouldBe(GrafanaProvisioningStatus.Current);
        reordered.ShouldBe(GrafanaProvisioningStatus.TargetsDiffer);
        edited.ShouldBe(GrafanaProvisioningStatus.TargetsDiffer);
        idle.Passes(requireData: false).ShouldBeTrue();
        idle.Passes(requireData: true).ShouldBeFalse();
        new GrafanaQueryObservation(false, 4).Passes(requireData: false).ShouldBeFalse();
        new GrafanaQueryObservation(true, 4).Passes(requireData: true).ShouldBeTrue();
    }
}
