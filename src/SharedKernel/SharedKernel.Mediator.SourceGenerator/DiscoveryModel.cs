using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace SharedKernel.Mediator.SourceGenerator;

/// <summary>
/// Captures the immutable discovery data produced for the initial generator report.
/// </summary>
internal sealed record DiscoveryModel(
    ImmutableArray<ModuleDescriptor> Modules,
    ImmutableArray<RequestDescriptor> Requests,
    ImmutableArray<NotificationDescriptor> Notifications,
    ImmutableArray<StreamRequestDescriptor> StreamRequests,
    ImmutableArray<Diagnostic> Diagnostics)
{
    /// <summary>
    /// Gets the number of discovered request contracts.
    /// </summary>
    public int RequestCount => Requests.Length;

    /// <summary>
    /// Gets the number of discovered request handlers.
    /// </summary>
    public int HandlerCount => Requests.Sum(static request => request.Handlers.Length);

    /// <summary>
    /// Gets the number of discovered pipeline behaviors.
    /// </summary>
    public int PipelineCount => Requests.Sum(static request => request.Pipelines.Length);

    /// <summary>
    /// Gets the number of discovered notifications.
    /// </summary>
    public int NotificationCount => Notifications.Length;

    /// <summary>
    /// Gets the number of discovered notification handlers.
    /// </summary>
    public int NotificationHandlerCount => Notifications.Sum(static notification => notification.Handlers.Length);

    /// <summary>
    /// Gets the number of discovered stream requests.
    /// </summary>
    public int StreamRequestCount => StreamRequests.Length;

    /// <summary>
    /// Gets the number of discovered stream handlers.
    /// </summary>
    public int StreamHandlerCount => StreamRequests.Sum(static request => request.Handlers.Length);
}
