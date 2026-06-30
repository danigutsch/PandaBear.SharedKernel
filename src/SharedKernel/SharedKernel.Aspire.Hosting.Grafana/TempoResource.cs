namespace Aspire.Hosting.ApplicationModel;

/// <summary>
/// Represents a Tempo trace backend resource in an Aspire AppHost model.
/// </summary>
public sealed class TempoResource([ResourceName] string name) : ContainerResource(name);
