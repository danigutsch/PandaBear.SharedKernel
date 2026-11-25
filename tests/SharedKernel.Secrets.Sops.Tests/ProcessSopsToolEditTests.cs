namespace SharedKernel.Secrets.Sops.Tests;

/// <summary>Tests <c>sops edit</c> through the adapter with a fake process runner.</summary>
[Trait(TestTraitNames.CapabilityName, "Secrets")]
public sealed class ProcessSopsToolEditTests
{
    private const string Sops = "/usr/local/bin/sops";

    private static readonly AgeIdentityRef Identity = AgeIdentityRef.Create("/keys/id.txt").ShouldBeSuccess();

    private static readonly byte[] Original = "original: document\n"u8.ToArray();

    [Fact]
    public async Task Edit_runs_sops_on_a_copy_in_the_private_directory_with_its_home_temporary_directory_and_editor()
    {
        // Arrange
        using var private_ = new TemporaryDirectory();
        var editor = EditorCommand.Create(Environment.ProcessPath + " -n", new ExecutableMap()).ShouldBeSuccess();
        var session = new SopsEditSession
        {
            Editor = editor,
            PrivateDirectory = private_.Path,
            TerminalEnvironment = new Dictionary<string, string> { ["TERM"] = "xterm", ["HOME"] = "/home/me", ["EDITOR"] = "nano" },
        };
        var runner = FakeToolRunner.Succeeding();
        runner.Attached = request =>
        {
            File.WriteAllText(request.Arguments[^1], "edited: document\n");
            return 0;
        };

        // Act
        var result = await new ProcessSopsTool(runner, Sops).Edit(Original, SopsFormat.Yaml, Identity, session, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeSuccess().ShouldBe("edited: document\n");
        var request = runner.Requests.ShouldHaveSingleItem();
        request.Arguments.ShouldBe(["--config", "/dev/null", "edit", "--input-type", "yaml", "--output-type", "yaml", Path.Combine(private_.Path, "data", "document.sops.yaml")]);
        request.WorkingDirectory.ShouldBe(private_.Path);
        request.Environment.ShouldBe(new Dictionary<string, string>
        {
            ["TERM"] = "xterm",
            ["HOME"] = private_.Path,
            ["TMPDIR"] = private_.Path,
            ["EDITOR"] = $"'{Environment.ProcessPath}' '-n'",
            ["SOPS_AGE_KEY_FILE"] = Identity.Path,
        });
        Directory.EnumerateFileSystemEntries(private_.Path).ShouldBeEmpty();
    }

    [Fact]
    public async Task An_edit_that_changed_nothing_returns_the_document_as_it_was()
    {
        // Arrange
        using var private_ = new TemporaryDirectory();
        var runner = FakeToolRunner.Succeeding();
        runner.Attached = static _ => 200;

        // Act
        var result = await new ProcessSopsTool(runner, Sops).Edit(Original, SopsFormat.Yaml, Identity, EditSessions.For(private_.Path), TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeSuccess().ShouldBe("original: document\n");
        Directory.EnumerateFileSystemEntries(private_.Path).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_failed_edit_is_an_error_and_leaves_nothing_in_the_private_directory()
    {
        // Arrange
        using var private_ = new TemporaryDirectory();
        var runner = FakeToolRunner.Succeeding();
        runner.Attached = static _ => 128;

        // Act
        var result = await new ProcessSopsTool(runner, Sops).Edit(Original, SopsFormat.Yaml, Identity, EditSessions.For(private_.Path), TestContext.Current.CancellationToken);

        // Assert
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldBe("sops edit exited with code 128.");
        Directory.EnumerateFileSystemEntries(private_.Path).ShouldBeEmpty();
    }

    [Fact]
    public async Task Edit_refuses_a_relative_private_directory_without_starting_sops()
    {
        // Arrange
        var runner = FakeToolRunner.Succeeding();

        // Act
        var result = await new ProcessSopsTool(runner, Sops).Edit(Original, SopsFormat.Yaml, Identity, EditSessions.For("private"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        runner.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Null_arguments_are_refused()
    {
        // Arrange
        var tool = new ProcessSopsTool(FakeToolRunner.Succeeding(), Sops);
        var token = TestContext.Current.CancellationToken;
        var session = EditSessions.For("/private");

        // Act
        var calls = new Func<Task>[]
        {
            () => tool.Edit(Original, SopsFormat.Yaml, null!, session, token),
            () => tool.Edit(Original, SopsFormat.Yaml, Identity, null!, token),
        };

        // Assert
        foreach (var call in calls)
        {
            await call.ShouldThrow<ArgumentNullException>();
        }
    }

    [Fact(SkipType = typeof(UnixModes), SkipUnless = nameof(UnixModes.Available), Skip = "Unix file modes only.")]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task The_copy_that_sops_edits_is_owner_only_and_writable()
    {
        // Arrange
        using var private_ = new TemporaryDirectory();
        UnixFileMode? copyMode = null;
        var runner = FakeToolRunner.Succeeding();
        runner.Attached = request =>
        {
            copyMode = File.GetUnixFileMode(request.Arguments[^1]);
            return 200;
        };

        // Act
        var result = await new ProcessSopsTool(runner, Sops).Edit(Original, SopsFormat.Yaml, Identity, EditSessions.For(private_.Path), TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeSuccess();
        copyMode.ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}
