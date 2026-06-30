namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Prometheus metric backend resource in an Aspire AppHost model.
/// </summary>
public sealed class PrometheusResource([ResourceName] string name) : ContainerResource(name);
