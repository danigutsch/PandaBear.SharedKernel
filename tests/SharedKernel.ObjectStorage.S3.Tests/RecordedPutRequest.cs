namespace PandaBear.ObjectStorage.S3.Tests;

/// <summary>One HTTP request received by <see cref="LoopbackS3Stub"/>.</summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="Path">The raw request path.</param>
/// <param name="ContentLength">The Content-Length header value, or -1 when absent.</param>
/// <param name="Headers">The request headers by name.</param>
/// <param name="Body">The raw request body bytes as sent on the wire.</param>
internal sealed record RecordedPutRequest(string Method, string Path, long ContentLength, IReadOnlyDictionary<string, string> Headers, byte[] Body);
