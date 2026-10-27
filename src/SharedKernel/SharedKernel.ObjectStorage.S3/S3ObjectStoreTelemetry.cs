using System.Diagnostics;

namespace PandaBear.ObjectStorage.S3;

/// <summary>Activity source for object-store operations.</summary>
/// <remarks>
/// Activities carry only the operation name. Bucket, object key, URL and request identifiers are never tagged,
/// so no exporter ordering can leak them.
/// </remarks>
public static class S3ObjectStoreTelemetry
{
    /// <summary>Name of the activity source to register with a tracer provider.</summary>
    public const string Name = "SharedKernel.ObjectStorage.S3";

    /// <summary>Source of the operation activities.</summary>
    internal static ActivitySource ActivitySource { get; } = new(Name);

    /// <summary>Starts a client activity for one operation, or returns null when nothing listens.</summary>
    internal static Activity? Start(string operation)
    {
        var activity = ActivitySource.StartActivity($"object_storage.{operation}", ActivityKind.Client);
        activity?.SetTag("object_storage.operation", operation);
        return activity;
    }
}
