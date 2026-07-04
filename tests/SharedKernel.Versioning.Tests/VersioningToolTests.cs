using SharedKernel.Versioning.Tool;

namespace SharedKernel.Versioning.Tests;

[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, "Versioning")]
public static class VersioningToolTests
{
    [Fact]
    public static void Parses_compute_options()
    {
        // Arrange
        string[] args = ["--base", "0.1.0", "--prerelease", "alpha.1", "--sha", "abc123"];

        // Act
        var options = VersionToolOptions.Parse(args);

        // Assert
        options.BaseVersion.ShouldBe("0.1.0");
        options.Prerelease.ShouldBe("alpha.1");
        options.Sha.ShouldBe("abc123");
    }

    [Fact]
    public static void Serializes_version_output_json()
    {
        // Arrange
        var output = VersionOutput.Create(new SemanticVersion(1, 2, 3), ReleaseImpact.Minor, "abc123");

        // Act
        var json = VersionOutputJson.Serialize(output);

        // Assert
        json.ShouldContain("\"semVer\":\"1.2.3\"", StringComparison.Ordinal);
        json.ShouldContain("\"releaseImpact\":\"minor\"", StringComparison.Ordinal);
        json.ShouldContain("\"assemblyVersion\":\"1.0.0.0\"", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Reads_null_separated_commit_messages_from_input()
    {
        // Arrange
        using var input = new StringReader("feat: add output\0fix: patch output");

        // Act
        var messages = await CommitMessageInput.ReadMessages(input);

        // Assert
        messages.ShouldContain("feat: add output");
        messages.ShouldContain("fix: patch output");
    }

    [Fact]
    public static async Task Runs_commit_impact_command()
    {
        // Arrange
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        using var error = new StringWriter();
        string[] args = ["commit-impact", "feat(api)!:", "remove", "route"];

        // Act
        var exitCode = await VersioningToolApplication.Run(args, input, output, error);

        // Assert
        exitCode.ShouldBe(0);
        output.ToString().ShouldContain("major", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Runs_compute_command_from_input()
    {
        // Arrange
        using var input = new StringReader("feat: add output\0fix: patch output");
        using var output = new StringWriter();
        using var error = new StringWriter();
        string[] args = ["compute", "--base", "0.1.0", "--prerelease", "alpha.1", "--sha", "abc123"];

        // Act
        var exitCode = await VersioningToolApplication.Run(args, input, output, error);

        // Assert
        exitCode.ShouldBe(0);
        var json = output.ToString();
        json.ShouldContain("\"semVer\":\"0.2.0-alpha.1\"", StringComparison.Ordinal);
        json.ShouldContain("\"informationalVersion\":\"0.2.0-alpha.1", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Returns_usage_for_unknown_command()
    {
        // Arrange
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await VersioningToolApplication.Run(["unknown"], input, output, error);

        // Assert
        exitCode.ShouldBe(2);
        error.ToString().ShouldContain("Usage:", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Returns_error_for_invalid_commit_impact_message()
    {
        // Arrange
        using var input = new StringReader(string.Empty);
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await VersioningToolApplication.Run(["commit-impact", "invalid"], input, output, error);

        // Assert
        exitCode.ShouldBe(2);
        error.ToString().ShouldContain("Error:", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Returns_error_for_invalid_compute_options()
    {
        // Arrange
        using var input = new StringReader("feat: add output");
        using var output = new StringWriter();
        using var error = new StringWriter();

        // Act
        var exitCode = await VersioningToolApplication.Run(["compute", "--base", "not-a-version"], input, output, error);

        // Assert
        exitCode.ShouldBe(2);
        error.ToString().ShouldContain("Error:", StringComparison.Ordinal);
    }
}
