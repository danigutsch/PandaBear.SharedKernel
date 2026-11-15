namespace PandaBear.Repository.Tests;

/// <summary>What a test gives <c>prune-prereleases</c> to reach the GitHub API and to read its token.</summary>
/// <param name="CreateHttpHandler">Creates the HTTP handler of the run.</param>
/// <param name="GetEnvironmentVariable">Reads an environment variable, such as <c>GH_TOKEN</c>, or returns <see langword="null"/>.</param>
internal sealed record PrereleasePruningServices(Func<HttpMessageHandler> CreateHttpHandler, Func<string, string?> GetEnvironmentVariable);
