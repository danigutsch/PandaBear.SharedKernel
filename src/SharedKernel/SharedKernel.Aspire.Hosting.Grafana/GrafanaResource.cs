namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Grafana dashboard resource in an Aspire AppHost model.
/// </summary>
public sealed class GrafanaResource([ResourceName] string name) : ContainerResource(name);
