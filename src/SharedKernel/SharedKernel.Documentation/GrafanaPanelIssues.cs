namespace SharedKernel.Documentation;

/// <summary>Reasons a dashboard panel failed verification.</summary>
[Flags]
public enum GrafanaPanelIssues
{
    /// <summary>Verification found no issues.</summary>
    None = 0,

    /// <summary>A required panel was absent from the dashboard observation.</summary>
    Missing = 1,

    /// <summary>The panel had no visualization or recognized empty state.</summary>
    NotDrawn = 2,

    /// <summary>The panel showed a query error.</summary>
    QueryError = 4,

    /// <summary>A panel that requires data showed its empty state.</summary>
    NoData = 8,
}
