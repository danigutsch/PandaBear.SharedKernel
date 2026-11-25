namespace SharedKernel.Repository.Tests;

/// <summary>Records what a request to <see cref="FakeGitHubReleasesApi"/> carried.</summary>
/// <param name="Method">The HTTP method.</param>
/// <param name="Uri">The absolute address.</param>
/// <param name="Authorization">The <c>Authorization</c> header, or <see langword="null"/> when the request sent none.</param>
/// <param name="UserAgent">The <c>User-Agent</c> header text.</param>
/// <param name="ApiVersion">The <c>X-GitHub-Api-Version</c> header, or <see langword="null"/>.</param>
internal sealed record FakeGitHubRequest(HttpMethod Method, Uri Uri, string? Authorization, string UserAgent, string? ApiVersion);
