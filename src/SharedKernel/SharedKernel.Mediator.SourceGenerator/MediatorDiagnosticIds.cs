namespace SharedKernel.Mediator;

/// <summary>
/// Central diagnostic identifiers used by the mediator generator and related tooling.
/// </summary>
internal static class MediatorDiagnosticIds
{
    public const string MissingHandler = "SKMED001";
    public const string MultipleHandlers = "SKMED002";
    public const string InvalidHandlerSignature = "SKMED003";
    public const string MissingCancellationToken = "SKMED004";
    public const string HandlerReturnTypeMismatch = "SKMED005";
    public const string MissingCancellationForwarding = "SKMED006";
    public const string MissingEnumeratorCancellation = "SKMED007";
    public const string NonIteratorStreamHandlerHasCancellationToken = "SKMED008";
    public const string InaccessibleRegistrationType = "SKMED010";
    public const string MissingModuleMarker = "SKMED011";
    public const string DuplicateGeneratedRegistration = "SKMED012";
    public const string UnprovenObjectDispatchCoverage = "SKMED013";
    public const string NotificationHandlersRequireExplicitOrder = "SKMED200";
    public const string DuplicateNotificationHandlerOrder = "SKMED201";
    public const string InvalidPipelineGenericArity = "SKMED020";
    public const string DuplicatePipelineOrder = "SKMED021";
    public const string NeverAppliesPipeline = "SKMED022";
    public const string UnboundPipelineConstraints = "SKMED023";
    public const string HandlerShouldNotCallSender = "SKMED500";
}
