using System.Collections.Immutable;

namespace PandaBear.Mediator.SourceGenerator;

/// <summary>
/// Describes a discovered stream request contract.
/// </summary>
internal sealed record StreamRequestDescriptor(
    string MetadataName,
    string Name,
    string AssemblyName,
    ResponseDescriptor ItemResponse,
    ImmutableArray<StreamHandlerDescriptor> Handlers,
    ImmutableArray<PipelineDescriptor> Pipelines);
