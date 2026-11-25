using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace SharedKernel.Mediator.SourceGenerator;

/// <summary>
/// Captures raw pipeline discovery data before request-specific binding occurs.
/// </summary>
internal sealed record RawPipelineDescriptor(
    string MetadataName,
    string? OpenGenericMetadataName,
    int Stage,
    int Order,
    PipelineApplicability Applicability,
    bool IsAccessibleToGeneratedMediator,
    bool IsStream,
    string RequestMetadataName,
    string ResponseMetadataName,
    INamedTypeSymbol TypeSymbol,
    ITypeSymbol RequestTypePattern,
    ITypeSymbol ResponseTypePattern,
    ImmutableArray<ITypeParameterSymbol> TypeParameters);
