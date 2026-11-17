namespace PandaBear.Secrets.Sops.Tests;

/// <summary>Tests <c>sops edit</c> through the adapter with a fake process runner.</summary>
[Trait("Capability", "Secrets")]
public sealed class ProcessSopsToolEditTests
{
    private const string Sops = "/usr/local/bin/sops";

    private static readonly AgeIdentityRef Identity = AgeIdentityRef.Create("/keys/id.txt").ShouldBeSuccess();

    [Fact]
    public async Task Edit_runs_sops_on_a_copy_in_the_private_directory_with_its_home_temporary_directory_and_editor()
    {
        // Arrange
        using var repository = new TemporaryDirectory();
        using var private_ = new TemporaryDirectory();
        var file = Path.Combine(repository.Path, "app.sops.yaml");
        await File.WriteAllTextAsync(file, "original: document\n", TestContext.Current.CancellationToken);
        var editor = EditorCommand.Create(Environment.ProcessPath + " -n", new NoExecutables()).ShouldBeSuccess();
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
        var result = await new ProcessSopsTool(runner, Sops).Edit(file, Identity, session, TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeSuccess().ShouldBe("edited: document\n");
        var request = runner.Requests.ShouldHaveSingleItem();
        request.Arguments.ShouldBe(["--config", "/dev/null", "edit", Path.Combine(private_.Path, "data", "app.sops.yaml")]);
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
        var unchanged = await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken);
        unchanged.ShouldBe("original: document\n");
    }

    [Fact]
    public async Task An_edit_that_changed_nothing_returns_the_document_as_it_was()
    {
        // Arrange
        using var repository = new TemporaryDirectory();
        using var private_ = new TemporaryDirectory();
        var file = Path.Combine(repository.Path, "app.sops.yaml");
        await File.WriteAllTextAsync(file, "original: document\n", TestContext.Current.CancellationToken);
        var runner = FakeToolRunner.Succeeding();
        runner.Attached = static _ => 200;

        // Act
        var result = await new ProcessSopsTool(runner, Sops).Edit(file, Identity, EditSessions.For(private_.Path), TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeSuccess().ShouldBe("original: document\n");
        Directory.EnumerateFileSystemEntries(private_.Path).ShouldBeEmpty();
    }

    [Fact]
    public async Task A_failed_edit_is_an_error_and_leaves_nothing_in_the_private_directory()
    {
        // Arrange
        using var repository = new TemporaryDirectory();
        using var private_ = new TemporaryDirectory();
        var file = Path.Combine(repository.Path, "app.sops.yaml");
        await File.WriteAllTextAsync(file, "original: document\n", TestContext.Current.CancellationToken);
        var runner = FakeToolRunner.Succeeding();
        runner.Attached = static _ => 128;

        // Act
        var result = await new ProcessSopsTool(runner, Sops).Edit(file, Identity, EditSessions.For(private_.Path), TestContext.Current.CancellationToken);

        // Assert
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldBe("sops edit exited with code 128.");
        Directory.EnumerateFileSystemEntries(private_.Path).ShouldBeEmpty();
    }

    [Fact]
    public async Task Edit_refuses_a_relative_file_or_private_directory_without_starting_sops()
    {
        // Arrange
        using var private_ = new TemporaryDirectory();
        var runner = FakeToolRunner.Succeeding();
        var tool = new ProcessSopsTool(runner, Sops);

        // Act
        var relativeFile = await tool.Edit("app.sops.yaml", Identity, EditSessions.For(private_.Path), TestContext.Current.CancellationToken);
        var relativeDirectory = await tool.Edit("/repo/app.sops.yaml", Identity, EditSessions.For("private"), TestContext.Current.CancellationToken);

        // Assert
        relativeFile.IsFailure.ShouldBeTrue();
        relativeDirectory.IsFailure.ShouldBeTrue();
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
            () => tool.Edit(null!, Identity, session, token),
            () => tool.Edit("/a.sops.yaml", null!, session, token),
            () => tool.Edit("/a.sops.yaml", Identity, null!, token),
        };

        // Assert
        foreach (var call in calls)
        {
            await call.ShouldThrow<ArgumentNullException>();
        }
    }

    [Fact(SkipType = typeof(UnixModes), SkipUnless = nameof(UnixModes.Available), Skip = "Unix file modes only.")]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task The_copy_of_a_read_only_file_is_writable_so_sops_can_save_the_edit()
    {
        // Arrange
        using var repository = new TemporaryDirectory();
        using var private_ = new TemporaryDirectory();
        var file = Path.Combine(repository.Path, "app.sops.yaml");
        await File.WriteAllTextAsync(file, "original: document\n", TestContext.Current.CancellationToken);
        File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        UnixFileMode? copyMode = null;
        var runner = FakeToolRunner.Succeeding();
        runner.Attached = request =>
        {
            copyMode = File.GetUnixFileMode(request.Arguments[^1]);
            return 200;
        };

        // Act
        var result = await new ProcessSopsTool(runner, Sops).Edit(file, Identity, EditSessions.For(private_.Path), TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeSuccess();
        copyMode.ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}
