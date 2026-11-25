namespace SharedKernel.Documentation;

/// <summary>The identifying information and query of a configured Grafana target.</summary>
/// <param name="PanelId">The panel ID from the dashboard.</param>
/// <param name="Title">The panel title.</param>
/// <param name="RefId">The target's reference ID within the panel.</param>
/// <param name="DatasourceType">The datasource plugin type.</param>
/// <param name="DatasourceUid">The datasource UID.</param>
/// <param name="Query">The original query expression.</param>
/// <param name="VerificationMode">The verification mode from the caller's dashboard format.</param>
public sealed record GrafanaQueryTarget(
    int PanelId,
    string Title,
    string RefId,
    string DatasourceType,
    string DatasourceUid,
    string Query,
    string VerificationMode);
