namespace PandaBear.IO.Analyzers;

/// <summary>
/// Exposes the public diagnostic identifiers for SharedKernel IO analyzers.
/// </summary>
public static class IODiagnosticIds
{
    /// <summary>
    /// Diagnostic emitted when a request or upload stream is buffered into memory without an explicit length bound.
    /// </summary>
    public const string UnboundedBuffering = "SKIO001";

    /// <summary>
    /// Diagnostic emitted when a hand-rolled read loop copies a stream into a <c>MemoryStream</c> without a length bound.
    /// </summary>
    public const string HandRolledCopyLoop = "SKIO002";

    /// <summary>
    /// Diagnostic emitted when an upload or raw-body endpoint sets no endpoint-level request size limit.
    /// </summary>
    public const string MissingRequestSizeLimit = "SKIO003";
}
