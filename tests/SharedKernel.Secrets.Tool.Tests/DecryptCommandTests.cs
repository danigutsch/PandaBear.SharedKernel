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
        request.Arguments.ShouldBe(["--config", "/dev/null", "decrypt", Path.Combine(repository.Path, "secrets", "app.sops.yaml")]);
        request.Environment.Keys.Order(StringComparer.Ordinal).ToArray().ShouldBe(["HOME", "SOPS_AGE_KEY_FILE"]);
        request.Environment["SOPS_AGE_KEY_FILE"].ShouldBe("/keys/id.txt");
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
        runner.Requests.ShouldHaveSingleItem().Arguments.ShouldBe(["--config", "/dev/null", "decrypt", "--extract", "[\"db\"][\"password\"]", Path.Combine(repository.Path, "secrets", "app.sops.yaml")]);
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
        const string key = FakeKeys.Secret;
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
        var output = "plain.txt";

        // Act
        var run = await Run(repository.Path, runner, ResolveAll, null, null, "decrypt", "secrets/app.sops.yaml", "--root", repository.Path, "--identity", "/keys/id.txt", "--output", output);

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("needs --force", StringComparison.Ordinal);
        runner.Requests.ShouldBeEmpty();
        File.Exists(Path.Combine(repository.Path, output)).ShouldBeFalse();
        run.Plaintext.ShouldBeEmpty();
    }

    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task Output_with_force_writes_a_new_owner_only_file_and_nothing_to_standard_output()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        var output = "plain.txt";

        // Act
        var run = await Run(repository.Path, ScriptedToolRunner.Succeeding("hunter2"), ResolveAll, null, null, "decrypt", "secrets/app.sops.yaml", "--root", repository.Path, "--identity", "/keys/id.txt", "--output", output, "--force");

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Plaintext.ShouldBeEmpty();
        var target = Path.Combine(repository.Path, output);
        var written = await File.ReadAllTextAsync(target, TestContext.Current.CancellationToken);
        written.ShouldBe("hunter2");
        File.GetUnixFileMode(target).ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
        Directory.GetFiles(repository.Path).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray().ShouldBe(["plain.txt"]);
    }

    [Fact]
    public async Task An_existing_output_file_is_never_overwritten()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        var output = "plain.txt";
        await File.WriteAllTextAsync(Path.Combine(repository.Path, output), "existing", TestContext.Current.CancellationToken);

        // Act
        var run = await Run(repository.Path, ScriptedToolRunner.Succeeding("hunter2"), ResolveAll, null, null, "decrypt", "secrets/app.sops.yaml", "--root", repository.Path, "--identity", "/keys/id.txt", "--output", output, "--force");

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("never overwrites", StringComparison.Ordinal);
        var written = await File.ReadAllTextAsync(Path.Combine(repository.Path, output), TestContext.Current.CancellationToken);
        written.ShouldBe("existing");
        Directory.GetFiles(repository.Path).Select(Path.GetFileName).Order(StringComparer.Ordinal).ToArray().ShouldBe(["plain.txt"]);
        var visible = run.Output + run.Error;
        visible.ShouldNotContain("hunter2", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("sub/../../escape.txt")]
    [InlineData("/tmp/absolute-escape.txt")]
    [InlineData("a\\b.txt")]
    [InlineData("missing-directory/plain.txt")]
    public async Task An_output_outside_the_root_or_in_a_directory_that_does_not_exist_is_refused_and_sops_is_not_run(string output)
    {
        // Arrange
        using var repository = CreateRepository(Config);
        var runner = ScriptedToolRunner.Unused();

        // Act
        var run = await Run(repository.Path, runner, ResolveAll, null, null, "decrypt", "secrets/app.sops.yaml", "--root", repository.Path, "--identity", "/keys/id.txt", "--output", output, "--force");

        // Assert
        run.ExitCode.ShouldBe(2);
        runner.Requests.ShouldBeEmpty();
        File.Exists(Path.Combine(Path.GetDirectoryName(repository.Path)!, "escape.txt")).ShouldBeFalse();
    }

    [Fact]
    public async Task An_output_through_a_symbolic_link_is_refused()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        using var elsewhere = new TemporaryDirectory();
        Directory.CreateSymbolicLink(Path.Combine(repository.Path, "linked"), elsewhere.Path);
        File.CreateSymbolicLink(Path.Combine(repository.Path, "link.txt"), Path.Combine(elsewhere.Path, "victim.txt"));
        var runner = ScriptedToolRunner.Succeeding("hunter2");

        // Act
        var throughDirectory = await Run(repository.Path, runner, ResolveAll, null, null, "decrypt", "secrets/app.sops.yaml", "--root", repository.Path, "--identity", "/keys/id.txt", "--output", "linked/plain.txt", "--force");
        var throughFile = await Run(repository.Path, runner, ResolveAll, null, null, "decrypt", "secrets/app.sops.yaml", "--root", repository.Path, "--identity", "/keys/id.txt", "--output", "link.txt", "--force");

        // Assert
        throughDirectory.ExitCode.ShouldBe(2);
        throughFile.ExitCode.ShouldBe(2);
        Directory.GetFileSystemEntries(elsewhere.Path).ShouldBeEmpty();
    }


    [Fact]
    public async Task A_file_that_appears_while_decrypting_is_never_overwritten_and_no_temporary_file_is_left()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        var target = Path.Combine(repository.Path, "plain.txt");
        var runner = new ScriptedToolRunner(_ =>
        {
            File.WriteAllText(target, "raced");
            return ScriptedToolRunner.Result(0, "hunter2");
        });

        // Act
        var run = await Run(repository.Path, runner, ResolveAll, null, null, "decrypt", "secrets/app.sops.yaml", "--root", repository.Path, "--identity", "/keys/id.txt", "--output", "plain.txt", "--force");

        // Assert
        var content = await File.ReadAllTextAsync(target, TestContext.Current.CancellationToken);
        run.ExitCode.ShouldBe(2);
        content.ShouldBe("raced");
        Directory.GetFiles(repository.Path).Select(Path.GetFileName).ToArray().ShouldBe(["plain.txt"]);
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
