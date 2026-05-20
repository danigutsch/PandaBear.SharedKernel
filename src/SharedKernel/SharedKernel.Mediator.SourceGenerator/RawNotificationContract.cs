namespace SharedKernel.Mediator.SourceGenerator;

/// <summary>
/// Captures the discovered notification contract before handler projection.
/// </summary>
internal sealed record RawNotificationContract(
    string MetadataName,
    string AssemblyName,
    bool PublishInParallel);
