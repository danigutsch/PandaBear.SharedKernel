namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Loki log backend resource in an Aspire AppHost model.
/// </summary>
public sealed class LokiResource([ResourceName] string name) : ContainerResource(name);
