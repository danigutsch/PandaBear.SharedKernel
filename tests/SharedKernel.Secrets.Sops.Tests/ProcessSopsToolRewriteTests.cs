namespace PandaBear.Secrets.Sops.Tests;

/// <summary>Tests the sops operations that produce a new version of an encrypted file, with a fake process runner.</summary>
[Trait("Capability", "Secrets")]
public sealed class ProcessSopsToolRewriteTests
{
    private const string Sops = "/usr/local/bin/sops";

    private static readonly AgeIdentityRef Identity = AgeIdentityRef.Create("/keys/id.txt").ShouldBeSuccess();

    [Fact]
    public async Task UpdateKeys_syncs_a_private_copy_to_a_config_that_lists_exactly_the_recipients()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var file = Path.Combine(directory.Path, "app.sops.yaml");
        var original = SopsSamples.Yaml([TestRecipients.First]);
        await File.WriteAllTextAsync(file, original, TestContext.Current.CancellationToken);
        string? config = null;
        string? workingDirectory = null;
        var runner = new FakeToolRunner(request =>
        {
            config = File.ReadAllText(request.Arguments[1]);
            workingDirectory = request.WorkingDirectory;
            File.WriteAllText(request.Arguments[^1], "updated: document\n");
            return FakeToolRunner.Result(0, string.Empty);
        });
        var tool = new ProcessSopsTool(runner, Sops);

        // Act
        var result = await tool.UpdateKeys(file, Identity, TestRecipients.Set(TestRecipients.First, TestRecipients.Second), TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeSuccess().ShouldBe("updated: document\n");
        var request = runner.Requests.ShouldHaveSingleItem();
        request.Arguments[0].ShouldBe("--config");
        request.Arguments.Skip(2).ToArray().ShouldBe(["updatekeys", "--yes", "--input-type", "yaml", Path.Combine(workingDirectory!, "data", "app.sops.yaml")]);
        request.Arguments[1].ShouldBe(Path.Combine(workingDirectory!, ".sops.yaml"));
        config.ShouldBe($"creation_rules:\n  - age: \"{TestRecipients.Second},{TestRecipients.First}\"\n");
        request.Environment.ShouldBe(new Dictionary<string, string> { ["HOME"] = workingDirectory!, ["SOPS_AGE_KEY_FILE"] = Identity.Path });
        Directory.Exists(workingDirectory).ShouldBeFalse();
        var unchanged = await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken);
        unchanged.ShouldBe(original);
    }

    [Fact]
    public async Task A_failed_update_is_an_error_and_leaves_no_directory_behind()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var file = Path.Combine(directory.Path, "app.sops.yaml");
        await File.WriteAllTextAsync(file, SopsSamples.Yaml([TestRecipients.First]), TestContext.Current.CancellationToken);
        string? workingDirectory = null;
        var runner = new FakeToolRunner(request =>
        {
            workingDirectory = request.WorkingDirectory;
            return FakeToolRunner.Result(128, string.Empty, "Failed to get the data key required to decrypt the SOPS file.");
        });

        // Act
        var result = await new ProcessSopsTool(runner, Sops).UpdateKeys(file, Identity, TestRecipients.Set(TestRecipients.Second), TestContext.Current.CancellationToken);

        // Assert
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldBe("sops exited with code 128: Failed to get the data key required to decrypt the SOPS file.");
        Directory.Exists(workingDirectory).ShouldBeFalse();
    }

    [Fact]
    public async Task Rotate_adds_and_removes_only_the_difference_and_returns_standard_output()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var file = Path.Combine(directory.Path, "app.sops.yaml");
        await File.WriteAllTextAsync(file, SopsSamples.Yaml([TestRecipients.First, TestRecipients.Third]), TestContext.Current.CancellationToken);
        var runner = FakeToolRunner.Succeeding("rotated: document\n");

        // Act
        var result = await new ProcessSopsTool(runner, Sops).Rotate(file, Identity, TestRecipients.Set(TestRecipients.First, TestRecipients.Second), TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeSuccess().ShouldBe("rotated: document\n");
        var request = runner.Requests.ShouldHaveSingleItem();
        request.Arguments.ShouldBe(["--config", "/dev/null", "rotate", "--input-type", "yaml", "--output-type", "yaml", "--add-age", TestRecipients.Second, "--rm-age", TestRecipients.Third, file]);
        request.Environment["SOPS_AGE_KEY_FILE"].ShouldBe(Identity.Path);
    }

    [Fact]
    public async Task Rotate_to_the_same_recipients_only_replaces_the_data_key()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var file = Path.Combine(directory.Path, "app.sops.json");
        await File.WriteAllTextAsync(file, SopsSamples.Json([TestRecipients.First]), TestContext.Current.CancellationToken);
        var runner = FakeToolRunner.Succeeding("{}");

        // Act
        var result = await new ProcessSopsTool(runner, Sops).Rotate(file, Identity, TestRecipients.Set(TestRecipients.First), TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeSuccess();
        runner.Requests.ShouldHaveSingleItem().Arguments.ShouldBe(["--config", "/dev/null", "rotate", "--input-type", "json", "--output-type", "json", file]);
    }

    [Fact]
    public async Task Rotate_refuses_a_file_whose_recipients_cannot_be_read_without_starting_sops()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var file = Path.Combine(directory.Path, "app.sops.yaml");
        await File.WriteAllTextAsync(file, "plain: text\n", TestContext.Current.CancellationToken);
        var runner = FakeToolRunner.Succeeding();

        // Act
        var result = await new ProcessSopsTool(runner, Sops).Rotate(file, Identity, TestRecipients.Set(TestRecipients.First), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        runner.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("relative/app.sops.yaml")]
    [InlineData("")]
    public async Task UpdateKeys_and_rotate_refuse_a_path_that_is_not_a_safe_absolute_path(string file)
    {
        // Arrange
        var runner = FakeToolRunner.Succeeding();
        var tool = new ProcessSopsTool(runner, Sops);
        var recipients = TestRecipients.Set(TestRecipients.First);

        // Act
        var updated = await tool.UpdateKeys(file, Identity, recipients, TestContext.Current.CancellationToken);
        var rotated = await tool.Rotate(file, Identity, recipients, TestContext.Current.CancellationToken);

        // Assert
        updated.IsFailure.ShouldBeTrue();
        rotated.IsFailure.ShouldBeTrue();
        runner.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Null_arguments_are_refused()
    {
        // Arrange
        var tool = new ProcessSopsTool(FakeToolRunner.Succeeding(), Sops);
        var recipients = TestRecipients.Set(TestRecipients.First);
        var token = TestContext.Current.CancellationToken;

        // Act
        var calls = new Func<Task>[]
        {
            () => tool.UpdateKeys(null!, Identity, recipients, token),
            () => tool.UpdateKeys("/a.sops.yaml", null!, recipients, token),
            () => tool.UpdateKeys("/a.sops.yaml", Identity, null!, token),
            () => tool.Rotate(null!, Identity, recipients, token),
            () => tool.Rotate("/a.sops.yaml", null!, recipients, token),
            () => tool.Rotate("/a.sops.yaml", Identity, null!, token),
        };

        // Assert
        foreach (var call in calls)
        {
            await call.ShouldThrow<ArgumentNullException>();
        }
    }

    [Fact(SkipType = typeof(UnixModes), SkipUnless = nameof(UnixModes.Available), Skip = "Unix file modes only.")]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task The_copy_that_updatekeys_rewrites_is_writable_even_for_a_read_only_file()
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var file = Path.Combine(directory.Path, "app.sops.yaml");
        await File.WriteAllTextAsync(file, SopsSamples.Yaml([TestRecipients.First]), TestContext.Current.CancellationToken);
        File.SetUnixFileMode(file, UnixFileMode.UserRead);
        UnixFileMode? copyMode = null;
        var runner = new FakeToolRunner(request =>
        {
            copyMode = File.GetUnixFileMode(request.Arguments[^1]);
            return FakeToolRunner.Result(0, string.Empty);
        });

        // Act
        var result = await new ProcessSopsTool(runner, Sops).UpdateKeys(file, Identity, TestRecipients.Set(TestRecipients.Second), TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeSuccess();
        copyMode.ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}
