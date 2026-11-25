using Microsoft.CodeAnalysis;

namespace SharedKernel.Mediator.SourceGenerator;

/// <summary>
/// Captures raw stream request data before handlers and pipelines are attached.
/// </summary>
internal sealed record RawStreamRequestContract(
    string MetadataName,
    ResponseDescriptor ItemResponse,
    INamedTypeSymbol TypeSymbol,
    ITypeSymbol ItemResponseTypeSymbol,
    bool IsInPrimaryAssembly,
    Location? Location);
