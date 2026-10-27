namespace PandaBear.Mediator;

/// <summary>
/// Central diagnostic identifiers used by the mediator generator and related tooling.
/// </summary>
internal static class MediatorDiagnosticIds
{
    /// <summary>Diagnostic ID for: Request has no handler.</summary>
    public const string MissingHandler = "SKMED001";
    /// <summary>Diagnostic ID for: Request has multiple handlers.</summary>
    public const string MultipleHandlers = "SKMED002";
    /// <summary>Diagnostic ID for: Handler has invalid signature.</summary>
    public const string InvalidHandlerSignature = "SKMED003";
    /// <summary>Diagnostic ID for: Handler is missing the CancellationToken parameter.</summary>
    public const string MissingCancellationToken = "SKMED004";
    /// <summary>Diagnostic ID for: Handler return type does not match the request response type.</summary>
    public const string HandlerReturnTypeMismatch = "SKMED005";
    /// <summary>Diagnostic ID for: Mediator call does not pass the available CancellationToken.</summary>
    public const string MissingCancellationForwarding = "SKMED006";
    /// <summary>Diagnostic ID for: Async stream Handle method is missing [EnumeratorCancellation].</summary>
    public const string MissingEnumeratorCancellation = "SKMED007";
    /// <summary>Diagnostic ID for: CancellationToken parameter has no effect in a non-iterator stream handler.</summary>
    public const string NonIteratorStreamHandlerHasCancellationToken = "SKMED008";
    /// <summary>Diagnostic ID for: Mediator registration type is inaccessible.</summary>
    public const string InaccessibleRegistrationType = "SKMED010";
    /// <summary>Diagnostic ID for: Handler module is not marked with the MediatorModule assembly attribute.</summary>
    public const string MissingModuleMarker = "SKMED011";
    /// <summary>Diagnostic ID for: Generated mediator registration is duplicated.</summary>
    public const string DuplicateGeneratedRegistration = "SKMED012";
    /// <summary>Diagnostic ID for: Generated object dispatch coverage cannot be proven.</summary>
    public const string UnprovenObjectDispatchCoverage = "SKMED013";
    /// <summary>Diagnostic ID for: Notification handlers require explicit order.</summary>
    public const string NotificationHandlersRequireExplicitOrder = "SKMED200";
    /// <summary>Diagnostic ID for: Duplicate notification handler order.</summary>
    public const string DuplicateNotificationHandlerOrder = "SKMED201";
    /// <summary>Diagnostic ID for: Pipeline behavior has invalid generic arity.</summary>
    public const string InvalidPipelineGenericArity = "SKMED020";
    /// <summary>Diagnostic ID for: Duplicate pipeline order.</summary>
    public const string DuplicatePipelineOrder = "SKMED021";
    /// <summary>Diagnostic ID for: Pipeline behavior is registered but never applies.</summary>
    public const string NeverAppliesPipeline = "SKMED022";
    /// <summary>Diagnostic ID for: Pipeline behavior constraints cannot bind to any request.</summary>
    public const string UnboundPipelineConstraints = "SKMED023";
    /// <summary>Diagnostic ID for: Handler should not call mediator send APIs directly.</summary>
    public const string HandlerShouldNotCallSender = "SKMED500";
}
