using System.Text;
using static PandaBear.Secrets.Tool.Tests.SecretsToolHarness;

namespace PandaBear.Secrets.Tool.Tests;

/// <summary>Tests <c>encrypt</c> with a fake sops.</summary>
[Trait("Capability", "Secrets")]
public sealed class EncryptCommandTests
{
    private static readonly string Config = TestRecipients.Config(2, TestRecipients.First, TestRecipients.Second);

    [Fact]
    public async Task Without_force_it_previews_without_reading_standard_input_or_running_anything()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        var runner = ScriptedToolRunner.Unused();

        // Act
        var run = await Run(repository.Path, runner, ResolveAll, Encoding.UTF8.GetBytes("PLAINTEXT"), null, "encrypt", "secrets/app.sops.yaml", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("Preview: would encrypt standard input to secrets/app.sops.yaml for 2 recipients", StringComparison.Ordinal);
        runner.Requests.ShouldBeEmpty();
        File.Exists(Path.Combine(repository.Path, "secrets", "app.sops.yaml")).ShouldBeFalse();
    }

    [Fact]
    public async Task With_force_it_encrypts_standard_input_to_the_rule_recipients_and_creates_the_file()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        var runner = new ScriptedToolRunner(_ => ScriptedToolRunner.Result(0, TestRecipients.Yaml([TestRecipients.First, TestRecipients.Second])));

        // Act
        var run = await Run(repository.Path, runner, ResolveAll, Encoding.UTF8.GetBytes("password: hunter2"), null, "encrypt", "secrets/app.sops.yaml", "--root", repository.Path, "--force");

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("Encrypted secrets/app.sops.yaml to 2 recipients.", StringComparison.Ordinal);
        var request = runner.Requests.ShouldHaveSingleItem();
        request.Executable.ShouldBe("/fake/sops");
        request.Arguments.ShouldBe(["encrypt", "--age", $"{TestRecipients.Second},{TestRecipients.First}", "--input-type", "yaml", "--output-type", "yaml", "/dev/stdin"]);
        request.Arguments.ShouldNotContain("password: hunter2");
        request.Environment.ShouldBeEmpty();
        Encoding.UTF8.GetString(runner.Inputs.ShouldHaveSingleItem()).ShouldBe("password: hunter2");
        File.Exists(Path.Combine(repository.Path, "secrets", "app.sops.yaml")).ShouldBeTrue();
        var visible = run.Output + run.Error;
        visible.ShouldNotContain("hunter2", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_file_no_rule_matches_is_refused()
    {
        // Arrange
        using var repository = CreateRepository(Config);

        // Act
        var run = await Run(repository.Path, "encrypt", "other/app.sops.yaml", "--root", repository.Path, "--force");

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("No creation rule matches this file.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_existing_file_is_never_overwritten()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/app.sops.yaml", "existing");
        var runner = ScriptedToolRunner.Unused();

        // Act
        var run = await Run(repository.Path, runner, ResolveAll, Encoding.UTF8.GetBytes("x"), null, "encrypt", "secrets/app.sops.yaml", "--root", repository.Path, "--force");

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("never overwrites", StringComparison.Ordinal);
        var written = await File.ReadAllTextAsync(Path.Combine(repository.Path, "secrets", "app.sops.yaml"), TestContext.Current.CancellationToken);
        written.ShouldBe("existing");
    }

    [Theory]
    [InlineData("secrets/app.sops.env")]
    [InlineData("secrets/app.yaml")]
    [InlineData("../app.sops.yaml")]
    public async Task A_file_that_cannot_be_encrypted_is_refused(string file)
    {
        // Arrange
        using var repository = CreateRepository(TestRecipients.Config(1, TestRecipients.First).Replace("^secrets/", ".", StringComparison.Ordinal));

        // Act
        var run = await Run(repository.Path, "encrypt", file, "--root", repository.Path, "--force");

        // Assert
        run.ExitCode.ShouldBe(2);
    }

    [Fact]
    public async Task Empty_standard_input_is_refused_and_nothing_is_created()
    {
        // Arrange
        using var repository = CreateRepository(Config);

        // Act
        var run = await Run(repository.Path, ScriptedToolRunner.Unused(), ResolveAll, [], null, "encrypt", "secrets/app.sops.yaml", "--root", repository.Path, "--force");

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("Standard input must hold", StringComparison.Ordinal);
        Directory.Exists(Path.Combine(repository.Path, "secrets")).ShouldBeFalse();
    }

    [Fact]
    public async Task A_failing_sops_creates_no_file()
    {
        // Arrange
        using var repository = CreateRepository(Config);

        // Act
        var run = await Run(repository.Path, ScriptedToolRunner.Failing(1, "sops failed"), ResolveAll, Encoding.UTF8.GetBytes("x: y"), null, "encrypt", "secrets/app.sops.yaml", "--root", repository.Path, "--force");

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("sops exited with code 1: sops failed", StringComparison.Ordinal);
        File.Exists(Path.Combine(repository.Path, "secrets", "app.sops.yaml")).ShouldBeFalse();
    }

    [Fact]
    public async Task A_result_that_does_not_carry_exactly_the_rule_recipients_is_removed_and_fails()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        var runner = new ScriptedToolRunner(_ => ScriptedToolRunner.Result(0, TestRecipients.Yaml([TestRecipients.First])));

        // Act
        var run = await Run(repository.Path, runner, ResolveAll, Encoding.UTF8.GetBytes("x: y"), null, "encrypt", "secrets/app.sops.yaml", "--root", repository.Path, "--force");

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("does not carry exactly the rule's recipients", StringComparison.Ordinal);
        File.Exists(Path.Combine(repository.Path, "secrets", "app.sops.yaml")).ShouldBeFalse();
    }

    [Fact]
    public async Task A_missing_sops_is_a_failure()
    {
        // Arrange
        using var repository = CreateRepository(Config);

        // Act
        var run = await Run(repository.Path, ScriptedToolRunner.Unused(), ResolveNone, Encoding.UTF8.GetBytes("x: y"), null, "encrypt", "secrets/app.sops.yaml", "--root", repository.Path, "--force");

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("sops was not found on PATH.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Json_files_are_encrypted_with_the_json_types()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        var runner = new ScriptedToolRunner(_ => ScriptedToolRunner.Result(0, TestRecipients.Json([TestRecipients.First, TestRecipients.Second])));

        // Act
        var run = await Run(repository.Path, runner, ResolveAll, Encoding.UTF8.GetBytes("{}"), null, "encrypt", "secrets/app.sops.json", "--root", repository.Path, "--force");

        // Assert
        run.ExitCode.ShouldBe(0);
        runner.Requests.ShouldHaveSingleItem().Arguments.ShouldContain("json");
    }
}
