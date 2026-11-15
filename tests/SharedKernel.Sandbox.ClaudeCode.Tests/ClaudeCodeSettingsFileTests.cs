namespace PandaBear.Sandbox.ClaudeCode.Tests;

/// <summary>Tests planning and writing a Claude Code settings file under a repository root.</summary>
[Trait("Capability", "Sandbox")]
public sealed class ClaudeCodeSettingsFileTests
{
    private static readonly DesiredSettings Desired = new(null, ["./out"], [], ["~/.ssh"], [], [], [], false, new Dictionary<string, string>(), []);

    [Fact]
    public async Task Without_a_target_the_shared_project_file_is_planned_and_written()
    {
        // Arrange
        using var root = new TemporaryDirectory();
        var file = ClaudeCodeSettingsFile.Locate(root.Path, null);

        // Act
        var plan = await file.Plan(Desired, TestContext.Current.CancellationToken);
        file.Write(plan);
        var second = await file.Plan(Desired, TestContext.Current.CancellationToken);

        // Assert
        file.Target.ShouldBe(".claude/settings.json");
        plan.ExpectedContent.ShouldBeNull();
        plan.Changes.Select(static change => change.Location + " " + change.Value).ToArray().ShouldBe(["$.sandbox.filesystem.allowWrite ./out", "$.sandbox.filesystem.denyRead ~/.ssh"]);
        var written = await File.ReadAllTextAsync(Path.Combine(root.Path, ".claude", "settings.json"), TestContext.Current.CancellationToken);
        written.ShouldBe(plan.Content);
        second.Changes.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_file_that_changed_after_the_plan_is_not_overwritten()
    {
        // Arrange
        using var root = new TemporaryDirectory();
        var file = ClaudeCodeSettingsFile.Locate(root.Path, ".claude/settings.local.json");
        var plan = await file.Plan(Desired, TestContext.Current.CancellationToken);
        var path = Path.Combine(root.Path, ".claude", "settings.local.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(path, "{}", TestContext.Current.CancellationToken);

        // Act
        var exception = new Action(() => file.Write(plan)).ShouldThrow<InvalidOperationException>();

        var content = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);

        // Assert
        exception.Message.ShouldBe(".claude/settings.local.json changed after the plan was made; nothing was written. Run the command again.");
        content.ShouldBe("{}");
    }

    [Theory]
    [InlineData("../settings.json", "../settings.json: ")]
    [InlineData(".claude/settings.yaml", ".config/sharedkernel-sandbox.json: $.settings.target must name a .json file.")]
    public void A_target_outside_the_root_or_not_JSON_is_refused(string target, string expected)
    {
        // Arrange
        using var root = new TemporaryDirectory();

        // Act
        var exception = new Func<object?>(() => ClaudeCodeSettingsFile.Locate(root.Path, target)).ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldStartWith(expected, StringComparison.Ordinal);
    }
}
