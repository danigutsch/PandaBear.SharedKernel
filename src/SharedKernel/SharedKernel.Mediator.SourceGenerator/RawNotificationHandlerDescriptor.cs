using Microsoft.CodeAnalysis;

namespace SharedKernel.Mediator.SourceGenerator;

/// <summary>
/// Captures a discovered notification handler before final projection.
/// </summary>
internal sealed record RawNotificationHandlerDescriptor(
    string MetadataName,
    string Namespace,
    string Name,
    string NotificationMetadataName,
    string MethodName,
    Accessibility Accessibility,
    bool IsAccessibleToGeneratedMediator,
    int Order,
    bool HasExplicitOrder,
    Location? DiagnosticLocation);
