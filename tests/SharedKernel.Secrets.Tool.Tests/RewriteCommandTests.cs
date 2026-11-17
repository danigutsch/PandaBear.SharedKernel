using static PandaBear.Secrets.Tool.Tests.SecretsToolHarness;

namespace PandaBear.Secrets.Tool.Tests;

/// <summary>Tests <c>recipients update</c> and <c>rotate</c> with a fake sops: previews by default, and with <c>--force</c> a checked, atomic replacement.</summary>
[Trait("Capability", "Secrets")]
public sealed class RewriteCommandTests
{
    private const string File = "secrets/app.sops.yaml";

    private static readonly string Config = TestRecipients.Config(2, TestRecipients.First, TestRecipients.Second);

    [Fact]
    public async Task Update_previews_the_recipients_it_would_add_and_remove_and_starts_nothing()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        var original = TestRecipients.Yaml([TestRecipients.First, TestRecipients.Third]);
        WriteFile(repository.Path, File, original);
        var runner = ScriptedToolRunner.Unused();

        // Act
        var run = await Run(repository.Path, runner, ResolveAll, null, null, "recipients", "update", File);

        // Assert
        run.ExitCode.ShouldBe(0);
        run.Output.ShouldContain($"{File}: add recipient {TestRecipients.Second}", StringComparison.Ordinal);
        run.Output.ShouldContain($"{File}: remove recipient {TestRecipients.Third}", StringComparison.Ordinal);
        run.Output.ShouldContain("Preview: nothing was written.", StringComparison.Ordinal);
        runner.Requests.ShouldBeEmpty();
        var contents = await ReadText(repository.Path, File);
        contents.ShouldBe(original);
    }

    [Fact]
    public async Task Update_of_a_consistent_file_has_nothing_to_do()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, File, TestRecipients.Yaml([TestRecipients.First, TestRecipients.Second]));

        // Act
        var run = await Run(repository.Path, "recipients", "update", File);

        // Assert
        run.ExitCode.ShouldBe(0, run.Error);
        run.Output.ShouldContain("labels already consistent with the rule; nothing to update.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Update_with_force_replaces_the_file_with_the_checked_new_version_and_suggests_rotating()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, File, TestRecipients.Yaml([TestRecipients.First, TestRecipients.Third]));
        var updated = TestRecipients.Yaml([TestRecipients.First, TestRecipients.Second]);
        var runner = new ScriptedToolRunner(request =>
        {
            System.IO.File.WriteAllText(request.Arguments[^1], updated);
            return ScriptedToolRunner.Result(0, string.Empty);
        });

        // Act
        var run = await Run(repository.Path, runner, ResolveAll, null, null, "recipients", "update", File, "--identity", "/keys/id.txt", "--force");

        // Assert
        run.ExitCode.ShouldBe(0, run.Error);
        var contents = await ReadText(repository.Path, File);
        contents.ShouldBe(updated);
        runner.Requests.ShouldHaveSingleItem().Arguments[2].ShouldBe("updatekeys");
        run.Output.ShouldContain("recipients updated; the data key is unchanged.", StringComparison.Ordinal);
        run.Output.ShouldContain($"run 'rotate {File}'", StringComparison.Ordinal);
        Directory.EnumerateFiles(Path.Combine(repository.Path, "secrets")).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task A_new_version_without_exactly_the_rules_recipients_is_never_written()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        var original = TestRecipients.Yaml([TestRecipients.First]);
        WriteFile(repository.Path, File, original);
        var runner = new ScriptedToolRunner(request =>
        {
            System.IO.File.WriteAllText(request.Arguments[^1], TestRecipients.Yaml([TestRecipients.First, TestRecipients.Third]));
            return ScriptedToolRunner.Result(0, string.Empty);
        });

        // Act
        var run = await Run(repository.Path, runner, ResolveAll, null, null, "recipients", "update", File, "--identity", "/keys/id.txt", "--force");

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("does not carry exactly the rule's recipients", StringComparison.Ordinal);
        var contents = await ReadText(repository.Path, File);
        contents.ShouldBe(original);
    }

    [Fact]
    public async Task Update_with_force_but_no_identity_is_refused()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, File, TestRecipients.Yaml([TestRecipients.First]));

        // Act
        var run = await Run(repository.Path, "recipients", "update", File, "--force");

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("No identity file", StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_weakened_file_is_refused_because_sops_would_keep_the_weakening()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, File, TestRecipients.Yaml([TestRecipients.First], "    unencrypted_regex: ^password$\n"));

        // Act
        var update = await Run(repository.Path, "recipients", "update", File);
        var rotate = await Run(repository.Path, "rotate", File);

        // Assert
        update.ExitCode.ShouldBe(2);
        update.Error.ShouldContain("weakened", StringComparison.Ordinal);
        rotate.ExitCode.ShouldBe(2);
    }

    [Fact]
    public async Task Rotate_previews_by_default_and_with_force_writes_the_rotated_version()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, File, TestRecipients.Yaml([TestRecipients.First, TestRecipients.Second]));
        var rotated = TestRecipients.Yaml([TestRecipients.Second, TestRecipients.First], dataLines: "rotated: " + TestRecipients.Encrypted + "\n");
        var runner = ScriptedToolRunner.Succeeding(rotated);

        // Act
        var preview = await Run(repository.Path, ScriptedToolRunner.Unused(), ResolveAll, null, null, "rotate", File);
        var forced = await Run(repository.Path, runner, ResolveAll, null, new Dictionary<string, string> { ["SOPS_AGE_KEY_FILE"] = "/keys/id.txt" }, "rotate", File, "--force");

        // Assert
        preview.ExitCode.ShouldBe(0);
        preview.Output.ShouldContain("a new data key for the 2 recipients of its rule", StringComparison.Ordinal);
        preview.Output.ShouldContain("Preview: nothing was written.", StringComparison.Ordinal);
        forced.ExitCode.ShouldBe(0, forced.Error);
        forced.Output.ShouldContain($"{File}: rotated.", StringComparison.Ordinal);
        runner.Requests.ShouldHaveSingleItem().Arguments.ShouldBe(["--config", "/dev/null", "rotate", Path.Combine(repository.Path, "secrets", "app.sops.yaml")]);
        var contents = await ReadText(repository.Path, File);
        contents.ShouldBe(rotated);
    }

    [Fact]
    public async Task A_failed_rotation_writes_nothing()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        var original = TestRecipients.Yaml([TestRecipients.First, TestRecipients.Second]);
        WriteFile(repository.Path, File, original);

        // Act
        var run = await Run(repository.Path, ScriptedToolRunner.Failing(128, "Failed to get the data key"), ResolveAll, null, null, "rotate", File, "--identity", "/keys/id.txt", "--force");

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("sops exited with code 128", StringComparison.Ordinal);
        run.Error.ShouldContain("Nothing was written.", StringComparison.Ordinal);
        var contents = await ReadText(repository.Path, File);
        contents.ShouldBe(original);
    }

    [Fact]
    public async Task Rotate_refuses_a_file_with_a_non_age_key_holder_before_running_sops()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, "secrets/vault.sops.json", TestRecipients.Json([TestRecipients.First, TestRecipients.Second], ",\"hc_vault\":[{\"vault_address\":\"https://v\"}]"));
        var runner = ScriptedToolRunner.Unused();

        // Act
        var run = await Run(repository.Path, runner, ResolveAll, null, null, "rotate", "secrets/vault.sops.json", "--identity", "/keys/id.txt", "--force");

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("run 'recipients update secrets/vault.sops.json' first", StringComparison.Ordinal);
        run.Output.ShouldNotContain("remove non-age key holder", StringComparison.Ordinal);
        runner.Requests.ShouldBeEmpty();
    }

    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task A_fifo_named_like_a_sops_file_does_not_hang_the_rewriting_commands()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        Directory.CreateDirectory(Path.Combine(repository.Path, "secrets"));
        using var made = await new SystemToolRunner().Run(new ToolRequest("/usr/bin/mkfifo", [Path.Combine(repository.Path, File)]), TestContext.Current.CancellationToken);

        // Act
        var update = await Run(repository.Path, "recipients", "update", File).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);
        var rotate = await Run(repository.Path, "rotate", File).WaitAsync(TimeSpan.FromSeconds(30), TestContext.Current.CancellationToken);

        // Assert
        made.ExitCode.ShouldBe(0);
        update.ExitCode.ShouldBe(2);
        update.Error.ShouldContain("cannot be read", StringComparison.Ordinal);
        rotate.ExitCode.ShouldBe(2);
    }
}
