using System.Net;
using static SharedKernel.RepoConfig.Tests.GitHubEnvironmentProtectionTestOperations;

namespace SharedKernel.RepoConfig.Tests;

/// <summary>Tests the read-only GitHub environment protection command.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
public sealed class GitHubEnvironmentProtectionCommandTests
{
    [Theory]
    [InlineData("User")]
    [InlineData("Team")]
    public async Task Verify_accepts_an_exact_environment_with_a_valid_configured_reviewer(string reviewerType)
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, $$"""
            {
              "name": "Review Stage/API",
              "can_admins_bypass": false,
              "protection_rules": [
                { "type": "wait_timer", "wait_timer": 10 },
                { "type": "required_reviewers", "reviewers": [
                    { "type": "{{reviewerType}}", "reviewer": { "id": 321, "login": "private-reviewer" } }
                ] }
              ]
            }
            """);

        // Act
        var (exitCode, output, error) = await Run(handler, environment: "Review Stage/API");

        // Assert
        exitCode.ShouldBe(0);
        output.ShouldContain("GitHub environment protection verified", StringComparison.Ordinal);
        output.ShouldNotContain("private-reviewer", StringComparison.Ordinal);
        error.ShouldBe(string.Empty);
        var request = handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Get);
        request.PathAndQuery.ShouldBe("/repos/owner/repository/environments/Review%20Stage%2FAPI");
        request.Message.Headers.Authorization.ShouldBeNull();
    }

    [Theory]
    [InlineData("""{"name":"other","can_admins_bypass":false,"protection_rules":[]}""", "environment name")]
    [InlineData("""{"name":"ROADMAP-MAINTENANCE","can_admins_bypass":false,"protection_rules":[]}""", "environment name")]
    [InlineData("""{"can_admins_bypass":false,"protection_rules":[]}""", "environment name")]
    [InlineData("""{"name":"roadmap-maintenance","can_admins_bypass":true,"protection_rules":[]}""", "administrator bypass")]
    [InlineData("""{"name":"roadmap-maintenance","protection_rules":[]}""", "administrator bypass")]
    [InlineData("""{"name":"roadmap-maintenance","can_admins_bypass":"false","protection_rules":[]}""", "administrator bypass")]
    [InlineData("""{"name":"roadmap-maintenance","can_admins_bypass":false}""", "protection rules")]
    [InlineData("""{"name":"roadmap-maintenance","can_admins_bypass":false,"protection_rules":{}}""", "protection rules")]
    [InlineData("""{"name":"roadmap-maintenance","can_admins_bypass":false,"protection_rules":[]}""", "required reviewer")]
    [InlineData("""{"name":"roadmap-maintenance","can_admins_bypass":false,"protection_rules":[{"type":"required_reviewers"}]}""", "required reviewer")]
    [InlineData("""{"name":"roadmap-maintenance","can_admins_bypass":false,"protection_rules":[{"type":"required_reviewers","reviewers":[]}]}""", "required reviewer")]
    [InlineData("""{"name":"roadmap-maintenance","can_admins_bypass":false,"protection_rules":[{"type":"required_reviewers","reviewers":[{"type":"Other","reviewer":{"id":321}}]}]}""", "required reviewer")]
    [InlineData("""{"name":"roadmap-maintenance","can_admins_bypass":false,"protection_rules":[{"type":"required_reviewers","reviewers":[{"type":"User","reviewer":{"id":0}}]}]}""", "required reviewer")]
    [InlineData("""{"name":"roadmap-maintenance","name":"other","can_admins_bypass":false,"protection_rules":[]}""", "response")]
    [InlineData("private-provider-metadata", "response")]
    public async Task Verify_fails_closed_when_the_environment_response_cannot_establish_protection(string body, string reason)
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, body);

        // Act
        var (exitCode, output, error) = await Run(handler);

        // Assert
        exitCode.ShouldBe(1);
        output.ShouldBe(string.Empty);
        error.ShouldContain(reason, StringComparison.OrdinalIgnoreCase);
        error.ShouldNotContain("private-provider-metadata", StringComparison.Ordinal);
        handler.Requests.ShouldHaveSingleItem().Method.ShouldBe(HttpMethod.Get);
    }

    [Theory]
    [InlineData(HttpStatusCode.Found, "302")]
    [InlineData(HttpStatusCode.Forbidden, "403")]
    [InlineData(HttpStatusCode.NotFound, "404")]
    [InlineData(HttpStatusCode.TooManyRequests, "429")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "503")]
    public async Task Verify_reports_provider_status_without_echoing_the_provider_response(HttpStatusCode status, string statusCode)
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(status, """{"error":"private-provider-metadata"}""");

        // Act
        var (exitCode, output, error) = await Run(handler);

        // Assert
        exitCode.ShouldBe(1);
        output.ShouldBe(string.Empty);
        error.ShouldContain("HTTP " + statusCode, StringComparison.Ordinal);
        error.ShouldNotContain("private-provider-metadata", StringComparison.Ordinal);
        handler.Requests.ShouldHaveSingleItem().Method.ShouldBe(HttpMethod.Get);
    }

    [Fact]
    public async Task Verify_rejects_an_oversized_environment_response()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, new string('x', 2 * 1024 * 1024));

        // Act
        var (exitCode, output, error) = await Run(handler);

        // Assert
        exitCode.ShouldBe(1);
        output.ShouldBe(string.Empty);
        error.ShouldContain("size", StringComparison.OrdinalIgnoreCase);
        handler.Requests.ShouldHaveSingleItem().Method.ShouldBe(HttpMethod.Get);
    }

    [Fact]
    public async Task Verify_does_not_echo_transport_failure_details()
    {
        // Arrange
        using var handler = new RecordingHttpMessageHandler(_ => throw new HttpRequestException("private-transport-message"));

        // Act
        var (exitCode, output, error) = await Run(handler);

        // Assert
        exitCode.ShouldBe(1);
        output.ShouldBe(string.Empty);
        error.ShouldContain("request failed", StringComparison.OrdinalIgnoreCase);
        error.ShouldNotContain("private-transport-message", StringComparison.Ordinal);
        handler.Requests.ShouldHaveSingleItem().Method.ShouldBe(HttpMethod.Get);
    }

    [Theory]
    [InlineData("owner/repository/other", "roadmap-maintenance")]
    [InlineData("owner/repository", " ")]
    [InlineData("owner/repository", "..")]
    public async Task Verify_refuses_invalid_arguments_before_making_a_request(string repository, string environment)
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();

        // Act
        var (exitCode, output, _) = await Run(handler, repository, environment);

        // Assert
        exitCode.ShouldBe(1);
        output.ShouldNotContain("GitHub environment protection verified", StringComparison.Ordinal);
        handler.Requests.ShouldBeEmpty();
    }
}
