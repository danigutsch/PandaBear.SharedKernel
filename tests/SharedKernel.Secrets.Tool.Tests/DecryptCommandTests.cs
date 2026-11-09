using System.Text;
using static PandaBear.Secrets.Tool.Tests.SecretsToolHarness;

namespace PandaBear.Secrets.Tool.Tests;

/// <summary>Tests <c>decrypt</c> with a fake sops: standard output only unless <c>--output</c> and <c>--force</c>.</summary>
[Trait("Capability", "Secrets")]
public sealed class DecryptCommandTests
{
    private static readonly string Config = TestRecipients.Config(1, TestRecipients.First);

    [Fact]
    public async Task The_plaintext_goes_to_standard_output_exactly_and_nothing_else_does()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        var runner = ScriptedToolRunner.Succeeding("db:\n  password: hunter2\n");

        // Act
        var run = await Run(repository.Path, runner, ResolveAll, null, null, "decrypt", "secrets/app.sops.yaml", "--root", repository.Path, "--identity", "/keys/id.txt");

        // Assert
        run.ExitCode.ShouldBe(0);
        Encoding.UTF8.GetString(run.Plaintext).ShouldBe("db:\n  password: hunter2\n");
        run.Output.ShouldBe(string.Empty);
        run.Error.ShouldBe(string.Empty);
        var request = runner.Requests.ShouldHaveSingleItem();
        request.Arguments.ShouldBe(["decrypt", Path.Combine(repository.Path, "secrets", "app.sops.yaml")]);
        request.Environment.ShouldBe(new Dictionary<string, string> { ["SOPS_AGE_KEY_FILE"] = "/keys/id.txt" });
    }

    [Fact]
    public async Task A_secret_reference_extracts_one_value()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        var runner = ScriptedToolRunner.Succeeding("hunter2");

        // Act
        var run = await Run(repository.Path, runner, ResolveAll, null, null, "decrypt", "secrets/app.sops.yaml#db.password", "--root", repository.Path, "--identity", "/keys/id.txt");

        // Assert
        run.ExitCode.ShouldBe(0);
        Encoding.UTF8.GetString(run.Plaintext).ShouldBe("hunter2");
        runner.Requests.ShouldHaveSingleItem().Arguments.ShouldBe(["decrypt", "--extract", "[\"db\"][\"password\"]", Path.Combine(repository.Path, "secrets", "app.sops.yaml")]);
    }

    [Fact]
    public async Task The_identity_defaults_to_the_path_in_the_environment_variable()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        var runner = ScriptedToolRunner.Succeeding("x");

        // Act
        var run = await Run(repository.Path, runner, ResolveAll, null, new Dictionary<string, string> { ["SOPS_AGE_KEY_FILE"] = "/env/key.txt" }, "decrypt", "secrets/app.sops.yaml", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(0);
        runner.Requests.ShouldHaveSingleItem().Environment["SOPS_AGE_KEY_FILE"].ShouldBe("/env/key.txt");
    }

    [Fact]
    public async Task No_identity_is_a_failure_that_says_how_to_give_one()
    {
        // Arrange
        using var repository = CreateRepository(Config);

        // Act
        var run = await Run(repository.Path, "decrypt", "secrets/app.sops.yaml", "--root", repository.Path);

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("--identity", StringComparison.Ordinal);
        run.Error.ShouldContain("SOPS_AGE_KEY_FILE", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Key_material_given_as_the_identity_is_refused_without_echoing_it()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        const string key = "AGE-SECRET-KEY-1QQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQQ";
        var runner = ScriptedToolRunner.Unused();

        // Act
        var run = await Run(repository.Path, runner, ResolveAll, null, null, "decrypt", "secrets/app.sops.yaml", "--root", repository.Path, "--identity", key);

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("never the key itself", StringComparison.Ordinal);
        run.Error.ShouldNotContain("QQQQ", StringComparison.Ordinal);
        runner.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Output_without_force_is_refused_and_runs_nothing()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        var runner = ScriptedToolRunner.Unused();
        var output = Path.Combine(repository.Path, "plain.txt");

        // Act
        var run = await Run(repository.Path, runner, ResolveAll, null, null, "decrypt", "secrets/app.sops.yaml", "--root", repository.Path, "--identity", "/keys/id.txt", "--output", output);

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("needs --force", StringComparison.Ordinal);
        runner.Requests.ShouldBeEmpty();
        File.Exists(output).ShouldBeFalse();
        run.Plaintext.ShouldBeEmpty();
    }

    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task Output_with_force_writes_a_new_owner_only_file_and_nothing_to_standard_output()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        var output = Path.Combine(repository.Path, "plain.txt");

        // Act
        var run = await Run(repository.Path, ScriptedToolRunner.Succeeding("hunter2"), ResolveAll, null, null, "decrypt", "secrets/app.sops.yaml", "--root", repository.Path, "--identity", "/keys/id.txt", "--output", output, "--force");

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Plaintext.ShouldBeEmpty();
        var written = await File.ReadAllTextAsync(output, TestContext.Current.CancellationToken);
        written.ShouldBe("hunter2");
        File.GetUnixFileMode(output).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    [Fact]
    public async Task An_existing_output_file_is_never_overwritten()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        var output = Path.Combine(repository.Path, "plain.txt");
        await File.WriteAllTextAsync(output, "existing", TestContext.Current.CancellationToken);

        // Act
        var run = await Run(repository.Path, ScriptedToolRunner.Succeeding("hunter2"), ResolveAll, null, null, "decrypt", "secrets/app.sops.yaml", "--root", repository.Path, "--identity", "/keys/id.txt", "--output", output, "--force");

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("never overwrites", StringComparison.Ordinal);
        var written = await File.ReadAllTextAsync(output, TestContext.Current.CancellationToken);
        written.ShouldBe("existing");
        var visible = run.Output + run.Error;
        visible.ShouldNotContain("hunter2", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failing_sops_writes_no_plaintext_and_reports_one_line()
    {
        // Arrange
        using var repository = CreateRepository(Config);

        // Act
        var run = await Run(repository.Path, ScriptedToolRunner.Failing(128, "Failed to get the data key"), ResolveAll, null, null, "decrypt", "secrets/app.sops.yaml", "--root", repository.Path, "--identity", "/keys/id.txt");

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("sops exited with code 128: Failed to get the data key", StringComparison.Ordinal);
        run.Plaintext.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("secrets/app.yaml")]
    [InlineData("../app.sops.yaml")]
    [InlineData("secrets/app.sops.yaml#a..b")]
    public async Task An_invalid_target_is_refused_before_sops_runs(string target)
    {
        // Arrange
        using var repository = CreateRepository(Config);
        var runner = ScriptedToolRunner.Unused();

        // Act
        var run = await Run(repository.Path, runner, ResolveAll, null, null, "decrypt", target, "--root", repository.Path, "--identity", "/keys/id.txt");

        // Assert
        run.ExitCode.ShouldBe(2);
        runner.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_missing_sops_is_a_failure()
    {
        // Arrange
        using var repository = CreateRepository(Config);

        // Act
        var run = await Run(repository.Path, ScriptedToolRunner.Unused(), ResolveNone, null, null, "decrypt", "secrets/app.sops.yaml", "--root", repository.Path, "--identity", "/keys/id.txt");

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("sops was not found on PATH.", StringComparison.Ordinal);
    }
}
