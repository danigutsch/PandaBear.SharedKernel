namespace SharedKernel.Sandbox.Tests;

/// <summary>Tests how <c>allowWrite</c> entries are classified.</summary>
[Trait(TestTraitNames.CapabilityName, "Sandbox")]
public sealed class WritePathPolicyTests
{
    private static readonly string[] Homes = ["/home/user"];

    [Theory]
    [InlineData("/")]
    [InlineData("/home")]
    [InlineData("/home/user")]
    [InlineData("/home//user/.")]
    [InlineData("/usr")]
    public void A_root_home_or_ancestor_of_home_is_broad(string entry) =>
        WritePathPolicy.Classify(entry, Homes).HasFlag(WriteRisk.Broad).ShouldBeTrue();

    [Theory]
    [InlineData("~/.bashrc")]
    [InlineData("~/.ssh/config")]
    [InlineData("~/.config")]
    [InlineData("/home/user/.gnupg")]
    [InlineData("./.git")]
    [InlineData("./.git/hooks/pre-commit")]
    public void Startup_files_credential_folders_and_git_hooks_are_sensitive(string entry) =>
        WritePathPolicy.Classify(entry, Homes).HasFlag(WriteRisk.Sensitive).ShouldBeTrue();

    [Theory]
    [InlineData("~/.config/other")]
    [InlineData("./build")]
    [InlineData("./.github")]
    [InlineData("/tmp/build")]
    public void An_ordinary_tool_folder_has_no_risk(string entry) =>
        WritePathPolicy.Classify(entry, Homes).ShouldBe(WriteRisk.None);

    [Fact]
    public void Empty_home_entries_are_ignored()
    {
        // Act
        var risk = WritePathPolicy.Classify("/tmp/build", ["", " "]);

        // Assert
        risk.ShouldBe(WriteRisk.None);
    }
}
