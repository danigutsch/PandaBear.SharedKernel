using static PandaBear.Secrets.Tool.Tests.SecretsToolHarness;

namespace PandaBear.Secrets.Tool.Tests;

/// <summary>Tests <c>keygen</c> with a fake age-keygen.</summary>
[Trait(TestTraitNames.CapabilityName, "Secrets")]
public sealed class KeygenCommandTests
{
    [Fact]
    public async Task Without_force_it_previews_and_runs_nothing()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var runner = ScriptedToolRunner.Unused();
        var path = Path.Combine(directory.Path, "id.txt");

        // Act
        var run = await Run(directory.Path, runner, ResolveAll, null, null, "keygen", "--output", path);

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain("Preview: would create the age identity file", StringComparison.Ordinal);
        run.Output.ShouldContain("--force", StringComparison.Ordinal);
        runner.Requests.ShouldBeEmpty();
        File.Exists(path).ShouldBeFalse();
    }

    [Fact]
    public async Task With_force_it_runs_age_keygen_and_prints_only_the_public_recipient()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var runner = ScriptedToolRunner.Succeeding(string.Empty, $"Public key: {TestRecipients.First}\n");

        // Act
        var run = await Run(directory.Path, runner, ResolveAll, null, null, "keygen", "-o", "id.txt", "--force");

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain($"Public recipient: {TestRecipients.First}", StringComparison.Ordinal);
        run.Error.ShouldBe(string.Empty);
        var request = runner.Requests.ShouldHaveSingleItem();
        request.Executable.ShouldBe("/fake/age-keygen");
        request.Arguments.ShouldBe(["-o", Path.Combine(directory.Path, "id.txt")]);
    }

    [Fact]
    public async Task An_existing_file_is_refused_with_or_without_force()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var path = Path.Combine(directory.Path, "id.txt");
        await File.WriteAllTextAsync(path, "existing", TestContext.Current.CancellationToken);
        var runner = ScriptedToolRunner.Unused();

        // Act
        var preview = await Run(directory.Path, runner, ResolveAll, null, null, "keygen", "--output", path);
        var forced = await Run(directory.Path, runner, ResolveAll, null, null, "keygen", "--output", path, "--force");

        // Assert
        preview.ExitCode.ShouldBe(2);
        forced.ExitCode.ShouldBe(2);
        forced.Error.ShouldContain("never overwritten", StringComparison.Ordinal);
        runner.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_missing_program_is_a_failure_before_anything_runs()
    {
        // Arrange
        using var directory = new TemporaryDirectory();

        // Act
        var run = await Run(directory.Path, ScriptedToolRunner.Unused(), ResolveNone, null, null, "keygen", "--output", "id.txt", "--force");

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("age was not found on PATH.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_failing_age_keygen_is_a_failure()
    {
        // Arrange
        using var directory = new TemporaryDirectory();

        // Act
        var run = await Run(directory.Path, ScriptedToolRunner.Failing(1, "age-keygen: error: boom"), ResolveAll, null, null, "keygen", "--output", "id.txt", "--force");

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("age-keygen exited with code 1: age-keygen: error: boom", StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_output_option_is_required_and_a_missing_one_is_a_parse_error()
    {
        // Arrange
        using var directory = new TemporaryDirectory();

        // Act
        var run = await Run(directory.Path, "keygen");

        // Assert
        run.ExitCode.ShouldBe(1);
        run.Error.ShouldContain("--output", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_path_that_looks_like_key_material_is_refused_without_echoing_it()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        const string key = FakeKeys.Secret;

        // Act
        var run = await Run(directory.Path, ScriptedToolRunner.Unused(), ResolveAll, null, null, "keygen", "--output", key, "--force");

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("never the key itself", StringComparison.Ordinal);
        run.Error.ShouldNotContain("QQQQ", StringComparison.Ordinal);
    }
}
