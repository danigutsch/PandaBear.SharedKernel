using static SharedKernel.Secrets.Tool.Tests.SecretsToolHarness;

namespace SharedKernel.Secrets.Tool.Tests;

/// <summary>Tests that every command that takes <c>--root</c> refuses an empty or missing root before it reads or runs anything.</summary>
[Trait(TestTraitNames.CapabilityName, "Secrets")]
public sealed class RootOptionCommandTests
{
    [Theory]
    [InlineData("verify")]
    [InlineData("encrypt", "secrets/app.sops.yaml")]
    [InlineData("decrypt", "secrets/app.sops.yaml", "--identity", "/keys/id.txt")]
    [InlineData("edit", "secrets/app.sops.yaml")]
    [InlineData("rotate", "secrets/app.sops.yaml")]
    [InlineData("recipients", "list", "secrets/app.sops.yaml")]
    [InlineData("recipients", "check", "secrets/app.sops.yaml")]
    [InlineData("recipients", "update", "secrets/app.sops.yaml")]
    public async Task An_empty_root_is_a_parse_error_instead_of_the_current_directory(params string[] command)
    {
        // Arrange
        using var repository = CreateRepository(TestRecipients.Config(1, TestRecipients.First));

        // Act
        var run = await Run(repository.Path, [.. command, "--root", ""]);

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Error.ShouldContain("--root requires a directory path.", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("verify")]
    [InlineData("encrypt", "secrets/app.sops.yaml")]
    [InlineData("decrypt", "secrets/app.sops.yaml", "--identity", "/keys/id.txt")]
    [InlineData("edit", "secrets/app.sops.yaml")]
    [InlineData("rotate", "secrets/app.sops.yaml")]
    [InlineData("recipients", "list", "secrets/app.sops.yaml")]
    [InlineData("recipients", "check", "secrets/app.sops.yaml")]
    [InlineData("recipients", "update", "secrets/app.sops.yaml")]
    public async Task A_root_that_is_not_a_directory_fails_without_running_a_program(params string[] command)
    {
        // Arrange
        using var repository = CreateRepository(null);
        var missing = Path.Combine(repository.Path, "missing");

        // Act
        var run = await Run(repository.Path, [.. command, "--root", "missing"]);

        // Assert
        run.ExitCode.ShouldBe(SecretsExitCodes.Failure);
        run.Output.ShouldBe(string.Empty);
        run.Error.ShouldBe("--root is not an existing directory: " + missing + "\n");
    }

    [Fact]
    public async Task A_relative_root_is_resolved_from_the_working_directory()
    {
        // Arrange
        using var repository = CreateRepository(null);
        var nested = Path.Combine(repository.Path, "nested");
        Directory.CreateDirectory(Path.Combine(nested, ".config"));
        await File.WriteAllTextAsync(Path.Combine(nested, ".config", "sharedkernel-secrets.json"), TestRecipients.Config(1, TestRecipients.First), TestContext.Current.CancellationToken);
        WriteFile(nested, "secrets/a.sops.yaml", TestRecipients.Yaml([TestRecipients.First]));

        // Act
        var run = await Run(repository.Path, "verify", "--root", "nested");

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("secrets/a.sops.yaml: labels consistent", StringComparison.Ordinal);
    }
}
