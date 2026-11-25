using System.Collections.Immutable;

namespace SharedKernel.Mediator.SourceGenerator;

/// <summary>
/// Describes a discovered notification contract.
/// </summary>
internal sealed record NotificationDescriptor(
    string MetadataName,
    string Name,
    string AssemblyName,
    bool PublishInParallel,
    ImmutableArray<NotificationHandlerDescriptor> Handlers);
