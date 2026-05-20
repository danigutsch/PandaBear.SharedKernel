using System.Diagnostics;

namespace SharedKernel.Mediator;

/// <summary>
/// Exposes the shared activity source used by optional mediator observability components.
/// </summary>
public static class SharedKernelMediatorActivitySource
{
    /// <summary>
    /// The stable activity source name for mediator request dispatch.
    /// </summary>
    public static string ActivitySourceName => MediatorTelemetry.Name;

    /// <summary>
    /// Gets the shared activity source for mediator request dispatch.
    /// </summary>
    public static ActivitySource Source { get; } = new(ActivitySourceName);

    /// <summary>
    /// Starts an internal activity for the provided request type.
    /// </summary>
    /// <typeparam name="TRequest">The mediator request type.</typeparam>
    /// <returns>The started activity when a listener is enabled; otherwise <see langword="null" />.</returns>
    internal static Activity? StartRequestActivity<TRequest>()
    {
        return Source.StartActivity(GetActivityName<TRequest>(), ActivityKind.Internal);
    }

    /// <summary>
    /// Gets the default activity name for the provided request type.
    /// </summary>
    /// <typeparam name="TRequest">The mediator request type.</typeparam>
    /// <returns>The default activity name.</returns>
    internal static string GetActivityName<TRequest>()
    {
        return typeof(TRequest).Name;
    }
}
