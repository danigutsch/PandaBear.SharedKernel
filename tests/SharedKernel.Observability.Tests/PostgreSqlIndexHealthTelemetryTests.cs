using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using SharedKernel.Testing;

namespace SharedKernel.Observability.Npgsql.Tests;

[Trait(TestTraitNames.ScopeName, TestTraits.UnitScope)]
[Trait(TestTraits.ComponentName, TestTraits.PostgreSqlObservabilityComponent)]
public sealed class PostgreSqlIndexHealthTelemetryTests
{
    [Fact]
    public void Record_aggregates_assessments_by_action_and_reason()
    {
        // Arrange
        var tableTooSmallEvidence = new PostgreSqlIndexEvidence
        {
            Kind = PostgreSqlIndexEvidenceKind.Table,
            SchemaName = "public",
            TableName = "bookings",
            StatisticsWindow = TimeSpan.FromDays(14),
            ScanCount = 10,
            TuplesRead = 10,
            TuplesFetched = 0,
            EstimatedRows = 10,
            IsProtected = false,
            IsUsable = true,
            IsSimple = true,
            StatisticsAreReliable = true,
        };
        var statisticsUnavailableEvidence = tableTooSmallEvidence with { TableName = "customers" };
        var secondTableTooSmallEvidence = tableTooSmallEvidence with { TableName = "tours" };
        var result = new PostgreSqlIndexHealthCollectionResult(
            PostgreSqlIndexHealthCollectionOutcome.Collected,
            [
                new PostgreSqlIndexHealthAssessment(
                    tableTooSmallEvidence,
                    PostgreSqlIndexHealthRecommendationAction.InsufficientEvidence,
                    PostgreSqlIndexHealthRecommendationReason.TableTooSmall),
                new PostgreSqlIndexHealthAssessment(
                    statisticsUnavailableEvidence,
                    PostgreSqlIndexHealthRecommendationAction.InsufficientEvidence,
                    PostgreSqlIndexHealthRecommendationReason.StatisticsUnavailable),
                new PostgreSqlIndexHealthAssessment(
                    secondTableTooSmallEvidence,
                    PostgreSqlIndexHealthRecommendationAction.InsufficientEvidence,
                    PostgreSqlIndexHealthRecommendationReason.TableTooSmall),
            ]);
        var measurements = new ConcurrentQueue<(string InstrumentName, long Value, IReadOnlyDictionary<string, string?> Tags)>();
        using var meterListener = new MeterListener
        {
            InstrumentPublished = static (instrument, meterListener) =>
            {
                if (string.Equals(instrument.Meter.Name, PostgreSqlIndexHealthTelemetry.MeterName, StringComparison.Ordinal))
                {
                    meterListener.EnableMeasurementEvents(instrument);
                }
            },
        };
        meterListener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
        {
            var recordedTags = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var tag in tags)
            {
                recordedTags[tag.Key] = tag.Value?.ToString();
            }

            measurements.Enqueue((instrument.Name, value, recordedTags));
        });
        meterListener.Start();

        // Act
        PostgreSqlIndexHealthTelemetry.Record(result);
        var assessmentMeasurements = measurements
            .Where(measurement => measurement.InstrumentName == PostgreSqlIndexHealthTelemetry.AssessmentMetricName)
            .ToArray();
        var tableTooSmallMeasurements = assessmentMeasurements
            .Where(measurement => measurement.Tags.TryGetValue("action", out var action)
                && action == "insufficient_evidence"
                && measurement.Tags.TryGetValue("reason", out var reason)
                && reason == "table_too_small")
            .ToArray();
        var statisticsUnavailableMeasurements = assessmentMeasurements
            .Where(measurement => measurement.Tags.TryGetValue("action", out var action)
                && action == "insufficient_evidence"
                && measurement.Tags.TryGetValue("reason", out var reason)
                && reason == "statistics_unavailable")
            .ToArray();

        // Assert
        assessmentMeasurements.Length.ShouldBe(2);
        tableTooSmallMeasurements.Length.ShouldBe(1);
        tableTooSmallMeasurements[0].Value.ShouldBe(2);
        statisticsUnavailableMeasurements.Length.ShouldBe(1);
        statisticsUnavailableMeasurements[0].Value.ShouldBe(1);
    }
}
