namespace SharedKernel.Observability.Npgsql;

/// <summary>Classifies the advisory action supported by index-health evidence.</summary>
public enum PostgreSqlIndexHealthRecommendationAction
{
    /// <summary>Evidence is not sufficient to support an index change review.</summary>
    InsufficientEvidence,

    /// <summary>A human may review whether a new index is warranted.</summary>
    ReviewCreation,

    /// <summary>A human may review whether an existing index needs a different design.</summary>
    ReviewModification,
}
