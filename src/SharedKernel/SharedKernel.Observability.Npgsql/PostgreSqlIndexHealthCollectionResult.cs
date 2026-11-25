namespace SharedKernel.Observability.Npgsql;

/// <summary>Contains the outcome and individual advisory assessments from a collection attempt.</summary>
public sealed record PostgreSqlIndexHealthCollectionResult(
    PostgreSqlIndexHealthCollectionOutcome Outcome,
    IReadOnlyList<PostgreSqlIndexHealthAssessment> Assessments);
