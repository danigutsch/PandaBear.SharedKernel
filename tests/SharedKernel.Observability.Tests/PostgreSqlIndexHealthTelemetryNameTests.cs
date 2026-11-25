using SharedKernel.Testing;

namespace SharedKernel.Observability.Npgsql.Tests;

/// <summary>Tests that the index-health metrics use the names of the SharedKernel telemetry standard.</summary>
[TestScope(TestScope.Unit)]
[Trait(TestTraits.ComponentName, TestTraits.PostgreSqlObservabilityComponent)]
public sealed class PostgreSqlIndexHealthTelemetryNameTests
{
    [Fact]
    public void Meter_and_instruments_use_the_standard_names()
    {
        // Act, Assert
        PostgreSqlIndexHealthTelemetry.MeterName.ShouldBe("SharedKernel.Observability.Npgsql");
        PostgreSqlIndexHealthTelemetry.AssessmentMetricName.ShouldBe("sharedkernel.index_health.assessments");
        PostgreSqlIndexHealthTelemetry.CollectionMetricName.ShouldBe("sharedkernel.index_health.collections");
    }
}
