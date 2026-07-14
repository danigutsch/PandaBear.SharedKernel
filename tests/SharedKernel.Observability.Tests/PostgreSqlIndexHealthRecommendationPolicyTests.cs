using SharedKernel.Testing;

namespace SharedKernel.Observability.Npgsql.Tests;

[Trait(TestTraitNames.ScopeName, TestTraits.UnitScope)]
[Trait(TestTraits.ComponentName, TestTraits.PostgreSqlObservabilityComponent)]
public sealed class PostgreSqlIndexHealthRecommendationPolicyTests
{
    [Fact]
    public void Assess_returns_insufficient_evidence_when_the_statistics_window_is_too_short()
    {
        // Arrange
        var evidence = new PostgreSqlIndexEvidence
        {
            Kind = PostgreSqlIndexEvidenceKind.Index,
            SchemaName = "public",
            TableName = "catalog_items",
            StatisticsWindow = TimeSpan.FromDays(1),
            ScanCount = 0,
            TuplesRead = 0,
            TuplesFetched = 0,
            EstimatedRows = 20_000,
            IsProtected = false,
            IsUsable = true,
            IsSimple = true,
            StatisticsAreReliable = true,
        };

        // Act
        var assessment = PostgreSqlIndexHealthRecommendationPolicy.Assess(evidence);

        // Assert
        assessment.Action.ShouldBe(PostgreSqlIndexHealthRecommendationAction.InsufficientEvidence);
        assessment.Reason.ShouldBe(PostgreSqlIndexHealthRecommendationReason.StatisticsWindowTooShort);
    }

    [Fact]
    public void Assess_does_not_recommend_removing_a_protected_index()
    {
        // Arrange
        var evidence = new PostgreSqlIndexEvidence
        {
            Kind = PostgreSqlIndexEvidenceKind.Index,
            SchemaName = "public",
            TableName = "catalog_items",
            StatisticsWindow = TimeSpan.FromDays(14),
            ScanCount = 0,
            TuplesRead = 0,
            TuplesFetched = 0,
            EstimatedRows = 20_000,
            IsProtected = true,
            IsUsable = true,
            IsSimple = true,
            StatisticsAreReliable = true,
        };

        // Act
        var assessment = PostgreSqlIndexHealthRecommendationPolicy.Assess(evidence);

        // Assert
        assessment.Action.ShouldBe(PostgreSqlIndexHealthRecommendationAction.InsufficientEvidence);
        assessment.Reason.ShouldBe(PostgreSqlIndexHealthRecommendationReason.ProtectedIndex);
    }

    [Fact]
    public void Assess_does_not_recommend_removing_an_index_without_a_per_object_observation_window()
    {
        // Arrange
        var evidence = new PostgreSqlIndexEvidence
        {
            Kind = PostgreSqlIndexEvidenceKind.Index,
            SchemaName = "public",
            TableName = "catalog_items",
            StatisticsWindow = TimeSpan.FromDays(14),
            ScanCount = 0,
            TuplesRead = 0,
            TuplesFetched = 0,
            EstimatedRows = 20_000,
            IsProtected = false,
            IsUsable = true,
            IsSimple = true,
            StatisticsAreReliable = true,
        };

        // Act
        var assessment = PostgreSqlIndexHealthRecommendationPolicy.Assess(evidence);

        // Assert
        assessment.Action.ShouldBe(PostgreSqlIndexHealthRecommendationAction.InsufficientEvidence);
        assessment.Reason.ShouldBe(PostgreSqlIndexHealthRecommendationReason.PerObjectStatisticsWindowUnavailable);
    }

    [Fact]
    public void Assess_does_not_recommend_changing_a_partial_or_expression_index()
    {
        // Arrange
        var evidence = new PostgreSqlIndexEvidence
        {
            Kind = PostgreSqlIndexEvidenceKind.Index,
            SchemaName = "public",
            TableName = "catalog_items",
            StatisticsWindow = TimeSpan.FromDays(14),
            ScanCount = 0,
            TuplesRead = 0,
            TuplesFetched = 0,
            EstimatedRows = 20_000,
            IsProtected = false,
            IsUsable = true,
            IsSimple = false,
            StatisticsAreReliable = true,
        };

        // Act
        var assessment = PostgreSqlIndexHealthRecommendationPolicy.Assess(evidence);

        // Assert
        assessment.Action.ShouldBe(PostgreSqlIndexHealthRecommendationAction.InsufficientEvidence);
        assessment.Reason.ShouldBe(PostgreSqlIndexHealthRecommendationReason.UnsupportedIndexShape);
    }

    [Fact]
    public void Assess_recommends_human_review_for_high_index_read_volume()
    {
        // Arrange
        var evidence = new PostgreSqlIndexEvidence
        {
            Kind = PostgreSqlIndexEvidenceKind.Index,
            SchemaName = "public",
            TableName = "catalog_items",
            StatisticsWindow = TimeSpan.FromDays(14),
            ScanCount = 100,
            TuplesRead = 200_000,
            TuplesFetched = 100,
            EstimatedRows = 20_000,
            IsProtected = false,
            IsUsable = true,
            IsSimple = true,
            StatisticsAreReliable = true,
        };

        // Act
        var assessment = PostgreSqlIndexHealthRecommendationPolicy.Assess(evidence);

        // Assert
        assessment.Action.ShouldBe(PostgreSqlIndexHealthRecommendationAction.ReviewModification);
        assessment.Reason.ShouldBe(PostgreSqlIndexHealthRecommendationReason.HighIndexReadVolume);
    }

    [Fact]
    public void Assess_recommends_human_review_for_high_sequential_scan_volume()
    {
        // Arrange
        var evidence = new PostgreSqlIndexEvidence
        {
            Kind = PostgreSqlIndexEvidenceKind.Table,
            SchemaName = "public",
            TableName = "catalog_items",
            StatisticsWindow = TimeSpan.FromDays(14),
            ScanCount = 1_000,
            TuplesRead = 200_000,
            TuplesFetched = 0,
            EstimatedRows = 20_000,
            IsProtected = false,
            IsUsable = true,
            IsSimple = true,
            StatisticsAreReliable = true,
        };

        // Act
        var assessment = PostgreSqlIndexHealthRecommendationPolicy.Assess(evidence);

        // Assert
        assessment.Action.ShouldBe(PostgreSqlIndexHealthRecommendationAction.ReviewCreation);
        assessment.Reason.ShouldBe(PostgreSqlIndexHealthRecommendationReason.HighSequentialScanVolume);
    }
}
