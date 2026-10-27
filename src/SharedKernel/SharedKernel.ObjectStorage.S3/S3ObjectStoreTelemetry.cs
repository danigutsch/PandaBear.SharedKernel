using System.Diagnostics;
using OpenTelemetry;

namespace PandaBear.ObjectStorage.S3;

/// <summary>Activity source for object-store operations.</summary>
/// <remarks>
/// Activities carry only the operation name. Bucket, object key, URL and request identifiers are never tagged,
/// and downstream HTTP instrumentation is suppressed while an operation runs, so path-style S3 URLs cannot reach
/// exporters through HttpClient spans regardless of exporter order.
/// </remarks>
public static class S3ObjectStoreTelemetry
{
    /// <summary>Name of the activity source to register with a tracer provider.</summary>
    public const string Name = "SharedKernel.ObjectStorage.S3";

    /// <summary>Source of the operation activities.</summary>
    internal static ActivitySource ActivitySource { get; } = new(Name);

    /// <summary>Starts a client activity for one operation and suppresses downstream instrumentation until disposed.</summary>
    internal static IDisposable Start(string operation)
    {
        var activity = ActivitySource.StartActivity($"object_storage.{operation}", ActivityKind.Client);
        activity?.SetTag("object_storage.operation", operation);
        return new OperationScope(activity, SuppressInstrumentationScope.Begin());
    }

    private sealed class OperationScope(Activity? activity, IDisposable suppression) : IDisposable
    {
        public void Dispose()
        {
            suppression.Dispose();
            activity?.Dispose();
        }
    }
}
