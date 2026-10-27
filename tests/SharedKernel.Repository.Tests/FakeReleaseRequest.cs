namespace PandaBear.Repository.Tests;

/// <summary>Records what a request to <see cref="FakeReleaseServer"/> carried.</summary>
/// <param name="Uri">The absolute address that the request asked for.</param>
/// <param name="UserAgent">The <c>User-Agent</c> header text, empty when the request sent none.</param>
/// <param name="HasAuthorization">Whether the request carried an <c>Authorization</c> header.</param>
internal sealed record FakeReleaseRequest(Uri Uri, string UserAgent, bool HasAuthorization);
