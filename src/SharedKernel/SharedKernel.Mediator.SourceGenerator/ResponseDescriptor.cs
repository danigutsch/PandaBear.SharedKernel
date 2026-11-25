using System.Collections.Immutable;

namespace SharedKernel.Mediator.SourceGenerator;

/// <summary>
/// Describes a discovered request or stream response type.
/// </summary>
internal sealed record ResponseDescriptor(
    string MetadataName,
    bool IsConstructedGenericType,
    string? GenericTypeDefinitionMetadataName,
    ImmutableArray<string> TypeArguments,
    ImmutableArray<string> Interfaces);
