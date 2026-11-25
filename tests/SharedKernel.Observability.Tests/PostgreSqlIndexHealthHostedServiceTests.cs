using SharedKernel.Testing;

namespace SharedKernel.Observability.Npgsql.Tests;

/// <summary>Integration tests for the PostgreSQL index health hosted service lifecycle and telemetry.</summary>
[Collection("PostgreSQL index health telemetry")]
[TestScope(TestScope.Integration)]
[Trait(TestTraits.ComponentName, TestTraits.PostgreSqlObservabilityComponent)]
public sealed class PostgreSqlIndexHealthHostedServiceTests
{
    [Fact]
    public async Task Hosted_service_records_an_unavailable_collection_and_stops_cleanly()
    {
        // Arrange
        await using var scope = await PostgreSqlIndexHealthHostedServiceScope.Start(TestContext.Current.CancellationToken);

        // Act
        var unavailableObserved = await scope.WaitForUnavailableCollection(TestContext.Current.CancellationToken);
        var hasCollectionMeasurement = scope.Measurements.Any(
            measurement => measurement.InstrumentName == PostgreSqlIndexHealthTelemetry.CollectionMetricName);
        var renderedTags = string.Join(
            ",",
            scope.Measurements.SelectMany(measurement => measurement.Tags.Select(tag => $"{tag.Key}={tag.Value}")));

        // Assert
        unavailableObserved.ShouldBeTrue();
        hasCollectionMeasurement.ShouldBeTrue();
        renderedTags.ShouldNotContain("127.0.0.1", StringComparison.Ordinal);
        renderedTags.ShouldNotContain("monitor", StringComparison.Ordinal);
        renderedTags.ShouldNotContain("test-only", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Monitor_pool_dimensions_use_stable_names_without_connection_identity()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var scope = await PostgreSqlIndexHealthHostedServiceScope.Start(ct);
        await scope.WaitForUnavailableCollection(ct);

        // Act
        var names = PostgreSqlIndexHealthHostedServiceScope.ReadPoolNames();

        // Assert
        names.ShouldNotBeEmpty();
        var exposesConfiguredIdentity = names.Any(static name =>
            name.Contains("synthetic_monitor_", StringComparison.Ordinal)
            || name.Contains("127.0.0.1", StringComparison.Ordinal)
            || name.Contains("test-only", StringComparison.Ordinal));
        exposesConfiguredIdentity.ShouldBeFalse();
        names.ShouldContain("postgresql-index-health-1");
        names.ShouldContain("postgresql-index-health-2");
    }
}
