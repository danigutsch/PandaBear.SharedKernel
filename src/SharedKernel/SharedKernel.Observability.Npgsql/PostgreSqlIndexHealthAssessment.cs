namespace SharedKernel.Observability.Npgsql;

/// <summary>Pairs advisory evidence with the conservative action and reason it supports.</summary>
public sealed record PostgreSqlIndexHealthAssessment(
    PostgreSqlIndexEvidence Evidence,
    PostgreSqlIndexHealthRecommendationAction Action,
    PostgreSqlIndexHealthRecommendationReason Reason);
