using Microsoft.CodeAnalysis;

namespace SharedKernel.Mediator.SourceGenerator;

/// <summary>
/// Describes a request handler discovered for a request contract.
/// </summary>
internal sealed record HandlerDescriptor(
    string MetadataName,
    string RequestMetadataName,
    string ResponseMetadataName,
    string MethodName,
    Accessibility Accessibility,
    bool IsAccessibleToGeneratedMediator,
    bool HasCompatibleHandleMethod,
    HandlerKind Kind);
