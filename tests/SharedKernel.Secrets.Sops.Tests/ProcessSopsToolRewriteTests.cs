using System.Text;

namespace PandaBear.Secrets.Sops.Tests;

/// <summary>Tests the sops operations that produce a new version of an encrypted document, with a fake process runner.</summary>
[Trait("Capability", "Secrets")]
public sealed class ProcessSopsToolRewriteTests
{
    private const string Sops = "/usr/local/bin/sops";

    private static readonly AgeIdentityRef Identity = AgeIdentityRef.Create("/keys/id.txt").ShouldBeSuccess();

    [Fact]
    public async Task UpdateKeys_syncs_a_private_copy_of_the_document_to_a_config_that_lists_exactly_the_recipients()
    {
        // Arrange
        var document = Encoding.UTF8.GetBytes(SopsSamples.Yaml([TestRecipients.First]));
        string? config = null;
        string? copied = null;
        string? workingDirectory = null;
        var runner = new FakeToolRunner(request =>
        {
            config = File.ReadAllText(request.Arguments[1]);
            copied = File.ReadAllText(request.Arguments[^1]);
            workingDirectory = request.WorkingDirectory;
            File.WriteAllText(request.Arguments[^1], "updated: document\n");
            return FakeToolRunner.Result(0, string.Empty);
        });
        var tool = new ProcessSopsTool(runner, Sops);

        // Act
        var result = await tool.UpdateKeys(document, SopsFormat.Yaml, Identity, TestRecipients.Set(TestRecipients.First, TestRecipients.Second), TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeSuccess().ShouldBe("updated: document\n");
        var request = runner.Requests.ShouldHaveSingleItem();
        request.Arguments[0].ShouldBe("--config");
        request.Arguments[1].ShouldBe(Path.Combine(workingDirectory!, ".sops.yaml"));
        request.Arguments.Skip(2).ToArray().ShouldBe(["updatekeys", "--yes", "--input-type", "yaml", Path.Combine(workingDirectory!, "data", "document.sops.yaml")]);
        config.ShouldBe($"creation_rules:\n  - age: \"{TestRecipients.Second},{TestRecipients.First}\"\n");
        copied.ShouldBe(Encoding.UTF8.GetString(document));
        request.Environment.ShouldBe(new Dictionary<string, string> { ["HOME"] = workingDirectory!, ["SOPS_AGE_KEY_FILE"] = Identity.Path });
        Directory.Exists(workingDirectory).ShouldBeFalse();
    }

    [Fact(SkipType = typeof(UnixModes), SkipUnless = nameof(UnixModes.Available), Skip = "Unix file modes only.")]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task The_copy_that_updatekeys_rewrites_is_owner_only_and_writable()
    {
        // Arrange
        UnixFileMode? copyMode = null;
        var runner = new FakeToolRunner(request =>
        {
            copyMode = File.GetUnixFileMode(request.Arguments[^1]);
            return FakeToolRunner.Result(0, string.Empty);
        });

        // Act
        var result = await new ProcessSopsTool(runner, Sops).UpdateKeys(Encoding.UTF8.GetBytes(SopsSamples.Yaml([TestRecipients.First])), SopsFormat.Yaml, Identity, TestRecipients.Set(TestRecipients.Second), TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeSuccess();
        copyMode.ShouldBe(UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    [Fact]
    public async Task A_failed_update_is_an_error_and_leaves_no_directory_behind()
    {
        // Arrange
        string? workingDirectory = null;
        var runner = new FakeToolRunner(request =>
        {
            workingDirectory = request.WorkingDirectory;
            return FakeToolRunner.Result(128, string.Empty, "Failed to get the data key required to decrypt the SOPS file.");
        });

        // Act
        var result = await new ProcessSopsTool(runner, Sops).UpdateKeys(Encoding.UTF8.GetBytes(SopsSamples.Yaml([TestRecipients.First])), SopsFormat.Yaml, Identity, TestRecipients.Set(TestRecipients.Second), TestContext.Current.CancellationToken);

        // Assert
        result.ErrorDetails.ShouldNotBeNull().Detail.ShouldBe("sops exited with code 128: Failed to get the data key required to decrypt the SOPS file.");
        Directory.Exists(workingDirectory).ShouldBeFalse();
    }

    [Fact]
    public async Task Rotate_adds_and_removes_only_the_difference_and_returns_standard_output()
    {
        // Arrange
        var document = Encoding.UTF8.GetBytes(SopsSamples.Yaml([TestRecipients.First, TestRecipients.Third]));
        string? copied = null;
        var runner = new FakeToolRunner(request =>
        {
            copied = File.ReadAllText(request.Arguments[^1]);
            return FakeToolRunner.Result(0, "rotated: document\n");
        });

        // Act
        var result = await new ProcessSopsTool(runner, Sops).Rotate(document, SopsFormat.Yaml, Identity, TestRecipients.Set(TestRecipients.First, TestRecipients.Second), TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeSuccess().ShouldBe("rotated: document\n");
        var request = runner.Requests.ShouldHaveSingleItem();
        request.Arguments.ShouldBe(["--config", "/dev/null", "rotate", "--input-type", "yaml", "--output-type", "yaml", "--add-age", TestRecipients.Second, "--rm-age", TestRecipients.Third, Path.Combine(request.WorkingDirectory!, "document.sops.yaml")]);
        request.Environment["SOPS_AGE_KEY_FILE"].ShouldBe(Identity.Path);
        copied.ShouldBe(Encoding.UTF8.GetString(document));
        Directory.Exists(request.WorkingDirectory).ShouldBeFalse();
    }

    [Fact]
    public async Task Rotate_to_the_same_recipients_only_replaces_the_data_key()
    {
        // Arrange
        var runner = FakeToolRunner.Succeeding("{}");

        // Act
        var result = await new ProcessSopsTool(runner, Sops).Rotate(Encoding.UTF8.GetBytes(SopsSamples.Json([TestRecipients.First])), SopsFormat.Json, Identity, TestRecipients.Set(TestRecipients.First), TestContext.Current.CancellationToken);

        // Assert
        result.ShouldBeSuccess();
        var request = runner.Requests.ShouldHaveSingleItem();
        request.Arguments.ShouldBe(["--config", "/dev/null", "rotate", "--input-type", "json", "--output-type", "json", Path.Combine(request.WorkingDirectory!, "document.sops.json")]);
    }

    [Fact]
    public async Task Rotate_refuses_a_document_whose_recipients_cannot_be_read_without_starting_sops()
    {
        // Arrange
        var runner = FakeToolRunner.Succeeding();

        // Act
        var result = await new ProcessSopsTool(runner, Sops).Rotate("plain: text\n"u8.ToArray(), SopsFormat.Yaml, Identity, TestRecipients.Set(TestRecipients.First), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.ShouldBeTrue();
        runner.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Null_arguments_are_refused()
    {
        // Arrange
        var tool = new ProcessSopsTool(FakeToolRunner.Succeeding(), Sops);
        var recipients = TestRecipients.Set(TestRecipients.First);
        var token = TestContext.Current.CancellationToken;
        var document = Encoding.UTF8.GetBytes(SopsSamples.Yaml([TestRecipients.First]));

        // Act
        var calls = new Func<Task>[]
        {
            () => tool.UpdateKeys(document, SopsFormat.Yaml, null!, recipients, token),
            () => tool.UpdateKeys(document, SopsFormat.Yaml, Identity, null!, token),
            () => tool.Rotate(document, SopsFormat.Yaml, null!, recipients, token),
            () => tool.Rotate(document, SopsFormat.Yaml, Identity, null!, token),
        };

        // Assert
        foreach (var call in calls)
        {
            await call.ShouldThrow<ArgumentNullException>();
        }
    }
}
