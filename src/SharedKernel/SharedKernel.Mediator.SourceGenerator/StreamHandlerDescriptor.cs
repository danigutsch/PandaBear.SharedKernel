using Microsoft.CodeAnalysis;

namespace SharedKernel.Mediator.SourceGenerator;

/// <summary>
/// Describes a handler discovered for a stream request contract.
/// </summary>
internal sealed record StreamHandlerDescriptor(
    string MetadataName,
    string RequestMetadataName,
    string MethodName,
    Accessibility Accessibility,
    bool IsAccessibleToGeneratedMediator,
    bool HasCompatibleHandleMethod);
