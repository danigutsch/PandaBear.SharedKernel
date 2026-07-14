using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SharedKernel.Observability.Npgsql;

/// <summary>Defines the bounded OpenTelemetry metric contract for PostgreSQL index-health collection.</summary>
public static class PostgreSqlIndexHealthTelemetry
{
    /// <summary>Gets the stable meter name.</summary>
    public const string MeterName = "SharedKernel.Observability.Npgsql";

    /// <summary>Gets the aggregate index-health assessment metric name.</summary>
    public const string AssessmentMetricName = "postgresql.index_health.assessments";

    /// <summary>Gets the once-per-run index-health collection metric name.</summary>
    public const string CollectionMetricName = "postgresql.index_health.collections";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Assessments = Meter.CreateCounter<long>(
        AssessmentMetricName,
        unit: "{assessment}",
        description: "Aggregate, advisory PostgreSQL index-health assessments.");
    private static readonly Counter<long> Collections = Meter.CreateCounter<long>(
        CollectionMetricName,
        unit: "{collection}",
        description: "PostgreSQL index-health collection outcomes.");

    internal static void Record(PostgreSqlIndexHealthCollectionResult result)
    {
        RecordCollection(result.Outcome);

        foreach (var assessment in result.Assessments)
        {
            RecordAssessment(assessment.Action, assessment.Reason);
        }
    }

    private static void RecordCollection(PostgreSqlIndexHealthCollectionOutcome outcome)
    {
        TagList tags =
        [
            new KeyValuePair<string, object?>("outcome", GetOutcomeTag(outcome)),
        ];

        Collections.Add(1, tags);
    }

    private static void RecordAssessment(
        PostgreSqlIndexHealthRecommendationAction action,
        PostgreSqlIndexHealthRecommendationReason reason)
    {
        TagList tags =
        [
            new KeyValuePair<string, object?>("action", GetActionTag(action)),
            new KeyValuePair<string, object?>("reason", GetReasonTag(reason)),
        ];

        Assessments.Add(1, tags);
    }

    private static string GetOutcomeTag(PostgreSqlIndexHealthCollectionOutcome outcome)
    {
        return outcome switch
        {
            PostgreSqlIndexHealthCollectionOutcome.Collected => "collected",
            PostgreSqlIndexHealthCollectionOutcome.PermissionDenied => "permission_denied",
            PostgreSqlIndexHealthCollectionOutcome.Unsupported => "unsupported",
            PostgreSqlIndexHealthCollectionOutcome.Unavailable => "unavailable",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
        };
    }

    private static string GetActionTag(PostgreSqlIndexHealthRecommendationAction action)
    {
        return action switch
        {
            PostgreSqlIndexHealthRecommendationAction.InsufficientEvidence => "insufficient_evidence",
            PostgreSqlIndexHealthRecommendationAction.ReviewCreation => "review_creation",
            PostgreSqlIndexHealthRecommendationAction.ReviewModification => "review_modification",
            _ => throw new ArgumentOutOfRangeException(nameof(action)),
        };
    }

    private static string GetReasonTag(PostgreSqlIndexHealthRecommendationReason reason)
    {
        return reason switch
        {
            PostgreSqlIndexHealthRecommendationReason.StatisticsUnavailable => "statistics_unavailable",
            PostgreSqlIndexHealthRecommendationReason.StatisticsWindowTooShort => "statistics_window_too_short",
            PostgreSqlIndexHealthRecommendationReason.TableTooSmall => "table_too_small",
            PostgreSqlIndexHealthRecommendationReason.ProtectedIndex => "protected_index",
            PostgreSqlIndexHealthRecommendationReason.UnsupportedIndexShape => "unsupported_index_shape",
            PostgreSqlIndexHealthRecommendationReason.PerObjectStatisticsWindowUnavailable => "per_object_statistics_window_unavailable",
            PostgreSqlIndexHealthRecommendationReason.HighIndexReadVolume => "high_index_read_volume",
            PostgreSqlIndexHealthRecommendationReason.HighSequentialScanVolume => "high_sequential_scan_volume",
            PostgreSqlIndexHealthRecommendationReason.InsufficientActivity => "insufficient_activity",
            _ => throw new ArgumentOutOfRangeException(nameof(reason)),
        };
    }
}
