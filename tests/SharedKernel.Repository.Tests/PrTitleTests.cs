using System.Text.Json;

namespace PandaBear.Repository.Tests;

public sealed class PrTitleTests
{
    [Theory]
    [InlineData("feat: support the contract", "BREAKING CHANGE: remove an old field", 0)]
    [InlineData("Update contract", "", 2)]
    [InlineData("feat:    ", "", 2)]
    [InlineData("feat: support the contract", "BREAKING CHANGE missing colon", 2)]
    public async Task Metadata_rejects_invalid_squash_headers_and_breaking_footers(string title, string body, int expected)
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        await File.WriteAllTextAsync(Path.Combine(scope.Root, "event.json"), JsonSerializer.Serialize(new { pull_request = new { title, body } }), ct);
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(scope.Root, ["verify-pr-title", "--event", "event.json"], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(expected);
        result.Output.ShouldBe(string.Empty);
        processes.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task Invalid_title_is_reported_on_standard_error()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = new RepositoryTestScope();
        var eventPath = Path.Combine(scope.Root, "event.json");
        await File.WriteAllTextAsync(eventPath, JsonSerializer.Serialize(new { pull_request = new { title = "Update contract", body = "" } }), ct);
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(Path.GetDirectoryName(scope.Root)!, ["verify-pr-title", "--event", eventPath], processes.Run, ct);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("Conventional Commit", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_event_file_fails_with_the_domain_exit_code()
    {
        // Arrange
        using var scope = new RepositoryTestScope();
        var processes = new ProcessLog();

        // Act
        var result = await RepositoryToolInvocation.Run(
            scope.Root, ["verify-pr-title", "--event", "missing.json"], processes.Run, TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("missing.json", StringComparison.Ordinal);
    }
}
