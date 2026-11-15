using System.Net.Http.Headers;

namespace PandaBear.GitHub.Tests;

/// <summary>Tests the headers of the GitHub API client.</summary>
public sealed class GitHubApiClientTests
{
    [Fact]
    public async Task Requests_carry_the_API_headers_and_the_token()
    {
        // Arrange
        var handler = new RecordingHandler();
        using var client = GitHubApiClient.Create(handler, new ProductInfoHeaderValue("sharedkernel-test", "1.0"), "secret");

        // Act
        using var response = await client.GetAsync(new Uri("https://api.github.com/repos/o/r"), TestContext.Current.CancellationToken);

        // Assert
        var request = handler.Request.ShouldNotBeNull();
        request.Headers.UserAgent.ToString().ShouldBe("sharedkernel-test/1.0");
        request.Headers.Accept.ToString().ShouldBe("application/vnd.github+json");
        request.Headers.GetValues("X-GitHub-Api-Version").ShouldBe(["2026-03-10"]);
        request.Headers.Authorization.ShouldBe(new AuthenticationHeaderValue("Bearer", "secret"));
    }

    [Fact]
    public async Task Anonymous_requests_carry_no_authorization()
    {
        // Arrange
        var handler = new RecordingHandler();
        using var client = GitHubApiClient.Create(handler, new ProductInfoHeaderValue("sharedkernel-test", "1.0"), token: null);

        // Act
        using var response = await client.GetAsync(new Uri("https://api.github.com/repos/o/r"), TestContext.Current.CancellationToken);

        // Assert
        handler.Request.ShouldNotBeNull().Headers.Authorization.ShouldBeNull();
    }

    [Fact]
    public void Invalid_arguments_are_rejected()
    {
        // Arrange
        using var handler = new RecordingHandler();
        var userAgent = new ProductInfoHeaderValue("sharedkernel-test", "1.0");

        // Act
        Action withoutHandler = () => GitHubApiClient.Create(null!, userAgent, null);
        Action withoutUserAgent = () => GitHubApiClient.Create(handler, null!, null);
        Action blankToken = () => GitHubApiClient.Create(handler, userAgent, " ");

        // Assert
        withoutHandler.ShouldThrow<ArgumentNullException>();
        withoutUserAgent.ShouldThrow<ArgumentNullException>();
        blankToken.ShouldThrow<ArgumentException>();
    }
}
