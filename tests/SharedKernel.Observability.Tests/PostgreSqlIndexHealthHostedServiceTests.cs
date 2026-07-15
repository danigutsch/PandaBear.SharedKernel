using SharedKernel.Testing;

namespace SharedKernel.Observability.Npgsql.Tests;

[Collection("PostgreSQL index health telemetry")]
[Trait(TestTraitNames.ScopeName, TestTraits.IntegrationScope)]
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
}
