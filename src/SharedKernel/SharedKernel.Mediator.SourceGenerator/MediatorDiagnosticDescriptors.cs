using Microsoft.CodeAnalysis;

namespace SharedKernel.Mediator;

/// <summary>
/// Central diagnostic descriptors used by mediator generation, analysis, and code fixes.
/// </summary>
internal static class MediatorDiagnosticDescriptors
{
    private const string UsageCategory = "Usage";
    private const string ArchitectureCategory = "Architecture";

    public static readonly DiagnosticDescriptor MissingHandler = new(
        id: MediatorDiagnosticIds.MissingHandler,
        title: "Request has no handler",
        messageFormat: "Request '{0}' does not have an accessible compatible handler",
        category: UsageCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MultipleHandlers = new(
        id: MediatorDiagnosticIds.MultipleHandlers,
        title: "Request has multiple handlers",
        messageFormat: "Request '{0}' has {1} accessible compatible handlers",
        category: UsageCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidHandlerSignature = new(
        id: MediatorDiagnosticIds.InvalidHandlerSignature,
        title: "Handler has invalid signature",
        messageFormat: "Handler '{0}' does not expose a compatible public Handle method for request '{1}'",
        category: UsageCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingCancellationToken = new(
        id: MediatorDiagnosticIds.MissingCancellationToken,
        title: "Handler is missing CancellationToken ct",
        messageFormat: "Handler '{0}' does not expose a public Handle method that accepts CancellationToken ct for request '{1}'",
        category: UsageCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor HandlerReturnTypeMismatch = new(
        id: MediatorDiagnosticIds.HandlerReturnTypeMismatch,
        title: "Handler return type does not match request response type",
        messageFormat: "Handler '{0}' returns '{1}' but request '{2}' requires '{3}'",
        category: UsageCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingCancellationForwarding = new(
        id: MediatorDiagnosticIds.MissingCancellationForwarding,
        title: "Mediator call does not pass available CancellationToken ct",
        messageFormat: "Call '{0}' should forward available CancellationToken '{1}'",
        category: UsageCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingEnumeratorCancellation = new(
        id: MediatorDiagnosticIds.MissingEnumeratorCancellation,
        title: "Async stream Handle method is missing [EnumeratorCancellation]",
        messageFormat: "Async stream Handle method '{0}' should annotate CancellationToken '{1}' with [EnumeratorCancellation]",
        category: UsageCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NonIteratorStreamHandlerHasCancellationToken = new(
        id: MediatorDiagnosticIds.NonIteratorStreamHandlerHasCancellationToken,
        title: "CancellationToken parameter has no effect in non-iterator stream handler",
        messageFormat: "CancellationToken parameter '{0}' in non-iterator stream handler '{1}' has no effect via WithCancellation(); pass the token explicitly to the inner IAsyncEnumerable<T>",
        category: UsageCategory,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InaccessibleRegistrationType = new(
        id: MediatorDiagnosticIds.InaccessibleRegistrationType,
        title: "Mediator registration type is inaccessible",
        messageFormat: "Type '{0}' is inaccessible to generated mediator registrations",
        category: UsageCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingModuleMarker = new(
        id: MediatorDiagnosticIds.MissingModuleMarker,
        title: "Handler module is not marked with [assembly: MediatorModule]",
        messageFormat: "Assembly '{0}' contains mediator registrations but is not marked with [assembly: MediatorModule]",
        category: UsageCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateGeneratedRegistration = new(
        id: MediatorDiagnosticIds.DuplicateGeneratedRegistration,
        title: "Generated mediator registration is duplicated",
        messageFormat: "Generated mediator registration '{0}' implemented by '{1}' is duplicated",
        category: UsageCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnprovenObjectDispatchCoverage = new(
        id: MediatorDiagnosticIds.UnprovenObjectDispatchCoverage,
        title: "Generated object dispatch coverage cannot be proven",
        messageFormat: "Generated object dispatch coverage cannot be proven because assembly '{0}' is not marked with [assembly: MediatorModule]",
        category: UsageCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor InvalidPipelineGenericArity = new(
        id: MediatorDiagnosticIds.InvalidPipelineGenericArity,
        title: "Pipeline behavior has invalid generic arity",
        messageFormat: "Pipeline behavior '{0}' declares {1} type parameter(s); open generic pipeline behaviors must declare exactly two type parameters",
        category: UsageCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicatePipelineOrder = new(
        id: MediatorDiagnosticIds.DuplicatePipelineOrder,
        title: "Duplicate pipeline order",
        messageFormat: "Request '{0}' has multiple applicable pipeline behaviors with stage {1} and order {2}",
        category: UsageCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NeverAppliesPipeline = new(
        id: MediatorDiagnosticIds.NeverAppliesPipeline,
        title: "Pipeline behavior is registered but never applies",
        messageFormat: "Pipeline behavior '{0}' is registered but does not apply to any discovered request",
        category: UsageCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnboundPipelineConstraints = new(
        id: MediatorDiagnosticIds.UnboundPipelineConstraints,
        title: "Pipeline behavior constraints cannot bind to any request",
        messageFormat: "Pipeline behavior '{0}' constraints cannot bind to any discovered request/response pair",
        category: UsageCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NotificationHandlersRequireExplicitOrder = new(
        id: MediatorDiagnosticIds.NotificationHandlersRequireExplicitOrder,
        title: "Notification handlers require explicit order",
        messageFormat: "Notification '{0}' has multiple sequential handlers and handler '{1}' does not declare [NotificationOrder]",
        category: UsageCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor DuplicateNotificationHandlerOrder = new(
        id: MediatorDiagnosticIds.DuplicateNotificationHandlerOrder,
        title: "Duplicate notification handler order",
        messageFormat: "Notification '{0}' has multiple handlers with explicit order {1}",
        category: UsageCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor HandlerShouldNotCallSender = new(
        id: MediatorDiagnosticIds.HandlerShouldNotCallSender,
        title: "Handler should not call mediator send APIs directly",
        messageFormat: "Handler '{0}' should not call mediator send APIs directly",
        category: ArchitectureCategory,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);
}
