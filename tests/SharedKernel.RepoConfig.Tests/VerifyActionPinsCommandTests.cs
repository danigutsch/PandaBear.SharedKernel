namespace PandaBear.RepoConfig.Tests;

/// <summary>
/// Tests the verify-action-pins command against workspaces with a manifest, workflows, and composite actions.
/// </summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
[Collection(RepoConfigToolEnvironmentGroup.Name)]
public sealed class VerifyActionPinsCommandTests
{
    private const string CheckoutSha = "de0fac2e4500dabe0009e67214ff5f5447ce83dd";
    private const string OtherSha = "043fb46d1a93c77aae656e7c1c64a875d1fc6a0a";

    private const string Manifest = $$"""
        {
          "version": 1,
          "firstParty": ["owner/first-party"],
          "actions": [
            { "repository": "actions/checkout", "sha": "{{CheckoutSha}}", "version": "v6.0.2" },
            { "repository": "github/codeql-action", "sha": "{{OtherSha}}", "version": "v4.1.0" }
          ]
        }
        """;

    [Fact]
    public async Task Verify_action_pins_accepts_pinned_commented_local_and_first_party_references()
    {
        // Arrange
        using var workspace = ActionPinTestWorkspace.Create(
            Manifest,
            $"""
            jobs:
              build:
                steps:
                  - uses: actions/checkout@{CheckoutSha} # v6.0.2
                  - uses: ./.github/actions/local
                  - uses: github/codeql-action/upload-sarif@{OtherSha} # v4.1.0
                  - uses: owner/first-party/.github/actions/tool@{OtherSha}
                  # - uses: actions/unknown@main
            """,
            $"""
            runs:
              using: composite
              steps:
                - uses: 'actions/checkout@{CheckoutSha}' # v6.0.2
            """);

        // Act
        var result = await LinkTestRepository.Run(workspace, ["verify-action-pins", "--root", workspace.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.StandardError.ShouldBe(string.Empty);
        result.StandardOutput.ShouldBe("Action pins match the manifest." + Environment.NewLine);
    }

    [Theory]
    [InlineData("actions/checkout@v6", "'actions/checkout' must pin a full 40-character lowercase commit SHA, not 'v6'.")]
    [InlineData("actions/checkout@main", "'actions/checkout' must pin a full 40-character lowercase commit SHA, not 'main'.")]
    [InlineData("actions/checkout", "'actions/checkout' must pin a full 40-character lowercase commit SHA, not ''.")]
    [InlineData("actions/checkout@DE0FAC2E4500DABE0009E67214FF5F5447CE83DD", "'actions/checkout' must pin a full 40-character lowercase commit SHA, not 'DE0FAC2E4500DABE0009E67214FF5F5447CE83DD'.")]
    [InlineData("docker://alpine:3", "'docker://alpine:3' is a container reference, which the manifest cannot approve.")]
    [InlineData($"actions/setup-dotnet@{CheckoutSha} # v5.2.0", "'actions/setup-dotnet' is not in the action pin manifest.")]
    [InlineData($"actions/checkout@{OtherSha} # v6.0.2", $"'actions/checkout' pins {OtherSha} but the manifest approves {CheckoutSha} (v6.0.2).")]
    [InlineData($"actions/checkout@{CheckoutSha}", "'actions/checkout' needs the trailing comment '# v6.0.2'.")]
    [InlineData($"actions/checkout@{CheckoutSha} # v6", "'actions/checkout' needs the trailing comment '# v6.0.2'.")]
    [InlineData("owner/first-party@v1", "'owner/first-party' must pin a full 40-character lowercase commit SHA, not 'v1'.")]
    public async Task Verify_action_pins_rejects_a_reference_that_the_manifest_does_not_approve(string reference, string expectedMessage)
    {
        // Arrange
        using var workspace = ActionPinTestWorkspace.Create(Manifest, $"jobs:\n  build:\n    steps:\n      - uses: {reference}\n", null);

        // Act
        var result = await LinkTestRepository.Run(workspace, ["verify-action-pins", "--root", workspace.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardOutput.ShouldBe(string.Empty);
        result.StandardError.ShouldBe(
            "Action pin verification failed:" + Environment.NewLine
            + $"- .github/workflows/ci.yml: Line 4: {expectedMessage}" + Environment.NewLine);
    }

    [Fact]
    public async Task Verify_action_pins_checks_composite_actions_and_names_their_path()
    {
        // Arrange
        using var workspace = ActionPinTestWorkspace.Create(
            Manifest,
            "jobs: {}\n",
            "runs:\n  steps:\n    - uses: actions/checkout@main\n");

        // Act
        var result = await LinkTestRepository.Run(workspace, ["verify-action-pins", "--root", workspace.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("- .github/actions/local/action.yml: Line 3:", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_action_pins_reads_a_manifest_from_another_location()
    {
        // Arrange
        using var workspace = ActionPinTestWorkspace.Create(Manifest, $"jobs:\n  build:\n    steps:\n      - uses: actions/checkout@{CheckoutSha} # v6.0.2\n", null);
        workspace.DeleteFile(".github/action-pins.json");
        workspace.WriteFile("shared/pins.json", Manifest);

        // Act
        var result = await LinkTestRepository.Run(
            workspace,
            ["verify-action-pins", "--manifest", "shared/pins.json", "--root", workspace.RootPath],
            TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
    }

    [Theory]
    [InlineData("{ \"version\": 2, \"firstParty\": [], \"actions\": [] }")]
    [InlineData("{ \"version\": 1, \"firstParty\": [], \"actions\": [{ \"repository\": \"actions/checkout\", \"sha\": \"v6\", \"version\": \"v6\" }] }")]
    [InlineData("{ \"version\": 1, \"firstParty\": [], \"actions\": [{ \"repository\": \"checkout\", \"sha\": \"de0fac2e4500dabe0009e67214ff5f5447ce83dd\", \"version\": \"v6\" }] }")]
    [InlineData("{ \"version\": 1, \"firstParty\": [], \"actions\": [{ \"repository\": \"actions/checkout\", \"sha\": \"de0fac2e4500dabe0009e67214ff5f5447ce83dd\", \"version\": \"\" }] }")]
    [InlineData("{ \"version\": 1, \"firstParty\": [], \"actions\": [{ \"repository\": \"actions/checkout\", \"sha\": \"de0fac2e4500dabe0009e67214ff5f5447ce83dd\", \"version\": \"v6\" }, { \"repository\": \"Actions/Checkout\", \"sha\": \"de0fac2e4500dabe0009e67214ff5f5447ce83dd\", \"version\": \"v6\" }] }")]
    [InlineData("{ \"version\": 1, \"firstParty\": [], \"actions\": [], \"extra\": true }")]
    [InlineData("not json")]
    public async Task Verify_action_pins_fails_for_an_invalid_manifest(string manifest)
    {
        // Arrange
        using var workspace = ActionPinTestWorkspace.Create(Manifest, "jobs: {}\n", null);
        workspace.WriteFile(".github/action-pins.json", manifest);

        // Act
        var result = await LinkTestRepository.Run(workspace, ["verify-action-pins", "--root", workspace.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldStartWith("sharedkernel-repo: ", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_action_pins_fails_when_the_manifest_is_missing()
    {
        // Arrange
        using var workspace = ActionPinTestWorkspace.Create(Manifest, "jobs: {}\n", null);
        workspace.DeleteFile(".github/action-pins.json");

        // Act
        var result = await LinkTestRepository.Run(workspace, ["verify-action-pins", "--root", workspace.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldBe("sharedkernel-repo: The action pin manifest does not exist." + Environment.NewLine);
    }

    [Fact]
    public async Task Verify_action_pins_rejects_a_tag_reference_with_trailing_white_space()
    {
        // Arrange
        using var workspace = ActionPinTestWorkspace.Create(Manifest, "jobs:\n  build:\n    steps:\n      - uses: actions/checkout@v6   \n", null);

        // Act
        var result = await LinkTestRepository.Run(workspace, ["verify-action-pins", "--root", workspace.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain("'actions/checkout' must pin a full 40-character lowercase commit SHA, not 'v6'.", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("      - uses:\n          actions/checkout@v6", "Line 4:")]
    [InlineData("      - { uses: actions/checkout@v6 }", "Line 4:")]
    [InlineData("      - name: x\n        with: { uses: actions/checkout@v6 }", "Line 5:")]
    public async Task Verify_action_pins_reports_a_uses_keyword_that_it_cannot_parse(string steps, string expectedLine)
    {
        // Arrange
        using var workspace = ActionPinTestWorkspace.Create(Manifest, $"jobs:\n  build:\n    steps:\n{steps}\n", null);

        // Act
        var result = await LinkTestRepository.Run(workspace, ["verify-action-pins", "--root", workspace.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldContain(expectedLine + " A uses: reference must be a block-style", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_action_pins_checks_yaml_extensions_mixed_case_repositories_and_reusable_workflow_paths()
    {
        // Arrange
        using var workspace = ActionPinTestWorkspace.Create(
            Manifest,
            $"jobs:\n  call:\n    uses: Actions/Checkout/.github/workflows/x.yml@{CheckoutSha} # v6.0.2\n",
            null);
        workspace.WriteFile(".github/workflows/other.yaml", "jobs:\n  a:\n    steps:\n      - uses: actions/checkout@main\n");

        // Act
        var result = await LinkTestRepository.Run(workspace, ["verify-action-pins", "--root", workspace.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldBe(
            "Action pin verification failed:" + Environment.NewLine
            + "- .github/workflows/other.yaml: Line 4: 'actions/checkout' must pin a full 40-character lowercase commit SHA, not 'main'." + Environment.NewLine,
            "The mixed-case reusable workflow reference matches the manifest and only the .yaml file fails.");
    }

    [Fact]
    public async Task Verify_action_pins_fails_when_the_root_has_no_workflows_directory()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile(".github/action-pins.json", Manifest);

        // Act
        var result = await LinkTestRepository.Run(workspace, ["verify-action-pins", "--root", workspace.RootPath], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.StandardError.ShouldBe("sharedkernel-repo: The root has no .github/workflows directory, so no action pin was checked." + Environment.NewLine);
    }
}
