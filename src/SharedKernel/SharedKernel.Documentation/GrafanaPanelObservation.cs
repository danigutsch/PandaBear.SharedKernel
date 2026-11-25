namespace SharedKernel.Documentation;

/// <summary>A host observation of a rendered Grafana panel.</summary>
/// <param name="Title">The title reported by the dashboard.</param>
/// <param name="Drawn">Whether the panel rendered a visualization or a recognized empty state.</param>
/// <param name="NoData">Whether the panel showed its empty state.</param>
/// <param name="QueryError">Whether the panel showed a query error.</param>
public sealed record GrafanaPanelObservation(string Title, bool Drawn, bool NoData, bool QueryError);
