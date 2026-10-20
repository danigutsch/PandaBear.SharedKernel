using System.Collections.Immutable;

namespace PandaBear.Mediator.SourceGenerator;

/// <summary>
/// Describes a discovered request contract.
/// </summary>
internal sealed record RequestDescriptor(
    string MetadataName,
    string Name,
    string AssemblyName,
    RequestKind Kind,
    ResponseDescriptor Response,
    bool IsValueType,
    ImmutableArray<HandlerDescriptor> Handlers,
    ImmutableArray<PipelineDescriptor> Pipelines);
