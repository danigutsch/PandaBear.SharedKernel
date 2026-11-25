using static SharedKernel.RepoConfig.Tests.NewTestOperations;

namespace SharedKernel.RepoConfig.Tests;

/// <summary>Tests forge-specific repository forms and the opt-in CI wrapper.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
[Collection(RepoConfigToolEnvironmentGroup.Name)]
public sealed class NewForgeProfileTests
{
    private const string CheckSha = "043fb46d1a93c77aae656e7c1c64a875d1fc6a0a";
    private const string CheckoutSha = "de0fac2e4500dabe0009e67214ff5f5447ce83dd";
    private const string Image = "registry.example.test/ci/dotnet@sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string PolicyPath = "ci-policy.json";

    [Theory]
    [InlineData("github", ".github", ".forgejo", "description")]
    [InlineData("forgejo", ".forgejo", ".github", "about")]
    public async Task New_places_the_neutral_forms_in_the_selected_forge_directory(
        string forge,
        string directory,
        string otherDirectory,
        string descriptionKey)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();

        // Act
        var result = await Run(workspace, "--forge", forge, "--force");
        var verification = await BaselineVerify(workspace);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        verification.ExitCode.ShouldBe(0, verification.Error);
        workspace.ReadFile($"{directory}/pull_request_template.md").ShouldContain("## Summary", StringComparison.Ordinal);
        workspace.ReadFile($"{directory}/ISSUE_TEMPLATE/bug.yml").ShouldContain(
            $"\n{descriptionKey}: Report something that does not work as expected\n", StringComparison.Ordinal);
        workspace.ReadFile($"{directory}/ISSUE_TEMPLATE/feature.yml").ShouldContain(
            $"\n{descriptionKey}: Propose a change or addition\n", StringComparison.Ordinal);
        Directory.Exists(Path.Combine(workspace.RootPath, otherDirectory)).ShouldBeFalse();
        workspace.FileExists($"{directory}/workflows/ci.yml").ShouldBeFalse();
    }

    [Theory]
    [InlineData("github", ".github", "github.com", "")]
    [InlineData("github", ".github", "enterprise.example.test:8443", "")]
    [InlineData("forgejo", ".forgejo", "forge.example.test", "https://forge.example.test/")]
    public async Task New_renders_the_forge_token_boundary_and_caller_pins(
        string forge,
        string directory,
        string host,
        string actionPrefix)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var input = new TemporaryRepoConfigWorkspace();
        var origin = forge == "github" ? $""" "githubActionOrigin": "https://{host}", """ : string.Empty;
        input.WriteFile(PolicyPath, $$"""
            {
              "runner": "consumer-linux",
              "image": "{{Image}}",
              {{origin}}
              "checkoutAction": "https://{{host}}/tools/checkout@{{CheckoutSha}}",
              "checkAction": "https://{{host}}/consumer/ci/check@{{CheckSha}}",
              "timeoutMinutes": 17
            }
            """);

        // Act
        var result = await Run(workspace, "--forge", forge, "--ci-policy", Path.Combine(input.RootPath, PolicyPath), "--force");

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        var workflow = workspace.ReadFile($"{directory}/workflows/ci.yml");
        workflow.ShouldContain("  pull_request:", StringComparison.Ordinal);
        workflow.ShouldContain("  cancel-in-progress: true", StringComparison.Ordinal);
        workflow.ShouldContain("group: ${{ github.workflow }}-${{ github.event.pull_request.number }}", StringComparison.Ordinal);
        workflow.ShouldContain("timeout-minutes: 17", StringComparison.Ordinal);
        workflow.ShouldContain("consumer-linux", StringComparison.Ordinal);
        workflow.ShouldContain(Image, StringComparison.Ordinal);
        workflow.ShouldContain($"{actionPrefix}tools/checkout@{CheckoutSha}", StringComparison.Ordinal);
        workflow.ShouldContain($"{actionPrefix}consumer/ci/check@{CheckSha}", StringComparison.Ordinal);
        workflow.ShouldContain("persist-credentials: false", StringComparison.Ordinal);
        workflow.ShouldNotContain("pull_request_target", StringComparison.Ordinal);
        workflow.ShouldNotContain("run:", StringComparison.Ordinal);
        workspace.ReadFile(".config/sharedkernel-repo.json").ShouldNotContain("workflows/ci.yml", StringComparison.Ordinal);
        if (forge == "github")
        {
            workflow.ShouldContain("permissions:\n  contents: read\n", StringComparison.Ordinal);
            workflow.ShouldNotContain("https://", StringComparison.Ordinal);
        }
        else
        {
            workflow.ShouldContain("forgejo.event.pull_request.head.repo.id &&", StringComparison.Ordinal);
            workflow.ShouldContain("forgejo.event.pull_request.base.repo.id &&", StringComparison.Ordinal);
            workflow.ShouldContain("forgejo.event.pull_request.head.repo.id != forgejo.event.pull_request.base.repo.id", StringComparison.Ordinal);
            workflow.ShouldNotContain("permissions:", StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("github", "https://github.com/consumer/ci/check@main", Image, "17", "checkAction")]
    [InlineData("forgejo", "https://forge.example.test/consumer/ci/check@v1", Image, "17", "checkAction")]
    [InlineData("forgejo", "https://forge.example.test/consumer/ci/check@" + CheckSha + "\\n", Image, "17", "checkAction")]
    [InlineData("github", "https://github.com:8443/consumer/ci/check@" + CheckSha, Image, "17", "checkAction")]
    [InlineData("github", "https://github.com/invalid.owner/ci/check@" + CheckSha, Image, "17", "checkAction")]
    [InlineData("github", "https://forge.example.test/consumer/ci/check@" + CheckSha, Image, "17", "checkAction")]
    [InlineData("forgejo", "consumer/ci/check@" + CheckSha, Image, "17", "checkAction")]
    [InlineData("forgejo", "https://forge.example.test@" + CheckSha, Image, "17", "checkAction")]
    [InlineData("forgejo", "https://forge.example.test/check@" + CheckSha, Image, "17", "checkAction")]
    [InlineData("forgejo", "https://forge.example.test/consumer//check@" + CheckSha, Image, "17", "checkAction")]
    [InlineData("forgejo", "https://forge.example.test/consumer/ci/check?mode=unsafe@" + CheckSha, Image, "17", "checkAction")]
    [InlineData("forgejo", "https://forge.example.test/consumer/../ci/check@" + CheckSha, Image, "17", "checkAction")]
    [InlineData("forgejo", "https://forge.example.test/consumer/ci/check@" + CheckSha, "ci:latest", "17", "image")]
    [InlineData("github", "https://github.com/consumer/ci/check@" + CheckSha, Image, "0", "timeoutMinutes")]
    [InlineData("github", "https://github.com/consumer/ci/check@" + CheckSha, Image, "361", "timeoutMinutes")]
    [InlineData("github", "https://github.com/consumer/ci/check@" + CheckSha, Image, "null", "timeoutMinutes")]
    [InlineData("github", "https://github.com/consumer/ci/check@" + CheckSha, Image, "17.5", "timeoutMinutes")]
    public async Task New_rejects_a_ci_policy_that_cannot_be_safely_rendered(
        string forge,
        string check,
        string image,
        string timeout,
        string expectedField)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var input = new TemporaryRepoConfigWorkspace();
        const string checkout = "https://github.com/tools/checkout@" + CheckoutSha;
        const string githubOrigin = """ "githubActionOrigin": "https://github.com", """;
        var origin = forge == "github" ? githubOrigin : string.Empty;
        input.WriteFile(PolicyPath, $$"""
            {
              "runner": "consumer-linux",
              "image": "{{image}}",
              {{origin}}
              "checkoutAction": "{{checkout}}",
              "checkAction": "{{check}}",
              "timeoutMinutes": {{timeout}}
            }
            """);

        // Act
        var result = await Run(workspace, "--forge", forge, "--ci-policy", Path.Combine(input.RootPath, PolicyPath), "--force");

        // Assert
        result.ExitCode.ShouldBe(1, result.Output);
        result.Error.ShouldContain($"CI policy {expectedField}", StringComparison.Ordinal);
        Directory.EnumerateFileSystemEntries(workspace.RootPath).ShouldBeEmpty();
    }

    [Fact]
    public async Task New_requires_a_forge_to_render_a_ci_policy()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();

        // Act
        var result = await Run(workspace, "--ci-policy", PolicyPath, "--force");

        // Assert
        result.ExitCode.ShouldBe(1, result.Output);
        result.Error.ShouldContain("--ci-policy requires --forge", StringComparison.Ordinal);
        Directory.EnumerateFileSystemEntries(workspace.RootPath).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("not JSON", "CI policy is not valid JSON")]
    [InlineData("""{"runner":"one","runner":"two"}""", "CI policy is not valid JSON")]
    [InlineData("[]", "CI policy must be a JSON object")]
    [InlineData("""{"permissions":"write-all"}""", "CI policy must be a JSON object")]
    [InlineData("""{"runner":false}""", "CI policy runner")]
    [InlineData("""{"runner":"invalid label"}""", "CI policy runner")]
    public async Task New_rejects_unsupported_policy_properties_or_values(string policy, string diagnosis)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var input = new TemporaryRepoConfigWorkspace();
        input.WriteFile(PolicyPath, policy);

        // Act
        var result = await Run(workspace, "--forge", "github", "--ci-policy", Path.Combine(input.RootPath, PolicyPath), "--force");

        // Assert
        result.ExitCode.ShouldBe(1, result.Output);
        result.Error.ShouldContain(diagnosis, StringComparison.Ordinal);
        Directory.EnumerateFileSystemEntries(workspace.RootPath).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("github", "")]
    [InlineData("github", """ "githubActionOrigin": "http://enterprise.example.test", """)]
    [InlineData("github", """ "githubActionOrigin": "https://enterprise.example.test/path", """)]
    [InlineData("forgejo", """ "githubActionOrigin": "https://enterprise.example.test", """)]
    public async Task New_requires_an_explicit_https_authority_for_the_native_github_resolver(string forge, string origin)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var input = new TemporaryRepoConfigWorkspace();
        input.WriteFile(PolicyPath, $$"""
            {
              "runner": "ubuntu-latest",
              "image": "{{Image}}",
              {{origin}}
              "checkoutAction": "https://enterprise.example.test/tools/checkout@{{CheckoutSha}}",
              "checkAction": "https://enterprise.example.test/consumer/ci/check@{{CheckSha}}",
              "timeoutMinutes": 17
            }
            """);

        // Act
        var result = await Run(workspace, "--forge", forge, "--ci-policy", Path.Combine(input.RootPath, PolicyPath), "--force");

        // Assert
        result.ExitCode.ShouldBe(1, result.Output);
        result.Error.ShouldContain("CI policy githubActionOrigin", StringComparison.Ordinal);
        Directory.EnumerateFileSystemEntries(workspace.RootPath).ShouldBeEmpty();
    }

    [Fact]
    public async Task New_previews_the_workflow_and_baseline_apply_preserves_consumer_edits()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var input = new TemporaryRepoConfigWorkspace();
        const string workflowPath = ".github/workflows/ci.yml";
        const string edited = "name: Owner's workflow\n";
        input.WriteFile(PolicyPath, $$"""
            {
              "runner": "ubuntu-latest",
              "image": "{{Image}}",
              "githubActionOrigin": "https://github.com",
              "checkoutAction": "https://github.com/tools/checkout@{{CheckoutSha}}",
              "checkAction": "https://github.com/consumer/ci/check@{{CheckSha}}",
              "timeoutMinutes": 17
            }
            """);
        string[] options = ["--forge", "github", "--ci-policy", Path.Combine(input.RootPath, PolicyPath)];
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();

        // Act
        var preview = await Run(workspace, options);
        var previewWroteWorkflow = workspace.FileExists(workflowPath);
        var creation = await Run(workspace, [.. options, "--force"]);
        workspace.WriteFile(workflowPath, edited);
        var apply = await RepoConfigToolRunner.Run(["baseline", "apply", "--root", workspace.RootPath, "--force"],
            output, error, Path.GetTempPath(), TestContext.Current.CancellationToken);

        // Assert
        preview.ExitCode.ShouldBe(0, preview.Error);
        preview.Output.ShouldContain($"preview: would create {workflowPath}.", StringComparison.Ordinal);
        previewWroteWorkflow.ShouldBeFalse();
        creation.ExitCode.ShouldBe(0, creation.Error);
        apply.ShouldBe(0, error.ToString());
        workspace.ReadFile(workflowPath).ShouldBe(edited);
    }
}
