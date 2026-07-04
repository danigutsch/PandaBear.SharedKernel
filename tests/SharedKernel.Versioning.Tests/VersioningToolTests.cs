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
    public static async Task Reads_null_separated_commit_messages_from_standard_input()
    {
        // Arrange
        using var input = new ConsoleInputScope("feat: add output\0fix: patch output");

        // Act
        var messages = await CommitMessageInput.ReadMessages();

        // Assert
        messages.ShouldContain("feat: add output");
        messages.ShouldContain("fix: patch output");
    }
}
