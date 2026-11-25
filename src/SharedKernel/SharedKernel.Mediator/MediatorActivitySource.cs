using System.Diagnostics;

namespace SharedKernel.Mediator;

/// <summary>
/// Exposes the shared activity source used by optional mediator observability components.
/// </summary>
public static class MediatorActivitySource
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
    /// Starts the internal <see cref="MediatorTelemetry.ActivityHandle" /> activity around one request.
    /// </summary>
    /// <returns>The started activity when a listener is enabled; otherwise <see langword="null" />.</returns>
    internal static Activity? StartRequestActivity()
    {
        return Source.StartActivity(MediatorTelemetry.ActivityHandle, ActivityKind.Internal);
    }

    /// <summary>
    /// Gets the simple name of a type without its generic arity, the form the generated dispatch spans use.
    /// </summary>
    /// <param name="type">The request or response type.</param>
    /// <returns>The type name, such as <c>List</c> for <c>List&lt;int&gt;</c>.</returns>
    internal static string GetTypeName(Type type)
    {
        var name = type.Name;
        var arity = name.IndexOf('`', StringComparison.Ordinal);
        return arity < 0 ? name : name[..arity];
    }
}
