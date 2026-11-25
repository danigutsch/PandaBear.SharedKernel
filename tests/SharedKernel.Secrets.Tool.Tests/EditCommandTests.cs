using static SharedKernel.Secrets.Tool.Tests.SecretsToolHarness;

namespace SharedKernel.Secrets.Tool.Tests;

/// <summary>Tests <c>edit</c> with a fake sops: a terminal, an editor, and a memory-backed private directory that is removed afterwards.</summary>
[Trait(TestTraitNames.CapabilityName, "Secrets")]
public sealed class EditCommandTests
{
    private const string File = "secrets/app.sops.yaml";
    private const string MemorySkip = "/dev/shm is not a tmpfs here.";

    private static readonly string Config = TestRecipients.Config(1, TestRecipients.First);

    [Fact(SkipType = typeof(MemoryRoot), SkipUnless = nameof(MemoryRoot.Available), Skip = MemorySkip)]
    public async Task Edit_saves_the_new_version_and_removes_the_private_directory()
    {
        // Arrange
        using var memory = MemoryRoot.Create();
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, File, TestRecipients.Yaml([TestRecipients.First]));
        var edited = TestRecipients.Yaml([TestRecipients.First], dataLines: "added: " + TestRecipients.Encrypted + "\n");
        var runner = ScriptedToolRunner.Unused();
        string? privateDirectory = null;
        runner.Attached = request =>
        {
            privateDirectory = request.WorkingDirectory;
            System.IO.File.WriteAllText(request.Arguments[^1], edited);
            return 0;
        };
        var variables = MemoryRoot.Variables(memory.Path);

        // Act
        var run = await RunInTerminal(repository.Path, runner, variables, "edit", File);

        // Assert
        run.ExitCode.ShouldBe(0, run.Error);
        run.Output.ShouldBe($"{File}: saved.\n");
        var saved = await ReadText(repository.Path, File);
        saved.ShouldBe(edited);
        var request = runner.Requests.ShouldHaveSingleItem();
        request.Environment["EDITOR"].ShouldBe("'/fake/vim' '-n'");
        request.Environment["TERM"].ShouldBe("xterm-256color");
        request.Environment.ContainsKey("GH_TOKEN").ShouldBeFalse();
        Path.GetDirectoryName(privateDirectory).ShouldBe(memory.Path);
        Directory.EnumerateFileSystemEntries(memory.Path).ShouldBeEmpty();
    }

    [Fact(SkipType = typeof(MemoryRoot), SkipUnless = nameof(MemoryRoot.Available), Skip = MemorySkip)]
    public async Task An_edit_that_changed_nothing_leaves_the_file_alone()
    {
        // Arrange
        using var memory = MemoryRoot.Create();
        using var repository = CreateRepository(Config);
        var original = TestRecipients.Yaml([TestRecipients.First]);
        WriteFile(repository.Path, File, original);
        var runner = ScriptedToolRunner.Unused();
        runner.Attached = static _ => 200;

        // Act
        var run = await RunInTerminal(repository.Path, runner, MemoryRoot.Variables(memory.Path), "edit", File);

        // Assert
        run.ExitCode.ShouldBe(0, run.Error);
        run.Output.ShouldBe($"{File}: unchanged.\n");
        var kept = await ReadText(repository.Path, File);
        kept.ShouldBe(original);
        Directory.EnumerateFileSystemEntries(memory.Path).ShouldBeEmpty();
    }

    [Fact]
    public async Task Edit_without_a_terminal_is_refused()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, File, TestRecipients.Yaml([TestRecipients.First]));

        // Act
        var run = await Run(repository.Path, ScriptedToolRunner.Unused(), ResolveAll, null, MemoryRoot.Variables("/dev/shm"), "edit", File);

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("edit needs a terminal", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Edit_without_an_editor_is_refused()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, File, TestRecipients.Yaml([TestRecipients.First]));
        var variables = new Dictionary<string, string> { ["SOPS_AGE_KEY_FILE"] = "/keys/id.txt" };

        // Act
        var run = await RunInTerminal(repository.Path, ScriptedToolRunner.Unused(), variables, "edit", File);

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("No editor: set VISUAL or EDITOR.", StringComparison.Ordinal);
    }

    [Fact(SkipType = typeof(MemoryRoot), SkipUnless = nameof(MemoryRoot.DiskAvailable), Skip = "The test output directory is not on a disk.")]
    public async Task A_temporary_root_on_a_disk_is_refused_before_anything_is_decrypted()
    {
        // Arrange
        var disk = MemoryRoot.Disk;
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, File, TestRecipients.Yaml([TestRecipients.First]));
        var runner = ScriptedToolRunner.Unused();

        // Act
        var run = await RunInTerminal(repository.Path, runner, MemoryRoot.Variables(disk), "edit", File);

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain("is not on a memory-backed file system", StringComparison.Ordinal);
        runner.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_file_whose_recipients_differ_from_its_rule_must_be_updated_first()
    {
        // Arrange
        using var repository = CreateRepository(Config);
        WriteFile(repository.Path, File, TestRecipients.Yaml([TestRecipients.Second]));
        var runner = ScriptedToolRunner.Unused();

        // Act
        var run = await RunInTerminal(repository.Path, runner, MemoryRoot.Variables("/dev/shm"), "edit", File);

        // Assert
        run.ExitCode.ShouldBe(2);
        run.Error.ShouldContain($"run 'recipients update {File}' first", StringComparison.Ordinal);
        runner.Requests.ShouldBeEmpty();
    }
}
