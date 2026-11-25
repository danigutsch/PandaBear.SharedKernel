using Microsoft.CodeAnalysis;

namespace SharedKernel.Mediator.SourceGenerator;

/// <summary>
/// Accumulates raw discovery results before descriptor projection.
/// </summary>
internal sealed class DiscoveryState
{
    public DiscoveryState()
    {
        RequestContracts = new Dictionary<string, RawRequestContract>(StringComparer.Ordinal);
        RequestHandlers = [];
        Pipelines = [];
        NotificationContracts = new Dictionary<string, RawNotificationContract>(StringComparer.Ordinal);
        NotificationHandlers = [];
        StreamRequestContracts = new Dictionary<string, RawStreamRequestContract>(StringComparer.Ordinal);
        StreamHandlers = [];
        StreamPipelines = [];
        Diagnostics = [];
        DiagnosedInaccessibleTypes = new HashSet<string>(StringComparer.Ordinal);
        GeneratedRegistrationKeys = new HashSet<string>(StringComparer.Ordinal);
        DuplicateRegistrationDiagnostics = new Dictionary<string, DuplicateRegistrationDiagnostic>(StringComparer.Ordinal);
        DiagnosedInvalidHandlerKeys = new HashSet<string>(StringComparer.Ordinal);
        DiagnosedUnmarkedAssemblies = new HashSet<string>(StringComparer.Ordinal);
    }

    public Dictionary<string, RawRequestContract> RequestContracts { get; }

    public List<HandlerDescriptor> RequestHandlers { get; }

    public List<RawPipelineDescriptor> Pipelines { get; }

    public Dictionary<string, RawNotificationContract> NotificationContracts { get; }

    public List<RawNotificationHandlerDescriptor> NotificationHandlers { get; }

    public Dictionary<string, RawStreamRequestContract> StreamRequestContracts { get; }

    public List<StreamHandlerDescriptor> StreamHandlers { get; }

    public List<RawPipelineDescriptor> StreamPipelines { get; }

    public List<Diagnostic> Diagnostics { get; }

    public HashSet<string> DiagnosedInaccessibleTypes { get; }

    public HashSet<string> GeneratedRegistrationKeys { get; }

    public Dictionary<string, DuplicateRegistrationDiagnostic> DuplicateRegistrationDiagnostics { get; }

    public HashSet<string> DiagnosedInvalidHandlerKeys { get; }

    public HashSet<string> DiagnosedUnmarkedAssemblies { get; }
}
