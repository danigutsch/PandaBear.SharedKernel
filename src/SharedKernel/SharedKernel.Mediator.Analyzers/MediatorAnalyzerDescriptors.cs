using Microsoft.CodeAnalysis;

namespace SharedKernel.Mediator.Analyzers;

/// <summary>
/// Exposes the shared mediator diagnostics used by the analyzer package.
/// </summary>
internal static class MediatorAnalyzerDescriptors
{
    public static DiagnosticDescriptor InvalidHandlerSignature => MediatorDiagnosticDescriptors.InvalidHandlerSignature;

    public static DiagnosticDescriptor MissingCancellationToken => MediatorDiagnosticDescriptors.MissingCancellationToken;

    public static DiagnosticDescriptor HandlerReturnTypeMismatch => MediatorDiagnosticDescriptors.HandlerReturnTypeMismatch;

    public static DiagnosticDescriptor MissingCancellationForwarding => MediatorDiagnosticDescriptors.MissingCancellationForwarding;

    public static DiagnosticDescriptor MissingEnumeratorCancellation => MediatorDiagnosticDescriptors.MissingEnumeratorCancellation;

    public static DiagnosticDescriptor NonIteratorStreamHandlerHasCancellationToken => MediatorDiagnosticDescriptors.NonIteratorStreamHandlerHasCancellationToken;

    public static DiagnosticDescriptor InvalidPipelineGenericArity => MediatorDiagnosticDescriptors.InvalidPipelineGenericArity;

    public static DiagnosticDescriptor HandlerShouldNotCallSender => MediatorDiagnosticDescriptors.HandlerShouldNotCallSender;
}
