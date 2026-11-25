using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace SharedKernel.Observability.Npgsql;

/// <summary>Defines the bounded OpenTelemetry metric contract for PostgreSQL index-health collection.</summary>
public static class PostgreSqlIndexHealthTelemetry
{
    /// <summary>Gets the stable meter name.</summary>
    public const string MeterName = "SharedKernel.Observability.Npgsql";

    /// <summary>Gets the aggregate index-health assessment metric name.</summary>
    public const string AssessmentMetricName = "sharedkernel.index_health.assessments";

    /// <summary>Gets the once-per-run index-health collection metric name.</summary>
    public const string CollectionMetricName = "sharedkernel.index_health.collections";

    private const string OutcomeTag = "sharedkernel.index_health.outcome";
    private const string ActionTag = "sharedkernel.index_health.action";
    private const string ReasonTag = "sharedkernel.index_health.reason";
    private const string DbSystemNameTag = "db.system.name";
    private const string DbSystemName = "postgresql";

    private static readonly Meter Meter = new(MeterName);
    private static readonly Counter<long> Assessments = Meter.CreateCounter<long>(
        AssessmentMetricName,
        unit: "{assessment}",
        description: "Aggregate, advisory PostgreSQL index-health assessments.");
    private static readonly Counter<long> Collections = Meter.CreateCounter<long>(
        CollectionMetricName,
        unit: "{collection}",
        description: "PostgreSQL index-health collection outcomes.");

    /// <summary>Records the collection outcome and the aggregate assessments of one collection run.</summary>
    internal static void Record(PostgreSqlIndexHealthCollectionResult result)
    {
        RecordCollection(result.Outcome);

        foreach (var assessmentGroup in result.Assessments.GroupBy(assessment => (assessment.Action, assessment.Reason)))
        {
            RecordAssessment(
                assessmentGroup.Key.Action,
                assessmentGroup.Key.Reason,
                assessmentGroup.LongCount());
        }
    }

    private static void RecordCollection(PostgreSqlIndexHealthCollectionOutcome outcome)
    {
        TagList tags =
        [
            new KeyValuePair<string, object?>(OutcomeTag, GetOutcomeTag(outcome)),
            new KeyValuePair<string, object?>(DbSystemNameTag, DbSystemName),
        ];

        Collections.Add(1, tags);
    }

    private static void RecordAssessment(
        PostgreSqlIndexHealthRecommendationAction action,
        PostgreSqlIndexHealthRecommendationReason reason,
        long count)
    {
        TagList tags =
        [
            new KeyValuePair<string, object?>(ActionTag, GetActionTag(action)),
            new KeyValuePair<string, object?>(ReasonTag, GetReasonTag(reason)),
            new KeyValuePair<string, object?>(DbSystemNameTag, DbSystemName),
        ];

        Assessments.Add(count, tags);
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
