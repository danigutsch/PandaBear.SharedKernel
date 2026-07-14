namespace SharedKernel.Observability.Npgsql;

/// <summary>Applies a conservative, side-effect-free policy to PostgreSQL index-health evidence.</summary>
public static class PostgreSqlIndexHealthRecommendationPolicy
{
    private static readonly TimeSpan MinimumStatisticsWindow = TimeSpan.FromDays(7);
    private const long MinimumEstimatedRows = 10_000;
    private const long MinimumSequentialScans = 1_000;
    private const long MinimumIndexScans = 100;
    private const long MinimumTupleReadMultiplier = 10;

    /// <summary>Assesses evidence without creating, changing, or removing a database object.</summary>
    /// <param name="evidence">The read-only PostgreSQL catalog evidence to assess.</param>
    /// <returns>An advisory action and bounded reason for the evidence.</returns>
    public static PostgreSqlIndexHealthAssessment Assess(PostgreSqlIndexEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        var reason = GetInsufficientEvidenceReason(evidence);
        if (reason is not null)
        {
            return new PostgreSqlIndexHealthAssessment(
                evidence,
                PostgreSqlIndexHealthRecommendationAction.InsufficientEvidence,
                reason.Value);
        }

        return evidence.Kind switch
        {
            PostgreSqlIndexEvidenceKind.Index => AssessIndex(evidence),
            PostgreSqlIndexEvidenceKind.Table => AssessTable(evidence),
            _ => throw new ArgumentOutOfRangeException(nameof(evidence)),
        };
    }

    private static PostgreSqlIndexHealthRecommendationReason? GetInsufficientEvidenceReason(PostgreSqlIndexEvidence evidence)
    {
        if (evidence.StatisticsWindow is null)
        {
            return PostgreSqlIndexHealthRecommendationReason.StatisticsUnavailable;
        }

        if (!evidence.StatisticsAreReliable)
        {
            return PostgreSqlIndexHealthRecommendationReason.StatisticsWindowTooShort;
        }

        if (evidence.StatisticsWindow < MinimumStatisticsWindow)
        {
            return PostgreSqlIndexHealthRecommendationReason.StatisticsWindowTooShort;
        }

        if (evidence.EstimatedRows < MinimumEstimatedRows)
        {
            return PostgreSqlIndexHealthRecommendationReason.TableTooSmall;
        }

        if (evidence.Kind is PostgreSqlIndexEvidenceKind.Index && evidence.IsProtected)
        {
            return PostgreSqlIndexHealthRecommendationReason.ProtectedIndex;
        }

        if (evidence.Kind is PostgreSqlIndexEvidenceKind.Index && (!evidence.IsUsable || !evidence.IsSimple))
        {
            return PostgreSqlIndexHealthRecommendationReason.UnsupportedIndexShape;
        }

        return null;
    }

    private static PostgreSqlIndexHealthAssessment AssessIndex(PostgreSqlIndexEvidence evidence)
    {
        if (evidence.ScanCount == 0)
        {
            return new PostgreSqlIndexHealthAssessment(
                evidence,
                PostgreSqlIndexHealthRecommendationAction.InsufficientEvidence,
                PostgreSqlIndexHealthRecommendationReason.PerObjectStatisticsWindowUnavailable);
        }

        if (evidence.ScanCount >= MinimumIndexScans && HasHighTupleReadVolume(evidence))
        {
            return new PostgreSqlIndexHealthAssessment(
                evidence,
                PostgreSqlIndexHealthRecommendationAction.ReviewModification,
                PostgreSqlIndexHealthRecommendationReason.HighIndexReadVolume);
        }

        return new PostgreSqlIndexHealthAssessment(
            evidence,
            PostgreSqlIndexHealthRecommendationAction.InsufficientEvidence,
            PostgreSqlIndexHealthRecommendationReason.InsufficientActivity);
    }

    private static PostgreSqlIndexHealthAssessment AssessTable(PostgreSqlIndexEvidence evidence)
    {
        if (evidence.ScanCount >= MinimumSequentialScans && HasHighTupleReadVolume(evidence))
        {
            return new PostgreSqlIndexHealthAssessment(
                evidence,
                PostgreSqlIndexHealthRecommendationAction.ReviewCreation,
                PostgreSqlIndexHealthRecommendationReason.HighSequentialScanVolume);
        }

        return new PostgreSqlIndexHealthAssessment(
            evidence,
            PostgreSqlIndexHealthRecommendationAction.InsufficientEvidence,
            PostgreSqlIndexHealthRecommendationReason.InsufficientActivity);
    }

    private static bool HasHighTupleReadVolume(PostgreSqlIndexEvidence evidence)
    {
        return evidence.TuplesRead >= MinimumTupleReadMultiplier
            && evidence.TuplesRead / MinimumTupleReadMultiplier >= evidence.EstimatedRows;
    }
}
