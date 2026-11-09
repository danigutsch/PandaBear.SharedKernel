namespace PandaBear.Packages.Tests;

/// <summary>An HTTP request as the fake handler saw it.</summary>
/// <param name="Method">The method.</param>
/// <param name="Uri">The absolute address.</param>
/// <param name="Body">The request body, or an empty string.</param>
/// <param name="UserAgent">The <c>User-Agent</c> header.</param>
internal sealed record RecordedRequest(HttpMethod Method, Uri Uri, string Body, string UserAgent);
