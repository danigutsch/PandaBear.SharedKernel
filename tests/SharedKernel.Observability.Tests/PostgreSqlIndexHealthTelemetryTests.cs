using System.Collections.Concurrent;
using SharedKernel.Testing;

namespace SharedKernel.Observability.Npgsql.Tests;

[Trait(TestTraitNames.ScopeName, TestTraits.UnitScope)]
[Trait(TestTraits.ComponentName, TestTraits.PostgreSqlObservabilityComponent)]
[Collection("PostgreSQL index health telemetry")]
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
        var thirdTableTooSmallEvidence = tableTooSmallEvidence with { TableName = "destinations" };
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
                    PostgreSqlIndexHealthRecommendationAction.ReviewCreation,
                    PostgreSqlIndexHealthRecommendationReason.TableTooSmall),
                new PostgreSqlIndexHealthAssessment(
                    thirdTableTooSmallEvidence,
                    PostgreSqlIndexHealthRecommendationAction.InsufficientEvidence,
                    PostgreSqlIndexHealthRecommendationReason.TableTooSmall),
            ]);
        var measurements = new ConcurrentQueue<(string InstrumentName, long Value, IReadOnlyDictionary<string, string?> Tags)>();
        using var meterListener = PostgreSqlIndexHealthTelemetryTestListener.Create(measurement => measurements.Enqueue(measurement));

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
        var reviewCreationTableTooSmallMeasurements = assessmentMeasurements
            .Where(measurement => measurement.Tags.TryGetValue("action", out var action)
                && action == "review_creation"
                && measurement.Tags.TryGetValue("reason", out var reason)
                && reason == "table_too_small")
            .ToArray();

        // Assert
        assessmentMeasurements.ShouldHaveCount(3);
        var tableTooSmallMeasurement = tableTooSmallMeasurements.ShouldHaveSingleItem();
        tableTooSmallMeasurement.Value.ShouldBe(2);
        var statisticsUnavailableMeasurement = statisticsUnavailableMeasurements.ShouldHaveSingleItem();
        statisticsUnavailableMeasurement.Value.ShouldBe(1);
        var reviewCreationTableTooSmallMeasurement = reviewCreationTableTooSmallMeasurements.ShouldHaveSingleItem();
        reviewCreationTableTooSmallMeasurement.Value.ShouldBe(1);
    }

    [Fact]
    public void Record_emits_all_bounded_outcome_action_and_reason_tags_without_identifiers()
    {
        // Arrange
        var evidence = new PostgreSqlIndexEvidence
        {
            Kind = PostgreSqlIndexEvidenceKind.Index,
            SchemaName = "private_schema",
            TableName = "customer_contact_details",
            IndexName = "customer_contact_details_email_idx",
            StatisticsWindow = TimeSpan.FromDays(14),
            ScanCount = 1_000,
            TuplesRead = 200_000,
            TuplesFetched = 1_000,
            EstimatedRows = 20_000,
            IsProtected = false,
            IsUsable = true,
            IsSimple = true,
            StatisticsAreReliable = true,
        };
        var assessments = new PostgreSqlIndexHealthAssessment[]
        {
            new(evidence, PostgreSqlIndexHealthRecommendationAction.InsufficientEvidence, PostgreSqlIndexHealthRecommendationReason.StatisticsUnavailable),
            new(evidence, PostgreSqlIndexHealthRecommendationAction.InsufficientEvidence, PostgreSqlIndexHealthRecommendationReason.StatisticsWindowTooShort),
            new(evidence, PostgreSqlIndexHealthRecommendationAction.InsufficientEvidence, PostgreSqlIndexHealthRecommendationReason.TableTooSmall),
            new(evidence, PostgreSqlIndexHealthRecommendationAction.InsufficientEvidence, PostgreSqlIndexHealthRecommendationReason.ProtectedIndex),
            new(evidence, PostgreSqlIndexHealthRecommendationAction.InsufficientEvidence, PostgreSqlIndexHealthRecommendationReason.UnsupportedIndexShape),
            new(evidence, PostgreSqlIndexHealthRecommendationAction.InsufficientEvidence, PostgreSqlIndexHealthRecommendationReason.PerObjectStatisticsWindowUnavailable),
            new(evidence, PostgreSqlIndexHealthRecommendationAction.ReviewModification, PostgreSqlIndexHealthRecommendationReason.HighIndexReadVolume),
            new(evidence, PostgreSqlIndexHealthRecommendationAction.ReviewCreation, PostgreSqlIndexHealthRecommendationReason.HighSequentialScanVolume),
            new(evidence, PostgreSqlIndexHealthRecommendationAction.InsufficientEvidence, PostgreSqlIndexHealthRecommendationReason.InsufficientActivity),
        };
        var measurements = new ConcurrentQueue<(string InstrumentName, long Value, IReadOnlyDictionary<string, string?> Tags)>();
        using var meterListener = PostgreSqlIndexHealthTelemetryTestListener.Create(measurement => measurements.Enqueue(measurement));

        // Act
        foreach (var outcome in Enum.GetValues<PostgreSqlIndexHealthCollectionOutcome>())
        {
            PostgreSqlIndexHealthTelemetry.Record(new PostgreSqlIndexHealthCollectionResult(outcome, []));
        }

        PostgreSqlIndexHealthTelemetry.Record(
            new PostgreSqlIndexHealthCollectionResult(PostgreSqlIndexHealthCollectionOutcome.Collected, assessments));
        var collectionMeasurements = measurements
            .Where(measurement => measurement.InstrumentName == PostgreSqlIndexHealthTelemetry.CollectionMetricName)
            .ToArray();
        var assessmentMeasurements = measurements
            .Where(measurement => measurement.InstrumentName == PostgreSqlIndexHealthTelemetry.AssessmentMetricName)
            .ToArray();
        var renderedTags = string.Join(
            ",",
            measurements.SelectMany(measurement => measurement.Tags.Select(tag => $"{tag.Key}={tag.Value}")));
        var collectionOutcomes = collectionMeasurements.Select(measurement => measurement.Tags["outcome"]).Distinct().ToArray();
        var assessmentActions = assessmentMeasurements.Select(measurement => measurement.Tags["action"]).Distinct().ToArray();
        var assessmentReasons = assessmentMeasurements.Select(measurement => measurement.Tags["reason"]).Distinct().ToArray();
        var collectionTagsAreBounded = collectionMeasurements.All(measurement => measurement.Tags.Count == 1 && measurement.Tags.ContainsKey("outcome"));
        var assessmentTagsAreBounded = assessmentMeasurements.All(
            measurement => measurement.Tags.Count == 2
                && measurement.Tags.ContainsKey("action")
                && measurement.Tags.ContainsKey("reason"));

        // Assert
        collectionOutcomes.ShouldHaveCount(4);
        collectionOutcomes.ShouldContain("collected");
        collectionOutcomes.ShouldContain("permission_denied");
        collectionOutcomes.ShouldContain("unsupported");
        collectionOutcomes.ShouldContain("unavailable");
        assessmentActions.ShouldHaveCount(3);
        assessmentActions.ShouldContain("insufficient_evidence");
        assessmentActions.ShouldContain("review_creation");
        assessmentActions.ShouldContain("review_modification");
        assessmentReasons.ShouldHaveCount(9);
        assessmentReasons.ShouldContain("statistics_unavailable");
        assessmentReasons.ShouldContain("statistics_window_too_short");
        assessmentReasons.ShouldContain("table_too_small");
        assessmentReasons.ShouldContain("protected_index");
        assessmentReasons.ShouldContain("unsupported_index_shape");
        assessmentReasons.ShouldContain("per_object_statistics_window_unavailable");
        assessmentReasons.ShouldContain("high_index_read_volume");
        assessmentReasons.ShouldContain("high_sequential_scan_volume");
        assessmentReasons.ShouldContain("insufficient_activity");
        collectionTagsAreBounded.ShouldBeTrue();
        assessmentTagsAreBounded.ShouldBeTrue();
        renderedTags.ShouldNotContain("private_schema", StringComparison.Ordinal);
        renderedTags.ShouldNotContain("customer_contact_details", StringComparison.Ordinal);
        renderedTags.ShouldNotContain("customer_contact_details_email_idx", StringComparison.Ordinal);
    }
}
