namespace PandaBear.Repository.Tests;

/// <summary>Records what a request to <see cref="FakeReleaseServer"/> carried.</summary>
internal sealed record FakeReleaseRequest(Uri Uri, string UserAgent, bool HasAuthorization);
