using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;

namespace PandaBear.RepoConfig.Tests;

[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
[Collection("Repo config tool environment")]
public sealed class RepoConfigToolApplicationTests
{
    [Fact]
    public async Task Verify_reports_missing_roadmap_structure()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = error.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("Repository config verification failed:", StringComparison.Ordinal);
        errorText.ShouldContain("roadmap: Missing required directory.", StringComparison.Ordinal);
        errorText.ShouldContain("roadmap/config.json: Missing required file.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Init_creates_structure_that_verify_accepts()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var initExitCode = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        var verifyExitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var verifyOutputText = verifyOutput.ToString();

        // Assert
        initExitCode.ShouldBe(0);
        verifyExitCode.ShouldBe(0);
        verifyOutputText.ShouldContain("Repository config is valid.", StringComparison.Ordinal);
        workspace.FileExists("roadmap/config.json").ShouldBeTrue();
        workspace.FileExists("roadmap/items/RM-001-roadmap-gitops.json").ShouldBeTrue();
    }

    [Fact]
    public async Task Init_writes_full_schema_templates()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        var configSchemaText = workspace.ReadFile("roadmap/schema/roadmap-config.schema.json");
        var itemSchemaText = workspace.ReadFile("roadmap/schema/roadmap-item.schema.json");

        // Assert
        exitCode.ShouldBe(0);
        configSchemaText.ShouldContain("\"required\": [", StringComparison.Ordinal);
        configSchemaText.ShouldContain("\"$schema\": \"https://json-schema.org/draft/2020-12/schema\"", StringComparison.Ordinal);
        configSchemaText.ShouldNotContain("github.com/", StringComparison.Ordinal);
        configSchemaText.ShouldContain("\"integrations\"", StringComparison.Ordinal);
        itemSchemaText.ShouldContain("\"$schema\": \"https://json-schema.org/draft/2020-12/schema\"", StringComparison.Ordinal);
        itemSchemaText.ShouldNotContain("github.com/", StringComparison.Ordinal);
        itemSchemaText.ShouldContain("\"size\"", StringComparison.Ordinal);
        itemSchemaText.ShouldContain("\"uniqueItems\": true", StringComparison.Ordinal);
        itemSchemaText.ShouldContain("\"minimum\": 0.1", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Init_writes_portable_roadmap_readme_command()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        var readmeText = workspace.ReadFile("roadmap/README.md");

        // Assert
        exitCode.ShouldBe(0);
        readmeText.ShouldContain("sharedkernel-repo verify", StringComparison.Ordinal);
        readmeText.ShouldNotContain("dotnet run --project", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_excludes_untriaged_items_from_executable_priority_queries()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        using var getOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var getError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        workspace.WriteFile("roadmap/items/RM-002-untriaged.json", RoadmapTestContent.UntriagedIssueJson);

        // Act
        var verifyExitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var getExitCode = await RepoConfigToolRunner.Run(["get", "next-priority", "--root", workspace.RootPath], getOutput, getError, workspace.RootPath, TestContext.Current.CancellationToken);
        var outputText = getOutput.ToString();

        // Assert
        verifyExitCode.ShouldBe(0);
        getExitCode.ShouldBe(0);
        outputText.ShouldContain("RM-001", StringComparison.Ordinal);
        outputText.ShouldNotContain("RM-002", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_escapes_control_characters_in_item_titles()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var getOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var getError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var item = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json")
            .Replace(
                "Establish GitOps roadmap and repo configuration tooling",
                "First line\\n\\u001b[31mSecond line",
                StringComparison.Ordinal);
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", item);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["get", "next-priority", "--root", workspace.RootPath], getOutput, getError, workspace.RootPath, TestContext.Current.CancellationToken);
        var outputText = getOutput.ToString();
        var outputLines = outputText.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        // Assert
        exitCode.ShouldBe(0);
        getError.ToString().ShouldBe(string.Empty);
        outputLines.Length.ShouldBe(1);
        outputText.ShouldNotContain("\u001b", StringComparison.Ordinal);
        outputText.ShouldContain("\\u000A", StringComparison.Ordinal);
        outputText.ShouldContain("\\u001B", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_does_not_emit_control_characters_from_an_invalid_item_id_prefix()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var config = workspace.ReadFile("roadmap/config.json")
            .Replace("\"itemIdPrefix\": \"RM\"", "\"itemIdPrefix\": \"RM\\n\\u001b\"", StringComparison.Ordinal);
        workspace.WriteFile("roadmap/config.json", config);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = verifyError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        verifyOutput.ToString().ShouldBe(string.Empty);
        errorText.ShouldContain("itemIdPrefix may contain only ASCII letters, digits, underscores, and hyphens.", StringComparison.Ordinal);
        errorText.ShouldNotContain("\u001b", StringComparison.Ordinal);
        errorText.ShouldNotContain("RM\n", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_blocking_overview_escapes_control_characters_in_item_ids()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var getOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var getError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        const string itemId = "RM-002\n\u001b";
        var serializedItemId = JsonSerializer.Serialize(itemId);
        var blockedItem = RoadmapTestContent.BlockedIssueJson.Replace("\"RM-002\"", serializedItemId, StringComparison.Ordinal);
        var blockerItem = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json")
            .Replace("\"blocks\": []", $"\"blocks\": [{serializedItemId}]", StringComparison.Ordinal);
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", blockerItem);
        workspace.WriteFile("roadmap/items/RM-002-blocked.json", blockedItem);
        workspace.WriteFile(
            "roadmap/order.json",
            $$"""
            {
              "ordering": "lower order values are higher priority",
              "items": ["RM-001", {{serializedItemId}}]
            }
            """);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["get", "blocking-overview", "--root", workspace.RootPath], getOutput, getError, workspace.RootPath, TestContext.Current.CancellationToken);
        var outputText = getOutput.ToString();
        var outputLines = outputText.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        // Assert
        exitCode.ShouldBe(0);
        getError.ToString().ShouldBe(string.Empty);
        outputLines.Length.ShouldBe(1);
        outputText.ShouldNotContain("\u001b", StringComparison.Ordinal);
        outputText.ShouldContain("RM-002\\u000A\\u001B blocked by RM-001", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("\"order\": 20,")]
    [InlineData("\"scoring\": { \"reach\": 10, \"impact\": 3, \"confidence\": 0.8, \"effort\": 2 },")]
    public async Task Verify_rejects_priority_inputs_on_untriaged_items(string priorityInput)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var itemText = RoadmapTestContent.UntriagedIssueJson.Replace("\"theme\":", $"{priorityInput}\n  \"theme\":", StringComparison.Ordinal);
        workspace.WriteFile("roadmap/items/RM-002-untriaged.json", itemText);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = verifyError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("Untriaged roadmap items must not define priority inputs.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_rejects_untriaged_items_in_order_json()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        workspace.WriteFile("roadmap/items/RM-002-untriaged.json", RoadmapTestContent.UntriagedIssueJson);
        workspace.WriteFile("roadmap/order.json", "{ \"items\": [\"RM-001\", \"RM-002\"] }");

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = verifyError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("order.json must not contain untriaged item: RM-002.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_blockers_of_renders_untriaged_items_without_priority_values()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var getOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var getError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var rootItem = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", rootItem.Replace("\"blockedBy\": []", "\"blockedBy\": [\"RM-002\"]", StringComparison.Ordinal));
        var untriagedItem = RoadmapTestContent.UntriagedIssueJson.Replace("\"blocks\": []", "\"blocks\": [\"RM-001\"]", StringComparison.Ordinal);
        workspace.WriteFile("roadmap/items/RM-002-untriaged.json", untriagedItem);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["get", "blockers-of", "RM-001", "--root", workspace.RootPath], getOutput, getError, workspace.RootPath, TestContext.Current.CancellationToken);
        var outputText = getOutput.ToString();

        // Assert
        exitCode.ShouldBe(0);
        outputText.ShouldContain("RM-002 | issue | ready | untriaged", StringComparison.Ordinal);
        outputText.ShouldNotContain("order", StringComparison.Ordinal);
        outputText.ShouldNotContain("score", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Init_writes_templates_with_resolvable_local_schema_references()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        using var configDocument = JsonDocument.Parse(workspace.ReadFile("roadmap/config.json"));
        using var itemDocument = JsonDocument.Parse(workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json"));
        var configSchema = configDocument.RootElement.GetProperty("$schema").GetString();
        var itemSchema = itemDocument.RootElement.GetProperty("$schema").GetString();
        var configSchemaPath = Path.GetFullPath(Path.Combine(workspace.RootPath, "roadmap", configSchema ?? string.Empty));
        var itemSchemaPath = Path.GetFullPath(Path.Combine(workspace.RootPath, "roadmap/items", itemSchema ?? string.Empty));

        // Assert
        exitCode.ShouldBe(0);
        File.Exists(configSchemaPath).ShouldBeTrue();
        File.Exists(itemSchemaPath).ShouldBeTrue();
    }

    [Fact]
    public async Task Set_updates_github_repository_config()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var setOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var setError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["set", "github.repository", "example/repository", "--root", workspace.RootPath], setOutput, setError, workspace.RootPath, TestContext.Current.CancellationToken);
        var configText = workspace.ReadFile("roadmap/config.json");

        // Assert
        exitCode.ShouldBe(0);
        configText.ShouldContain("\"repository\": \"example/repository\"", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Set_preserves_existing_github_projection_settings_and_valid_config()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var setOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var setError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);

        // Act
        var setExitCode = await RepoConfigToolRunner.Run(["set", "github.repository", "example/repository", "--root", workspace.RootPath], setOutput, setError, workspace.RootPath, TestContext.Current.CancellationToken);
        var verifyExitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(workspace.ReadFile("roadmap/config.json"));
        var github = document.RootElement.GetProperty("integrations").GetProperty("github");

        // Assert
        setExitCode.ShouldBe(0);
        verifyExitCode.ShouldBe(0);
        github.GetProperty("enabled").GetBoolean().ShouldBeFalse();
        github.GetProperty("sourceOfTruth").GetString().ShouldBe("projection");
        github.GetProperty("repository").GetString().ShouldBe("example/repository");
    }

    [Fact]
    public async Task Verify_rejects_obsolete_github_project_field_mapping()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var configText = workspace.ReadFile("roadmap/config.json");
        workspace.WriteFile("roadmap/config.json", configText.Replace("\"sourceOfTruth\": \"projection\"", "\"sourceOfTruth\": \"projection\",\n      \"projectFieldMapping\": {}", StringComparison.Ordinal));

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = verifyError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("integrations.github.projectFieldMapping is not supported.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_rejects_invalid_github_projection_source_of_truth()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var configText = workspace.ReadFile("roadmap/config.json");
        workspace.WriteFile("roadmap/config.json", configText.Replace("\"sourceOfTruth\": \"projection\"", "\"sourceOfTruth\": \"manual\"", StringComparison.Ordinal));

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = verifyError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("integrations.github.sourceOfTruth must be projection.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_allows_missing_github_projection_source_of_truth()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var configText = workspace.ReadFile("roadmap/config.json");
        workspace.WriteFile("roadmap/config.json", configText.Replace(",\n      \"sourceOfTruth\": \"projection\"", string.Empty, StringComparison.Ordinal));

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = verifyError.ToString();

        // Assert
        exitCode.ShouldBe(0);
        errorText.ShouldBe(string.Empty);
    }

    [Theory]
    [InlineData("owner only")]
    [InlineData("owner/repo?state=closed")]
    [InlineData("owner/repo#fragment")]
    [InlineData("owner/repo\\path")]
    [InlineData("owner--name/repository")]
    public async Task Set_rejects_invalid_github_repository_config(string repository)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var setOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var setError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["set", "github.repository", repository, "--root", workspace.RootPath], setOutput, setError, workspace.RootPath, TestContext.Current.CancellationToken);
        var configText = workspace.ReadFile("roadmap/config.json");
        var errorText = setError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("github.repository must be shaped as owner/repository.", StringComparison.Ordinal);
        configText.ShouldNotContain(JsonSerializer.Serialize(repository), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_reports_unknown_dependencies()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace("\"dependencies\": []", "\"dependencies\": [\"RM-404\"]", StringComparison.Ordinal));

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = verifyError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("Unknown dependency: RM-404.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_reports_duplicate_roadmap_ids_without_aborting_diagnostics()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        workspace.WriteFile("roadmap/items/RM-001-copy.json", workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json"));

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = verifyError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("Duplicate roadmap item id: RM-001.", StringComparison.Ordinal);
        errorText.ShouldNotContain("sharedkernel-repo:", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_reports_invalid_parent_type()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace("\"theme\":", "\"parent\": 7,\n  \"theme\":", StringComparison.Ordinal));

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = verifyError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("parent must be a string when present.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_rejects_blank_parent()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace("\"theme\":", "\"parent\": \"   \",\n  \"theme\":", StringComparison.Ordinal));

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = verifyError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("parent must not be blank when present.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_rejects_item_as_its_own_parent()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace("\"theme\":", "\"parent\": \"RM-001\",\n  \"theme\":", StringComparison.Ordinal));

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = verifyError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("Roadmap item cannot reference itself as parent.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_reports_parent_cycles()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var rootItemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", rootItemText.Replace("\"theme\":", "\"parent\": \"RM-002\",\n  \"theme\":", StringComparison.Ordinal));
        workspace.WriteFile("roadmap/items/RM-002-follow-up.json", RoadmapTestContent.HigherPriorityIssueJson);
        workspace.WriteFile("roadmap/order.json", "{ \"items\": [\"RM-002\", \"RM-001\"] }");

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = verifyError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("parent cycle includes RM-001.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_rejects_github_subissues_in_favor_of_parent_links()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var itemText = RoadmapTestContent.IssueWithGitHubMappingJson
            .Replace("\"issue\": 997", "\"issue\": 998,\n      \"subIssues\": [1000]", StringComparison.Ordinal);
        workspace.WriteFile("roadmap/items/RM-002-follow-up.json", itemText);
        workspace.WriteFile("roadmap/order.json", "{ \"items\": [\"RM-001\", \"RM-002\"] }");

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = verifyError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("integrations.github.subIssues is not supported; use parent links.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_reports_confidence_below_documented_range()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace("\"confidence\": 0.8", "\"confidence\": 0.09", StringComparison.Ordinal));

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = verifyError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("confidence must be between 0.1 and 1.0.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_reports_missing_item_id_prefix()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var configText = workspace.ReadFile("roadmap/config.json");
        workspace.WriteFile("roadmap/config.json", configText.Replace("  \"itemIdPrefix\": \"RM\",\n", string.Empty, StringComparison.Ordinal));

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = verifyError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("Missing required string property: itemIdPrefix.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_reports_missing_integrations_object()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var configText = workspace.ReadFile("roadmap/config.json");
        var integrationsStart = configText.IndexOf("  \"integrations\": {", StringComparison.Ordinal);
        workspace.WriteFile("roadmap/config.json", configText[..integrationsStart].TrimEnd().TrimEnd(',') + Environment.NewLine + "}" + Environment.NewLine);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = verifyError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("Missing required object property: integrations.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_reports_duplicate_config_array_values()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var configText = workspace.ReadFile("roadmap/config.json");
        workspace.WriteFile("roadmap/config.json", configText.Replace("\"done\",\n      \"dropped\"", "\"done\",\n      \"done\",\n      \"dropped\"", StringComparison.Ordinal));

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = verifyError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("project.closedStatuses contains a duplicate value: done.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_reports_invalid_config_array_values()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var configText = workspace.ReadFile("roadmap/config.json");
        workspace.WriteFile("roadmap/config.json", configText.Replace("\"done\",\n      \"dropped\"", "\"done\",\n      7", StringComparison.Ordinal));

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = verifyError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("project.closedStatuses must contain only non-empty strings.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_rejects_config_array_values_with_leading_or_trailing_whitespace()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var configText = workspace.ReadFile("roadmap/config.json");
        workspace.WriteFile("roadmap/config.json", configText.Replace("\"done\",\n      \"dropped\"", "\"done \",\n      \"dropped\"", StringComparison.Ordinal));

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = verifyError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("project.closedStatuses must not contain leading or trailing whitespace.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_rejects_obsolete_project_tag_fields()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var configText = workspace.ReadFile("roadmap/config.json");
        workspace.WriteFile("roadmap/config.json", configText.Replace("\"project\": {", "\"project\": {\n    \"tagFields\": [],", StringComparison.Ordinal));

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = verifyError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("project.tagFields is not supported.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_rejects_item_array_values_with_leading_or_trailing_whitespace()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace("\"area: tooling\"", "\"area: tooling \"", StringComparison.Ordinal));

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = verifyError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("labels must not contain leading or trailing whitespace.", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("[]", "integrations must be a JSON object when present.")]
    [InlineData("{ \"github\": [] }", "integrations.github must be a JSON object when present.")]
    public async Task Verify_reports_malformed_item_integrations(string integrations, string expectedMessage)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace("\"labels\": [", $"\"integrations\": {integrations},\n  \"labels\": [", StringComparison.Ordinal));

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = verifyError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain(expectedMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_reports_malformed_item_integrations_when_the_item_id_is_invalid()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        var malformedItem = itemText
            .Replace("\"id\": \"RM-001\"", "\"id\": \"\"", StringComparison.Ordinal)
            .Replace("\"labels\": [", "\"integrations\": [],\n  \"labels\": [", StringComparison.Ordinal);
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", malformedItem);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = verifyError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("Missing required string property: id.", StringComparison.Ordinal);
        errorText.ShouldContain("integrations must be a JSON object when present.", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("owner only")]
    [InlineData("owner/repo?state=closed")]
    [InlineData("owner/repo#fragment")]
    [InlineData("owner/repo\\path")]
    [InlineData("owner--name/repository")]
    public async Task Verify_reports_invalid_enabled_github_repository(string repository)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var configText = workspace.ReadFile("roadmap/config.json");
        workspace.WriteFile("roadmap/config.json", configText.Replace("\"enabled\": false", $"\"enabled\": true,\n      \"repository\": {JsonSerializer.Serialize(repository)}", StringComparison.Ordinal));

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = verifyError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("integrations.github.repository must be shaped as owner/repository when GitHub sync is enabled.", StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("[]", "integrations.github.projectV2 must be a JSON object.")]
    [InlineData("{}", "integrations.github.projectV2.id must be a non-empty string.")]
    [InlineData("{\"id\":\"\",\"owner\":\"owner\",\"number\":1}", "integrations.github.projectV2.id must be a non-empty string.")]
    [InlineData("{\"id\":1,\"owner\":\"owner\",\"number\":1}", "integrations.github.projectV2.id must be a non-empty string.")]
    [InlineData("{\"id\":\"project\",\"owner\":\"\",\"number\":1}", "integrations.github.projectV2.owner must be a valid GitHub owner.")]
    [InlineData("{\"id\":\"project\",\"owner\":\"invalid owner\",\"number\":1}", "integrations.github.projectV2.owner must be a valid GitHub owner.")]
    [InlineData("{\"id\":\"project\",\"owner\":1,\"number\":1}", "integrations.github.projectV2.owner must be a valid GitHub owner.")]
    [InlineData("{\"id\":\"project\",\"owner\":\"owner\",\"number\":0}", "integrations.github.projectV2.number must be a positive integer.")]
    [InlineData("{\"id\":\"project\",\"owner\":\"owner\",\"number\":-1}", "integrations.github.projectV2.number must be a positive integer.")]
    [InlineData("{\"id\":\"project\",\"owner\":\"owner\",\"number\":\"1\"}", "integrations.github.projectV2.number must be a positive integer.")]
    public async Task Verify_reports_invalid_github_project_target_configuration(string projectV2, string expectedError)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var configText = workspace.ReadFile("roadmap/config.json");
        workspace.WriteFile("roadmap/config.json", configText.Replace("\"enabled\": false", $"\"enabled\": true,\n      \"repository\": \"owner/repository\",\n      \"projectV2\": {projectV2}", StringComparison.Ordinal));

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        verifyError.ToString().ShouldContain(expectedError, StringComparison.Ordinal);
    }

    [Fact]
    public void GitHub_repository_name_accepts_single_hyphenated_owner()
    {
        // Act
        var isValid = GitHubRepositoryName.IsValid("owner-name/repository");

        // Assert
        isValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Verify_reports_duplicate_string_array_values_without_cascading_reference_errors()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace("\"dependencies\": []", "\"dependencies\": [\"RM-404\", \"RM-404\"]", StringComparison.Ordinal));

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = verifyError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("dependencies contains a duplicate value: RM-404.", StringComparison.Ordinal);
        var unknownDependencyReports = errorText.Split("Unknown dependency: RM-404.", StringSplitOptions.None).Length - 1;
        unknownDependencyReports.ShouldBe(1);
    }

    [Fact]
    public async Task Verify_reports_non_object_theme_file()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        workspace.WriteFile("roadmap/themes/repo-operations.json", "[]");

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = verifyError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("roadmap/themes/repo-operations.json: Theme root must be a JSON object.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_reports_items_when_theme_catalog_is_empty()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        workspace.DeleteFile("roadmap/themes/repo-operations.json");

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = verifyError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("Unknown theme: repo-operations.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_next_unblocked_skips_open_blockers()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var getOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var getError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var epicText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", epicText.Replace("\"blocks\": []", "\"blocks\": [\"RM-002\"]", StringComparison.Ordinal));
        workspace.WriteFile("roadmap/items/RM-002-follow-up.json", RoadmapTestContent.BlockedIssueJson);
        workspace.WriteFile("roadmap/order.json", "{ \"items\": [\"RM-001\", \"RM-002\"] }");

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["get", "next-unblocked", "--root", workspace.RootPath], getOutput, getError, workspace.RootPath, TestContext.Current.CancellationToken);
        var outputText = getOutput.ToString();

        // Assert
        exitCode.ShouldBe(0);
        outputText.ShouldContain("RM-001 | epic", StringComparison.Ordinal);
        outputText.ShouldNotContain("RM-002 | issue", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_next_work_prioritizes_unblocked_items_that_close_open_blockers()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var getOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var getError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var epicText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        var standardWork = RoadmapTestContent.UnblockedEnablerJson.Replace("\"order\": 30", "\"order\": 5", StringComparison.Ordinal);
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", epicText.Replace("\"blocks\": []", "\"blocks\": [\"RM-002\"]", StringComparison.Ordinal));
        workspace.WriteFile("roadmap/items/RM-002-follow-up.json", RoadmapTestContent.BlockedIssueJson);
        workspace.WriteFile("roadmap/items/RM-003-standard-work.json", standardWork);
        workspace.WriteFile("roadmap/order.json", "{ \"items\": [\"RM-003\", \"RM-001\", \"RM-002\"] }");

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["get", "next-work", "--root", workspace.RootPath], getOutput, getError, workspace.RootPath, TestContext.Current.CancellationToken);
        var outputText = getOutput.ToString();
        var blockerIndex = outputText.IndexOf("RM-001 | epic", StringComparison.Ordinal);
        var standardWorkIndex = outputText.IndexOf("RM-003 | enabler", StringComparison.Ordinal);

        // Assert
        exitCode.ShouldBe(0);
        blockerIndex.ShouldBeGreaterThan(-1);
        standardWorkIndex.ShouldBeGreaterThan(-1);
        blockerIndex.ShouldBeLessThan(standardWorkIndex);
        outputText.ShouldNotContain("RM-002 | issue", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_next_work_does_not_elevate_items_that_only_unblock_closed_work()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var getOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var getError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var epicText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        var closedDependent = RoadmapTestContent.BlockedIssueJson.Replace("\"status\": \"ready\"", "\"status\": \"done\"", StringComparison.Ordinal);
        var standardWork = RoadmapTestContent.UnblockedEnablerJson.Replace("\"order\": 30", "\"order\": 5", StringComparison.Ordinal);
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", epicText.Replace("\"blocks\": []", "\"blocks\": [\"RM-002\"]", StringComparison.Ordinal));
        workspace.WriteFile("roadmap/items/RM-002-follow-up.json", closedDependent);
        workspace.WriteFile("roadmap/items/RM-003-standard-work.json", standardWork);
        workspace.WriteFile("roadmap/order.json", "{ \"items\": [\"RM-003\", \"RM-001\", \"RM-002\"] }");

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["get", "next-work", "--root", workspace.RootPath], getOutput, getError, workspace.RootPath, TestContext.Current.CancellationToken);
        var outputText = getOutput.ToString();
        var blockerIndex = outputText.IndexOf("RM-001 | epic", StringComparison.Ordinal);
        var standardWorkIndex = outputText.IndexOf("RM-003 | enabler", StringComparison.Ordinal);

        // Assert
        exitCode.ShouldBe(0);
        blockerIndex.ShouldBeGreaterThan(-1);
        standardWorkIndex.ShouldBeGreaterThan(-1);
        standardWorkIndex.ShouldBeLessThan(blockerIndex);
        outputText.ShouldNotContain("RM-002 | issue", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_next_work_uses_canonical_priority_to_break_blocker_ties()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var getOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var getError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var epicText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        var higherPriorityBlocker = RoadmapTestContent.UnblockedEnablerJson
            .Replace("\"blocks\": []", "\"blocks\": [\"RM-005\"]", StringComparison.Ordinal)
            .Replace("\"order\": 30", "\"order\": 5", StringComparison.Ordinal);
        var secondDependent = RoadmapTestContent.BlockedIssueJson.Replace("\"id\": \"RM-002\"", "\"id\": \"RM-004\"", StringComparison.Ordinal);
        var thirdDependent = RoadmapTestContent.BlockedIssueJson
            .Replace("\"id\": \"RM-002\"", "\"id\": \"RM-005\"", StringComparison.Ordinal)
            .Replace("\"blockedBy\": [\n    \"RM-001\"\n  ]", "\"blockedBy\": [\n    \"RM-003\"\n  ]", StringComparison.Ordinal);
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", epicText.Replace("\"blocks\": []", "\"blocks\": [\"RM-002\", \"RM-004\"]", StringComparison.Ordinal));
        workspace.WriteFile("roadmap/items/RM-002-follow-up.json", RoadmapTestContent.BlockedIssueJson);
        workspace.WriteFile("roadmap/items/RM-003-priority-blocker.json", higherPriorityBlocker);
        workspace.WriteFile("roadmap/items/RM-004-follow-up.json", secondDependent);
        workspace.WriteFile("roadmap/items/RM-005-follow-up.json", thirdDependent);
        workspace.WriteFile("roadmap/order.json", "{ \"items\": [\"RM-003\", \"RM-001\", \"RM-002\", \"RM-004\", \"RM-005\"] }");

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["get", "next-work", "--root", workspace.RootPath], getOutput, getError, workspace.RootPath, TestContext.Current.CancellationToken);
        var outputText = getOutput.ToString();
        var lowerPriorityBlockerIndex = outputText.IndexOf("RM-001 | epic", StringComparison.Ordinal);
        var higherPriorityBlockerIndex = outputText.IndexOf("RM-003 | enabler", StringComparison.Ordinal);

        // Assert
        exitCode.ShouldBe(0);
        lowerPriorityBlockerIndex.ShouldBeGreaterThan(-1);
        higherPriorityBlockerIndex.ShouldBeGreaterThan(-1);
        higherPriorityBlockerIndex.ShouldBeLessThan(lowerPriorityBlockerIndex);
    }

    [Fact]
    public async Task Get_blockers_of_lists_direct_blockers()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var getOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var getError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var epicText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", epicText.Replace("\"blocks\": []", "\"blocks\": [\"RM-002\"]", StringComparison.Ordinal));
        workspace.WriteFile("roadmap/items/RM-002-follow-up.json", RoadmapTestContent.BlockedIssueJson);
        workspace.WriteFile("roadmap/order.json", "{ \"items\": [\"RM-001\", \"RM-002\"] }");

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["get", "blockers-of", "RM-002", "--root", workspace.RootPath], getOutput, getError, workspace.RootPath, TestContext.Current.CancellationToken);
        var outputText = getOutput.ToString();

        // Assert
        exitCode.ShouldBe(0);
        outputText.ShouldContain("RM-001 | epic", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_blockers_of_prefixes_unknown_item_error()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var getOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var getError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["get", "blockers-of", "RM-999", "--root", workspace.RootPath], getOutput, getError, workspace.RootPath, TestContext.Current.CancellationToken);
        var outputText = getOutput.ToString();
        var errorText = getError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        outputText.ShouldBe(string.Empty);
        errorText.ShouldBe("sharedkernel-repo: Unknown roadmap item id: RM-999" + Environment.NewLine);
    }

    [Fact]
    public async Task GitHub_sync_returns_clean_error_for_noncaller_cancellation()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var output = new CancellationThrowingTextWriter();
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace("\"labels\": [", "\"integrations\": { \"github\": { \"issue\": 997 } },\n  \"labels\": [", StringComparison.Ordinal));

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["github", "sync", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = error.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("sharedkernel-repo: The operation was canceled.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task GitHub_sync_reports_no_diagnostic_for_caller_cancellation()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var output = new CancellationThrowingTextWriter();
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        using var cancellation = new CancellationTokenSource();
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace("\"labels\": [", "\"integrations\": { \"github\": { \"issue\": 997 } },\n  \"labels\": [", StringComparison.Ordinal));
        await cancellation.CancelAsync();

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["github", "sync", "--root", workspace.RootPath], output, error, workspace.RootPath, cancellation.Token);

        // Assert
        exitCode.ShouldBe(1);
        error.ToString().ShouldBe(string.Empty);
    }

    [Fact]
    public async Task GitHub_sync_preview_reports_no_diagnostic_for_caller_cancellation_during_project_preflight()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var syncOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var syncError = new StringWriter(CultureInfo.InvariantCulture);
        using var cancellation = new CancellationTokenSource();
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        GitHubProjectSyncTestOperations.EnableProjectTarget(workspace);
        using var handler = new BeforeResponseHttpMessageHandler(
            () => new HttpResponseMessage(HttpStatusCode.OK),
            cancellation.Cancel);
        using var httpClient = new HttpClient(handler);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["github", "sync", "--root", workspace.RootPath], syncOutput, syncError, workspace.RootPath, httpClient, cancellation.Token);

        // Assert
        exitCode.ShouldBe(1);
        cancellation.IsCancellationRequested.ShouldBeTrue();
        syncOutput.ToString().ShouldBe(string.Empty);
        syncError.ToString().ShouldBe(string.Empty);
    }

    [Fact]
    public async Task GitHub_sync_preview_reports_no_diagnostic_for_caller_cancellation_after_project_fields_are_read()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var syncOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var syncError = new StringWriter(CultureInfo.InvariantCulture);
        using var cancellation = new CancellationTokenSource();
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        GitHubProjectSyncTestOperations.EnableProjectTarget(workspace);
        GitHubProjectSyncTestOperations.MapDefaultItem(workspace);
        var statusOptions = """
            [
              { "id": "proposed", "name": "proposed" },
              { "id": "ready", "name": "ready" },
              { "id": "in-progress", "name": "in_progress" },
              { "id": "done", "name": "done" },
              { "id": "done", "name": "legacy" },
              { "id": "dropped", "name": "dropped" }
            ]
            """;
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"id\": \"project-id\", \"number\": 1, \"owner\": { \"login\": \"owner\" } } } }");
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new CancellationAfterWriteHttpContent(
                GitHubProjectSyncTestOperations.CompleteProjectFields(statusOptions),
                cancellation.Cancel)
        });
        using var httpClient = new HttpClient(handler);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["github", "sync", "--root", workspace.RootPath], syncOutput, syncError, workspace.RootPath, httpClient, cancellation.Token);

        // Assert
        exitCode.ShouldBe(1);
        cancellation.IsCancellationRequested.ShouldBeTrue();
        syncOutput.ToString().ShouldBe(string.Empty);
        syncError.ToString().ShouldBe(string.Empty);
    }

    [Fact]
    public async Task GitHub_roadmap_syncer_apply_propagates_caller_cancellation_after_project_fields_are_read()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var cancellation = new CancellationTokenSource();
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        GitHubProjectSyncTestOperations.EnableProjectTarget(workspace);
        GitHubProjectSyncTestOperations.MapDefaultItem(workspace);
        var statusOptions = """
            [
              { "id": "proposed", "name": "proposed" },
              { "id": "ready", "name": "ready" },
              { "id": "in-progress", "name": "in_progress" },
              { "id": "done", "name": "done" },
              { "id": "done", "name": "legacy" },
              { "id": "dropped", "name": "dropped" }
            ]
            """;
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"id\": \"project-id\", \"number\": 1, \"owner\": { \"login\": \"owner\" } } } }");
        handler.EnqueueJson(HttpStatusCode.NotFound, "{}");
        handler.EnqueueJson(HttpStatusCode.OK, GitHubSyncTestResponseFactory.IssueMetadata(997));
        handler.EnqueueJson(HttpStatusCode.OK, "{}");
        handler.Enqueue(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new CancellationAfterWriteHttpContent(
                GitHubProjectSyncTestOperations.CompleteProjectFields(statusOptions),
                cancellation.Cancel)
        });
        using var httpClient = new HttpClient(handler);
        var syncer = new GitHubRoadmapSyncer(RoadmapProject.Load(workspace.RootPath), httpClient);

        // Act
        Func<Task> action = () => syncer.Apply(cancellation.Token);
        var exception = await action.ShouldThrow<OperationCanceledException>();

        // Assert
        exception.CancellationToken.ShouldBe(cancellation.Token);
    }

    [Fact]
    public async Task GitHub_sync_preview_reports_mapped_issues_without_network()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var syncOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var syncError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace("\"labels\": [", "\"integrations\": { \"github\": { \"issue\": 997 } },\n  \"labels\": [", StringComparison.Ordinal));

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["github", "sync", "--root", workspace.RootPath], syncOutput, syncError, workspace.RootPath, TestContext.Current.CancellationToken);
        var outputText = syncOutput.ToString();

        // Assert
        exitCode.ShouldBe(0);
        outputText.ShouldContain("preview: sync labels for owner/repository#997 from RM-001", StringComparison.Ordinal);
    }

    [Fact]
    public async Task GitHub_roadmap_syncer_apply_requires_a_token_for_authenticated_sync()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        GitHubProjectSyncTestOperations.MapDefaultItem(workspace);
        var syncer = new GitHubRoadmapSyncer(RoadmapProject.Load(workspace.RootPath));
        using var ghTokenScope = new EnvironmentVariableScope("GH_TOKEN", null);
        using var gitHubTokenScope = new EnvironmentVariableScope("GITHUB_TOKEN", null);

        // Act
        var action = (Func<Task>)(async () => await syncer.Apply(TestContext.Current.CancellationToken));
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Set GH_TOKEN or GITHUB_TOKEN before running authenticated GitHub sync.");
    }

    [Fact]
    public async Task GitHub_import_requires_a_token_for_authenticated_import()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        using var error = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        GitHubRoadmapIntakeTestOperations.Configure(
            workspace,
            """
            {
              "repository": "owner/repository",
              "snapshotDigest": "0000000000000000000000000000000000000000000000000000000000000000",
              "mechanicalPriorityOverride": {
                "firstItemNumber": 18,
                "reach": 1,
                "impact": "1 plus direct open blockers, capped at impactCap.",
                "impactCap": 5,
                "confidence": 0.1,
                "effort": { "type: epic": 8, "type: feature": 5, "type: enabler": 3, "type: docs": 2, "type: chore": 2, "default": 3 },
                "order": "Existing reviewed orders remain first; imported work uses topological order, score descending, then canonical ID."
              },
              "blockerEdges": [],
              "closedItemTransitions": [],
              "directCanonicalPrimaries": [],
              "childrenOfCanonicalPrimaries": [],
              "unmappedStructuralRoots": [],
              "needsHuman": [],
              "integrity": {
                "expectedIssueCount": 0,
                "expectedDirectCanonicalPrimaryCount": 0,
                "expectedChildrenOfCanonicalPrimaryCount": 0,
                "expectedUnmappedStructuralRootCount": 0,
                "expectedNeedsHumanCount": 0,
                "expectedBlockerEdgeCount": 0,
                "dispositionsAreDisjoint": true,
                "dispositionsCoverSnapshot": true
              }
            }
            """);
        using var ghTokenScope = new EnvironmentVariableScope("GH_TOKEN", null);
        using var gitHubTokenScope = new EnvironmentVariableScope("GITHUB_TOKEN", null);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["github", "import", "--root", workspace.RootPath], output, error, workspace.RootPath, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(1);
        output.ToString().ShouldBe(string.Empty);
        error.ToString().ShouldBe("sharedkernel-repo: Set GH_TOKEN or GITHUB_TOKEN before running authenticated GitHub import." + Environment.NewLine);
    }

    [Fact]
    public async Task GitHub_sync_preview_preflights_existing_project_membership_without_mutating()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var syncOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var syncError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        GitHubProjectSyncTestOperations.EnableProjectTarget(workspace);
        GitHubProjectSyncTestOperations.MapDefaultItem(workspace);
        using var handler = new TestHttpMessageHandler();
        GitHubProjectSyncTestOperations.EnqueueExistingProjectPreflight(
            handler,
            GitHubProjectSyncTestOperations.ProjectFieldsWithValidStatus("""{ "id": "order", "name": "Roadmap order", "dataType": "NUMBER" }"""),
            "{ \"data\": { \"node\": { \"fieldValues\": { \"nodes\": [] } } } }");
        using var httpClient = new HttpClient(handler);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["github", "sync", "--root", workspace.RootPath], syncOutput, syncError, workspace.RootPath, httpClient, TestContext.Current.CancellationToken);
        var outputText = syncOutput.ToString();

        // Assert
        exitCode.ShouldBe(0);
        outputText.ShouldContain("preview: GitHub Project 1 already contains owner/repository#997", StringComparison.Ordinal);
        outputText.ShouldContain("preview: set owner/repository#997 Project field Roadmap order to 10", StringComparison.Ordinal);
        handler.Requests.ShouldNotContain(request => request.Body?.Contains("mutation", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task GitHub_sync_preview_defers_field_writes_until_missing_project_membership_is_added()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var syncOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var syncError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        GitHubProjectSyncTestOperations.EnableProjectTarget(workspace);
        GitHubProjectSyncTestOperations.MapDefaultItem(workspace);
        using var handler = new TestHttpMessageHandler();
        GitHubProjectSyncTestOperations.EnqueueMissingProjectPreflight(
            handler,
            GitHubProjectSyncTestOperations.ProjectFieldsWithValidStatus("""{ "id": "order", "name": "Roadmap order", "dataType": "NUMBER" }"""));
        using var httpClient = new HttpClient(handler);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["github", "sync", "--root", workspace.RootPath], syncOutput, syncError, workspace.RootPath, httpClient, TestContext.Current.CancellationToken);
        var outputText = syncOutput.ToString();

        // Assert
        exitCode.ShouldBe(0);
        outputText.ShouldContain("preview: add owner/repository#997 to GitHub Project 1", StringComparison.Ordinal);
        outputText.ShouldContain("preview: set owner/repository#997 Project field Roadmap order to 10 after adding it", StringComparison.Ordinal);
        handler.Requests.Count.ShouldBe(4);
        handler.Requests.ShouldNotContain(request => request.Body?.Contains("mutation", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task GitHub_sync_preview_blocks_project_proposals_for_invalid_noncurrent_status_options()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var syncOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var syncError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        GitHubProjectSyncTestOperations.EnableProjectTarget(workspace);
        GitHubProjectSyncTestOperations.MapDefaultItem(workspace);
        var statusOptions = """
            [
              { "id": "proposed", "name": "proposed" },
              { "id": "ready", "name": "ready" },
              { "id": "in-progress", "name": "in_progress" },
              { "id": "done", "name": "done" },
              { "id": "done", "name": "legacy" },
              { "id": "dropped", "name": "dropped" }
            ]
            """;
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"id\": \"project-id\", \"number\": 1, \"owner\": { \"login\": \"owner\" } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, GitHubProjectSyncTestOperations.CompleteProjectFields(statusOptions));
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"repository\": { \"issue\": { \"id\": \"issue-id\" } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"items\": { \"nodes\": [], \"pageInfo\": { \"hasNextPage\": false, \"endCursor\": null } } } } }");
        using var httpClient = new HttpClient(handler);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["github", "sync", "--root", workspace.RootPath], syncOutput, syncError, workspace.RootPath, httpClient, TestContext.Current.CancellationToken);
        var outputText = syncOutput.ToString();

        // Assert
        exitCode.ShouldBe(0);
        outputText.ShouldContain("preview: skipped projection of owner/repository#997 to GitHub Project 1 because Roadmap status cannot be projected from roadmap source", StringComparison.Ordinal);
        handler.Requests.Count.ShouldBe(2);
        handler.Requests.ShouldNotContain(request => request.Body?.Contains("mutation", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task GitHub_sync_preview_rejects_an_unverified_project_target_without_mutating()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var syncOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var syncError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        GitHubProjectSyncTestOperations.EnableProjectTarget(workspace);
        GitHubProjectSyncTestOperations.MapDefaultItem(workspace);
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"id\": \"project-id\", \"number\": 2, \"owner\": { \"login\": \"owner\" } } } }");
        using var httpClient = new HttpClient(handler);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["github", "sync", "--root", workspace.RootPath], syncOutput, syncError, workspace.RootPath, httpClient, TestContext.Current.CancellationToken);
        var errorText = syncError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("Configured GitHub Project target could not be verified.", StringComparison.Ordinal);
        handler.Requests.Count.ShouldBe(1);
        handler.Requests.ShouldNotContain(request => request.Body?.Contains("mutation", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task GitHub_sync_preview_reports_project_value_conflicts_without_mutating()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var syncOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var syncError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        GitHubProjectSyncTestOperations.EnableProjectTarget(workspace);
        GitHubProjectSyncTestOperations.MapDefaultItem(workspace);
        using var handler = new TestHttpMessageHandler();
        GitHubProjectSyncTestOperations.EnqueueExistingProjectPreflight(
            handler,
            GitHubProjectSyncTestOperations.ProjectFieldsWithValidStatus("""{ "id": "order", "name": "Roadmap order", "dataType": "NUMBER" }"""),
            "{ \"data\": { \"node\": { \"fieldValues\": { \"nodes\": [{ \"number\": 11, \"field\": { \"id\": \"order\", \"name\": \"Roadmap order\" } }] } } } }");
        using var httpClient = new HttpClient(handler);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["github", "sync", "--root", workspace.RootPath], syncOutput, syncError, workspace.RootPath, httpClient, TestContext.Current.CancellationToken);
        var outputText = syncOutput.ToString();

        // Assert
        exitCode.ShouldBe(0);
        outputText.ShouldContain("drift: owner/repository#997 Project field Roadmap order cannot be projected from roadmap source", StringComparison.Ordinal);
        handler.Requests.ShouldNotContain(request => request.Body?.Contains("mutation", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task GitHub_sync_preview_sanitizes_transport_failures()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var syncOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var syncError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        GitHubProjectSyncTestOperations.EnableProjectTarget(workspace);
        GitHubProjectSyncTestOperations.MapDefaultItem(workspace);
        using var handler = TestHttpMessageHandler.FromException(new HttpRequestException("ghp_test_token"));
        using var httpClient = new HttpClient(handler);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["github", "sync", "--root", workspace.RootPath], syncOutput, syncError, workspace.RootPath, httpClient, TestContext.Current.CancellationToken);
        var errorText = syncError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("sharedkernel-repo: GitHub sync request failed.", StringComparison.Ordinal);
        errorText.ShouldNotContain("ghp_test_token", StringComparison.Ordinal);
    }

    [Fact]
    public async Task GitHub_sync_preview_preflights_project_schema_without_issue_mappings()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var syncOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var syncError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        GitHubProjectSyncTestOperations.EnableProjectTarget(workspace);
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"id\": \"project-id\", \"number\": 1, \"owner\": { \"login\": \"owner\" } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"fields\": { \"nodes\": [] } } } }");
        using var httpClient = new HttpClient(handler);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["github", "sync", "--root", workspace.RootPath], syncOutput, syncError, workspace.RootPath, httpClient, TestContext.Current.CancellationToken);
        var outputText = syncOutput.ToString();

        // Assert
        exitCode.ShouldBe(0);
        outputText.ShouldContain("No roadmap items have GitHub issue mappings or explicit creation requests.", StringComparison.Ordinal);
        outputText.ShouldContain("preview: GitHub Project 1 field Roadmap order cannot be projected from roadmap source", StringComparison.Ordinal);
        handler.Requests.Count.ShouldBe(2);
        handler.Requests.ShouldNotContain(request => request.Body?.Contains("mutation", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task GitHub_sync_preview_preflights_project_schema_for_issue_creation_intents()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var syncOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var syncError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        GitHubProjectSyncTestOperations.EnableProjectTarget(workspace);
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace("\"labels\": [", "\"integrations\": { \"github\": { \"issue\": \"create\" } },\n  \"labels\": [", StringComparison.Ordinal));
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"id\": \"project-id\", \"number\": 1, \"owner\": { \"login\": \"owner\" } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"fields\": { \"nodes\": [] } } } }");
        using var httpClient = new HttpClient(handler);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["github", "sync", "--root", workspace.RootPath], syncOutput, syncError, workspace.RootPath, httpClient, TestContext.Current.CancellationToken);
        var outputText = syncOutput.ToString();

        // Assert
        exitCode.ShouldBe(0);
        outputText.ShouldContain("preview: create GitHub issue for owner/repository from RM-001", StringComparison.Ordinal);
        outputText.ShouldContain("preview: GitHub Project 1 field Roadmap order cannot be projected from roadmap source", StringComparison.Ordinal);
        handler.Requests.Count.ShouldBe(2);
        handler.Requests.ShouldNotContain(request => request.Body?.Contains("mutation", StringComparison.Ordinal) == true);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("[{ \"id\": \"\", \"name\": \"proposed\" }, { \"id\": \"\", \"name\": \"ready\" }, { \"id\": \"\", \"name\": \"in_progress\" }, { \"id\": \"\", \"name\": \"done\" }, { \"id\": \"\", \"name\": \"dropped\" }]")]
    [InlineData("""
        [
          { "id": "proposed", "name": "proposed" },
          { "id": "duplicate", "name": "proposed" },
          { "id": "ready", "name": "ready" },
          { "id": "in-progress", "name": "in_progress" },
          { "id": "done", "name": "done" },
          { "id": "dropped", "name": "dropped" }
        ]
        """)]
    [InlineData("""
        [
          { "id": "shared", "name": "proposed" },
          { "id": "shared", "name": "ready" },
          { "id": "in-progress", "name": "in_progress" },
          { "id": "done", "name": "done" },
          { "id": "dropped", "name": "dropped" }
        ]
        """)]
    public async Task GitHub_sync_preview_rejects_unaddressable_or_ambiguous_status_options(string statusOptions)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var syncOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var syncError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        GitHubProjectSyncTestOperations.EnableProjectTarget(workspace);
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"id\": \"project-id\", \"number\": 1, \"owner\": { \"login\": \"owner\" } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, GitHubProjectSyncTestOperations.CompleteProjectFields(statusOptions));
        using var httpClient = new HttpClient(handler);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["github", "sync", "--root", workspace.RootPath], syncOutput, syncError, workspace.RootPath, httpClient, TestContext.Current.CancellationToken);
        var outputText = syncOutput.ToString();

        // Assert
        exitCode.ShouldBe(0);
        outputText.ShouldContain("preview: GitHub Project 1 field Roadmap status cannot be projected from roadmap source", StringComparison.Ordinal);
        handler.Requests.ShouldNotContain(request => request.Body?.Contains("mutation", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task Verify_reports_stale_order_file()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        workspace.WriteFile("roadmap/order.json", "{ \"items\": [\"RM-404\"] }");

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = verifyError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("Unknown ordered item: RM-404.", StringComparison.Ordinal);
        errorText.ShouldContain("Missing ordered item: RM-001.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task GitHub_sync_preview_respects_disabled_integration()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var syncOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var syncError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["github", "sync", "--root", workspace.RootPath], syncOutput, syncError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = syncError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("GitHub sync is disabled in roadmap/config.json.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task GitHub_sync_preview_defaults_to_disabled_when_enabled_is_missing()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var syncOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var syncError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var configText = workspace.ReadFile("roadmap/config.json");
        workspace.WriteFile("roadmap/config.json", configText.Replace("      \"enabled\": false,\n", string.Empty, StringComparison.Ordinal));

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["github", "sync", "--root", workspace.RootPath], syncOutput, syncError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = syncError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("GitHub sync is disabled in roadmap/config.json.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_reports_duplicate_github_issue_mappings()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace("\"labels\": [", "\"integrations\": { \"github\": { \"issue\": 997 } },\n  \"labels\": [", StringComparison.Ordinal));
        workspace.WriteFile("roadmap/items/RM-002-follow-up.json", RoadmapTestContent.IssueWithGitHubMappingJson);
        workspace.WriteFile("roadmap/order.json", "{ \"items\": [\"RM-001\", \"RM-002\"] }");

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = verifyError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("Duplicate GitHub issue mapping: 997.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_reports_unknown_theme()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace("\"theme\": \"repo-operations\"", "\"theme\": \"missing-theme\"", StringComparison.Ordinal));

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = verifyError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("Unknown theme: missing-theme.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_reports_missing_theme_without_unknown_theme_cascade()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace("  \"theme\": \"repo-operations\",\n", string.Empty, StringComparison.Ordinal));

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = verifyError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("Missing required string property: theme.", StringComparison.Ordinal);
        errorText.ShouldNotContain("Unknown theme:", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_reports_non_object_order_file()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        workspace.WriteFile("roadmap/order.json", "[]");

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = verifyError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("order.json root must be a JSON object.", StringComparison.Ordinal);
        errorText.ShouldNotContain("sharedkernel-repo:", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_reports_malformed_json_files_with_paths()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        workspace.WriteFile("roadmap/config.json", "{");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", "{");
        workspace.WriteFile("roadmap/themes/repo-operations.json", "{");
        workspace.WriteFile("roadmap/order.json", "{");

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = verifyError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("roadmap/config.json: Invalid JSON:", StringComparison.Ordinal);
        errorText.ShouldContain("roadmap/items/RM-001-roadmap-gitops.json: Invalid JSON:", StringComparison.Ordinal);
        errorText.ShouldContain("roadmap/themes/repo-operations.json: Invalid JSON:", StringComparison.Ordinal);
        errorText.ShouldContain("roadmap/order.json: Invalid JSON:", StringComparison.Ordinal);
        errorText.ShouldNotContain("sharedkernel-repo:", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_reports_order_file_sequence_mismatch()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        workspace.WriteFile("roadmap/items/RM-002-follow-up.json", RoadmapTestContent.HigherPriorityIssueJson);
        workspace.WriteFile("roadmap/order.json", "{ \"items\": [\"RM-001\", \"RM-002\"] }");

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = verifyError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("order.json items must match priority order: order ascending, score descending, then id.", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Verify_order_file_uses_priority_tiebreakers()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var tiedItem = RoadmapTestContent.HigherPriorityIssueJson
            .Replace("\"id\": \"RM-002\"", "\"id\": \"RM-000\"", StringComparison.Ordinal)
            .Replace("\"order\": 5", "\"order\": 10", StringComparison.Ordinal)
            .Replace("\"reach\": 10", "\"reach\": 1", StringComparison.Ordinal);
        workspace.WriteFile("roadmap/items/RM-000-follow-up.json", tiedItem);
        workspace.WriteFile("roadmap/order.json", "{ \"items\": [\"RM-001\", \"RM-000\"] }");

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);

        // Assert
        exitCode.ShouldBe(0);
    }

    [Fact]
    public async Task Verify_reports_blocked_by_cycles()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var verifyError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var epicText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", epicText
            .Replace("\"blockedBy\": []", "\"blockedBy\": [\"RM-002\"]", StringComparison.Ordinal)
            .Replace("\"blocks\": []", "\"blocks\": [\"RM-002\"]", StringComparison.Ordinal));
        workspace.WriteFile("roadmap/items/RM-002-follow-up.json", RoadmapTestContent.BlockedIssueJson
            .Replace("\"blocks\": []", "\"blocks\": [\"RM-001\"]", StringComparison.Ordinal));
        workspace.WriteFile("roadmap/order.json", "{ \"items\": [\"RM-001\", \"RM-002\"] }");

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["verify", "--root", workspace.RootPath], verifyOutput, verifyError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = verifyError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("blockedBy cycle includes", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_next_priority_orders_items_by_order_score_and_id()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var getOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var getError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var defaultItem = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        var lowerScoreItem = RoadmapTestContent.HigherPriorityIssueJson
            .Replace("\"id\": \"RM-002\"", "\"id\": \"RM-000\"", StringComparison.Ordinal)
            .Replace("\"order\": 5", "\"order\": 10", StringComparison.Ordinal)
            .Replace("\"reach\": 10", "\"reach\": 1", StringComparison.Ordinal);
        var sameScoreItem = defaultItem
            .Replace("\"id\": \"RM-001\"", "\"id\": \"RM-002\"", StringComparison.Ordinal)
            .Replace("Establish GitOps roadmap and repo configuration tooling", "Same score follow-up", StringComparison.Ordinal);
        workspace.WriteFile("roadmap/items/RM-000-follow-up.json", lowerScoreItem);
        workspace.WriteFile("roadmap/items/RM-002-follow-up.json", sameScoreItem);
        workspace.WriteFile("roadmap/order.json", "{ \"items\": [\"RM-001\", \"RM-002\", \"RM-000\"] }");

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["get", "next-priority", "--limit", "3", "--root", workspace.RootPath], getOutput, getError, workspace.RootPath, TestContext.Current.CancellationToken);
        var outputText = getOutput.ToString();
        var firstIndex = outputText.IndexOf("RM-001 |", StringComparison.Ordinal);
        var secondIndex = outputText.IndexOf("RM-002 |", StringComparison.Ordinal);
        var thirdIndex = outputText.IndexOf("RM-000 |", StringComparison.Ordinal);

        // Assert
        exitCode.ShouldBe(0);
        firstIndex.ShouldBeGreaterThan(-1);
        secondIndex.ShouldBeGreaterThan(-1);
        thirdIndex.ShouldBeGreaterThan(-1);
        firstIndex.ShouldBeLessThan(secondIndex);
        secondIndex.ShouldBeLessThan(thirdIndex);
    }

    [Fact]
    public async Task Get_next_blockers_orders_equal_blockers_by_id()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var getOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var getError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var defaultItem = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        var laterId = defaultItem
            .Replace("\"id\": \"RM-001\"", "\"id\": \"RM-003\"", StringComparison.Ordinal)
            .Replace("\"type\": \"epic\"", "\"type\": \"blocker\"", StringComparison.Ordinal);
        var earlierId = defaultItem
            .Replace("\"id\": \"RM-001\"", "\"id\": \"RM-002\"", StringComparison.Ordinal)
            .Replace("\"type\": \"epic\"", "\"type\": \"blocker\"", StringComparison.Ordinal);
        workspace.WriteFile("roadmap/items/a-blocker.json", laterId);
        workspace.WriteFile("roadmap/items/b-blocker.json", earlierId);
        workspace.WriteFile("roadmap/order.json", "{ \"items\": [\"RM-001\", \"RM-002\", \"RM-003\"] }");

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["get", "next-blockers", "--limit", "2", "--root", workspace.RootPath], getOutput, getError, workspace.RootPath, TestContext.Current.CancellationToken);
        var outputText = getOutput.ToString();

        // Assert
        exitCode.ShouldBe(0);
        outputText.ShouldStartWith("RM-002 | blocker", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_low_hanging_fruit_orders_equal_items_by_id()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var getOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var getError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var defaultItem = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        var laterId = defaultItem
            .Replace("\"id\": \"RM-001\"", "\"id\": \"RM-003\"", StringComparison.Ordinal)
            .Replace("\"type\": \"epic\"", "\"type\": \"issue\"", StringComparison.Ordinal);
        var earlierId = defaultItem
            .Replace("\"id\": \"RM-001\"", "\"id\": \"RM-002\"", StringComparison.Ordinal)
            .Replace("\"type\": \"epic\"", "\"type\": \"issue\"", StringComparison.Ordinal);
        workspace.WriteFile("roadmap/items/a-issue.json", laterId);
        workspace.WriteFile("roadmap/items/b-issue.json", earlierId);
        workspace.WriteFile("roadmap/order.json", "{ \"items\": [\"RM-001\", \"RM-002\", \"RM-003\"] }");

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["get", "low-hanging-fruit", "--type", "issue", "--limit", "2", "--root", workspace.RootPath], getOutput, getError, workspace.RootPath, TestContext.Current.CancellationToken);
        var outputText = getOutput.ToString();

        // Assert
        exitCode.ShouldBe(0);
        outputText.ShouldStartWith("RM-002 | issue", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_pareto_orders_equal_score_and_effort_by_order_then_id()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var getOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var getError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var defaultItem = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        var items = new[]
        {
            (FileName: "a-issue.json", Id: "RM-002", Order: 20),
            (FileName: "b-issue.json", Id: "RM-003", Order: 10),
            (FileName: "c-issue.json", Id: "RM-006", Order: 10),
            (FileName: "d-issue.json", Id: "RM-004", Order: 30),
            (FileName: "e-issue.json", Id: "RM-005", Order: 40),
            (FileName: "f-issue.json", Id: "RM-007", Order: 50)
        };
        foreach (var item in items)
        {
            var itemText = defaultItem
                .Replace("\"id\": \"RM-001\"", $"\"id\": \"{item.Id}\"", StringComparison.Ordinal)
                .Replace("\"type\": \"epic\"", "\"type\": \"issue\"", StringComparison.Ordinal)
                .Replace("\"order\": 10", $"\"order\": {item.Order}", StringComparison.Ordinal);
            workspace.WriteFile($"roadmap/items/{item.FileName}", itemText);
        }

        workspace.WriteFile("roadmap/order.json", "{ \"items\": [\"RM-001\", \"RM-003\", \"RM-006\", \"RM-002\", \"RM-004\", \"RM-005\", \"RM-007\"] }");

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["get", "pareto", "--type", "issue", "--limit", "2", "--root", workspace.RootPath], getOutput, getError, workspace.RootPath, TestContext.Current.CancellationToken);
        var outputText = getOutput.ToString();

        // Assert
        exitCode.ShouldBe(0);
        outputText.ShouldStartWith("RM-003 | issue", StringComparison.Ordinal);
        outputText.ShouldContain("RM-006 | issue", StringComparison.Ordinal);
        outputText.ShouldNotContain("RM-002 | issue", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_pareto_limit_uses_unblocked_open_items()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var getOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var getError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        var epicText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", epicText.Replace("\"blocks\": []", "\"blocks\": [\"RM-003\", \"RM-004\", \"RM-005\", \"RM-006\"]", StringComparison.Ordinal));
        var unblockedItem = RoadmapTestContent.UnblockedEnablerJson.Replace("\"id\": \"RM-003\"", "\"id\": \"RM-002\"", StringComparison.Ordinal);
        workspace.WriteFile("roadmap/items/RM-002-enabler.json", unblockedItem);
        for (var index = 3; index <= 6; index++)
        {
            var blockedItem = RoadmapTestContent.BlockedIssueJson
                .Replace("\"id\": \"RM-002\"", $"\"id\": \"RM-00{index}\"", StringComparison.Ordinal)
                .Replace("\"order\": 20", $"\"order\": {index * 10 + 10}", StringComparison.Ordinal);
            workspace.WriteFile($"roadmap/items/RM-00{index}-blocked.json", blockedItem);
        }

        workspace.WriteFile("roadmap/order.json", "{ \"items\": [\"RM-001\", \"RM-002\", \"RM-003\", \"RM-004\", \"RM-005\", \"RM-006\"] }");

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["get", "pareto", "--limit", "10", "--root", workspace.RootPath], getOutput, getError, workspace.RootPath, TestContext.Current.CancellationToken);
        var outputText = getOutput.ToString();

        // Assert
        exitCode.ShouldBe(0);
        outputText.ShouldContain("RM-002 |", StringComparison.Ordinal);
        outputText.ShouldNotContain("RM-001 |", StringComparison.Ordinal);
    }

    [Fact]
    public async Task GitHub_roadmap_syncer_preview_orders_mapped_items_by_priority()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        var defaultItem = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", defaultItem.Replace("\"labels\": [", "\"integrations\": { \"github\": { \"issue\": 997 } },\n  \"labels\": [", StringComparison.Ordinal));
        var followUpItem = RoadmapTestContent.HigherPriorityIssueJson
            .Replace("\"id\": \"RM-002\"", "\"id\": \"RM-000\"", StringComparison.Ordinal)
            .Replace("\"order\": 5", "\"order\": 10", StringComparison.Ordinal)
            .Replace("\"reach\": 10", "\"reach\": 1", StringComparison.Ordinal)
            .Replace("\"labels\": [", "\"integrations\": { \"github\": { \"issue\": 998 } },\n  \"labels\": [", StringComparison.Ordinal);
        workspace.WriteFile("roadmap/items/RM-000-follow-up.json", followUpItem);
        workspace.WriteFile("roadmap/order.json", "{ \"items\": [\"RM-001\", \"RM-000\"] }");
        var syncer = new GitHubRoadmapSyncer(RoadmapProject.Load(workspace.RootPath));

        // Act
        var result = await syncer.Preview(TestContext.Current.CancellationToken);

        // Assert
        result.Messages[0].ShouldBe("preview: sync labels for owner/repository#997 from RM-001");
        result.Messages[1].ShouldBe("preview: sync labels for owner/repository#998 from RM-000");
    }

    [Fact]
    public async Task GitHub_roadmap_syncer_apply_syncs_labels_after_confirming_the_mapping_is_not_a_pull_request()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace("\"labels\": [", "\"integrations\": { \"github\": { \"issue\": 997 } },\n  \"labels\": [", StringComparison.Ordinal));
        var project = RoadmapProject.Load(workspace.RootPath);
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.NotFound, "{}");
        handler.EnqueueJson(HttpStatusCode.OK, GitHubSyncTestResponseFactory.IssueMetadata(997));
        handler.EnqueueJson(HttpStatusCode.OK, "{}");
        using var httpClient = new HttpClient(handler);
        var syncer = new GitHubRoadmapSyncer(project, httpClient);

        // Act
        var result = await syncer.Apply(TestContext.Current.CancellationToken);

        // Assert
        result.Messages.ShouldContain("updated labels for owner/repository#997 from RM-001");
        handler.Requests.Count.ShouldBe(3);
        handler.Requests[0].Method.ShouldBe(HttpMethod.Get);
        handler.Requests[0].PathAndQuery.ShouldBe("/repos/owner/repository/pulls/997");
        handler.Requests[1].Method.ShouldBe(HttpMethod.Get);
        handler.Requests[1].PathAndQuery.ShouldBe("/repos/owner/repository/issues/997");
        handler.Requests[2].Method.ShouldBe(HttpMethod.Post);
        handler.Requests[2].PathAndQuery.ShouldBe("/repos/owner/repository/issues/997/labels");
        handler.Requests.ShouldNotContain(request => request.Method == HttpMethod.Patch);
        handler.Requests[2].Body.ShouldNotBeNull();
        handler.Requests[2].Body.ShouldContain("area: tooling", StringComparison.Ordinal);
    }

    [Fact]
    public async Task GitHub_roadmap_syncer_preview_describes_explicit_issue_creation_without_http()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace("\"labels\": [", "\"integrations\": { \"github\": { \"issue\": \"create\" } },\n  \"labels\": [", StringComparison.Ordinal));
        var project = RoadmapProject.Load(workspace.RootPath);
        using var handler = new TestHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var syncer = new GitHubRoadmapSyncer(project, httpClient);

        // Act
        var result = await syncer.Preview(TestContext.Current.CancellationToken);

        // Assert
        result.Messages.ShouldContain("preview: create GitHub issue for owner/repository from RM-001");
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task GitHub_roadmap_syncer_apply_reports_extra_github_labels_without_removing_them()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace("\"labels\": [", "\"integrations\": { \"github\": { \"issue\": 997 } },\n  \"labels\": [", StringComparison.Ordinal));
        var project = RoadmapProject.Load(workspace.RootPath);
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.NotFound, "{}");
        handler.EnqueueJson(HttpStatusCode.OK, GitHubSyncTestResponseFactory.IssueMetadata(labels: "[{ \"name\": \"AREA: TOOLING\" }, { \"name\": \"manual\" }]"));
        handler.EnqueueJson(HttpStatusCode.OK, "{}");
        using var httpClient = new HttpClient(handler);
        var syncer = new GitHubRoadmapSyncer(project, httpClient);

        // Act
        var result = await syncer.Apply(TestContext.Current.CancellationToken);

        // Assert
        result.Messages.ShouldContain("drift: owner/repository#997 has extra GitHub label manual");
        result.Messages.ShouldNotContain("drift: owner/repository#997 has extra GitHub label AREA: TOOLING");
        handler.Requests.ShouldNotContain(request => request.Method == HttpMethod.Delete || request.Method == HttpMethod.Patch || request.Method == HttpMethod.Put);
    }

    [Fact]
    public async Task GitHub_roadmap_syncer_apply_creates_and_persists_an_explicit_issue_mapping()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace("\"labels\": [", "\"integrations\": { \"github\": { \"issue\": \"create\" } },\n  \"labels\": [", StringComparison.Ordinal));
        var project = RoadmapProject.Load(workspace.RootPath);
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.Created, "{ \"number\": 1200 }");
        handler.EnqueueJson(HttpStatusCode.NotFound, "{}");
        handler.EnqueueJson(HttpStatusCode.OK, GitHubSyncTestResponseFactory.IssueMetadata(1200));
        handler.EnqueueJson(HttpStatusCode.OK, "{}");
        using var httpClient = new HttpClient(handler);
        var syncer = new GitHubRoadmapSyncer(project, httpClient);

        // Act
        var result = await syncer.Apply(TestContext.Current.CancellationToken);
        var persistedItem = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");

        // Assert
        result.Messages.ShouldContain("created owner/repository#1200 from RM-001");
        persistedItem.ShouldContain("\"issue\": 1200", StringComparison.Ordinal);
        handler.Requests[0].Method.ShouldBe(HttpMethod.Post);
        handler.Requests[0].PathAndQuery.ShouldBe("/repos/owner/repository/issues");
        handler.Requests[0].Body.ShouldNotBeNull();
        handler.Requests[0].Body.ShouldContain("Establish GitOps roadmap and repo configuration tooling", StringComparison.Ordinal);
        handler.Requests[0].Body.ShouldContain("area: tooling", StringComparison.Ordinal);
    }

    [Fact]
    public async Task GitHub_roadmap_syncer_apply_throws_timeout_when_issue_creation_limit_is_reached()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace("\"labels\": [", "\"integrations\": { \"github\": { \"issue\": \"create\" } },\n  \"labels\": [", StringComparison.Ordinal));
        var project = RoadmapProject.Load(workspace.RootPath);
        var timeProvider = new FakeTimeProvider();
        using var handler = new TimingOutHttpMessageHandler(timeProvider);
        using var httpClient = new HttpClient(handler);
        var syncer = new GitHubRoadmapSyncer(project, httpClient, timeProvider);

        // Act
        Func<Task> action = () => syncer.Apply(TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<TimeoutException>();

        // Assert
        exception.Message.ShouldBe("GitHub sync timed out after 30 seconds.");
        workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json").ShouldContain("\"issue\": \"create\"", StringComparison.Ordinal);
        var lockPath = Path.Combine(workspace.RootPath, "roadmap/items/RM-001-roadmap-gitops.json.lock");
        File.Exists(lockPath).ShouldBeTrue();
        using var retryHandler = new TestHttpMessageHandler();
        using var retryHttpClient = new HttpClient(retryHandler);
        var retrySyncer = new GitHubRoadmapSyncer(project, retryHttpClient, timeProvider);
        Func<Task> retry = () => retrySyncer.Apply(TestContext.Current.CancellationToken);
        await retry.ShouldThrow<InvalidOperationException>();
        retryHandler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task GitHub_roadmap_syncer_apply_adopts_a_mapping_persisted_after_project_load()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace("\"labels\": [", "\"integrations\": { \"github\": { \"issue\": \"create\" } },\n  \"labels\": [", StringComparison.Ordinal));
        var project = RoadmapProject.Load(workspace.RootPath);
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json").Replace("\"issue\": \"create\"", "\"issue\": 1200", StringComparison.Ordinal));
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.NotFound, "{}");
        handler.EnqueueJson(HttpStatusCode.OK, GitHubSyncTestResponseFactory.IssueMetadata(1200));
        handler.EnqueueJson(HttpStatusCode.OK, "{}");
        using var httpClient = new HttpClient(handler);
        var syncer = new GitHubRoadmapSyncer(project, httpClient);

        // Act
        var result = await syncer.Apply(TestContext.Current.CancellationToken);

        // Assert
        result.Messages.ShouldNotContain("created owner/repository#1200 from RM-001");
        handler.Requests.ShouldNotContain(request => request.Method == HttpMethod.Post && request.PathAndQuery == "/repos/owner/repository/issues");
        handler.Requests.ShouldContain(request => request.PathAndQuery == "/repos/owner/repository/issues/1200/labels");
    }

    [Fact]
    public async Task GitHub_roadmap_syncer_apply_does_not_overwrite_a_mapping_changed_during_issue_creation()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace("\"labels\": [", "\"integrations\": { \"github\": { \"issue\": \"create\" } },\n  \"labels\": [", StringComparison.Ordinal));
        var project = RoadmapProject.Load(workspace.RootPath);
        using var handler = new BeforeResponseHttpMessageHandler(
            () => new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent("{ \"number\": 1200 }")
            },
            () => workspace.WriteFile(
                "roadmap/items/RM-001-roadmap-gitops.json",
                workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json").Replace("\"issue\": \"create\"", "\"issue\": 1300", StringComparison.Ordinal)));
        using var httpClient = new HttpClient(handler);
        var syncer = new GitHubRoadmapSyncer(project, httpClient);

        // Act
        Func<Task> action = () => syncer.Apply(TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();
        var persistedItem = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");

        // Assert
        exception.Message.ShouldContain("GitHub issue #1200 was created but its roadmap mapping changed", StringComparison.Ordinal);
        persistedItem.ShouldContain("\"issue\": 1300", StringComparison.Ordinal);
        persistedItem.ShouldNotContain("\"issue\": 1200", StringComparison.Ordinal);
        File.Exists(Path.Combine(workspace.RootPath, "roadmap/items/RM-001-roadmap-gitops.json.lock")).ShouldBeTrue();
    }

    [Fact]
    public async Task GitHub_roadmap_syncer_apply_keeps_create_intent_when_issue_creation_fails()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace("\"labels\": [", "\"integrations\": { \"github\": { \"issue\": \"create\" } },\n  \"labels\": [", StringComparison.Ordinal));
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.UnprocessableEntity, "{}");
        using var httpClient = new HttpClient(handler);
        var syncer = new GitHubRoadmapSyncer(RoadmapProject.Load(workspace.RootPath), httpClient);

        // Act
        Func<Task> action = () => syncer.Apply(TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("GitHub issue creation failed: HTTP 422 (request validation failed).");
        workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json").ShouldContain("\"issue\": \"create\"", StringComparison.Ordinal);
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GitHub_roadmap_syncer_apply_retains_the_creation_lock_after_an_ambiguous_server_failure()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace("\"labels\": [", "\"integrations\": { \"github\": { \"issue\": \"create\" } },\n  \"labels\": [", StringComparison.Ordinal));
        var project = RoadmapProject.Load(workspace.RootPath);
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.InternalServerError, "{}");
        using var httpClient = new HttpClient(handler);
        var syncer = new GitHubRoadmapSyncer(project, httpClient);

        // Act
        Func<Task> action = () => syncer.Apply(TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("GitHub issue creation failed: HTTP 500.");
        var lockPath = Path.Combine(workspace.RootPath, "roadmap/items/RM-001-roadmap-gitops.json.lock");
        File.Exists(lockPath).ShouldBeTrue();
        using var retryHandler = new TestHttpMessageHandler();
        using var retryHttpClient = new HttpClient(retryHandler);
        var retrySyncer = new GitHubRoadmapSyncer(project, retryHttpClient);
        Func<Task> retry = () => retrySyncer.Apply(TestContext.Current.CancellationToken);
        await retry.ShouldThrow<InvalidOperationException>();
        retryHandler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task GitHub_roadmap_syncer_preview_describes_conditional_project_membership()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        var configText = workspace.ReadFile("roadmap/config.json");
        workspace.WriteFile("roadmap/config.json", configText.Replace("\"sourceOfTruth\": \"projection\"", "\"sourceOfTruth\": \"projection\",\n      \"projectV2\": { \"id\": \"project-id\", \"owner\": \"OWNER\", \"number\": 1 }", StringComparison.Ordinal));
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace("\"labels\": [", "\"integrations\": { \"github\": { \"issue\": 997 } },\n  \"labels\": [", StringComparison.Ordinal));
        using var handler = new TestHttpMessageHandler();
        GitHubProjectSyncTestOperations.EnqueueExistingProjectPreflight(
            handler,
            "{ \"data\": { \"node\": { \"fields\": { \"nodes\": [] } } } }",
            "{ \"data\": { \"node\": { \"fieldValues\": { \"nodes\": [] } } } }");
        using var httpClient = new HttpClient(handler);
        var syncer = new GitHubRoadmapSyncer(RoadmapProject.Load(workspace.RootPath), httpClient);

        // Act
        var result = await syncer.Preview(TestContext.Current.CancellationToken);

        // Assert
        result.Messages.ShouldContain("preview: ensure owner/repository#997 is in GitHub Project 1");
    }

    [Fact]
    public async Task GitHub_project_field_reads_fail_closed_when_graphql_reports_an_error()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"errors\": [{ \"type\": \"FORBIDDEN\" }] }");
        using var httpClient = new HttpClient(handler);
        var client = new GitHubProjectClient(httpClient);

        // Act
        Func<Task> action = () => client.GetFieldValues("item-id", TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("GitHub Project request failed (FORBIDDEN).");
    }

    [Fact]
    public async Task GitHub_project_client_finds_existing_item_on_a_later_page()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"items\": { \"nodes\": [{ \"id\": \"other-item\", \"content\": { \"id\": \"other-issue\" } }], \"pageInfo\": { \"hasNextPage\": true, \"endCursor\": \"cursor-1\" } } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"items\": { \"nodes\": [{ \"id\": \"target-item\", \"content\": { \"id\": \"target-issue\" } }], \"pageInfo\": { \"hasNextPage\": false, \"endCursor\": null } } } } }");
        using var httpClient = new HttpClient(handler);
        var client = new GitHubProjectClient(httpClient);
        var target = new GitHubProjectTarget("project-id", "owner", 1);

        // Act
        var itemId = await client.FindItemId(target, "target-issue", TestContext.Current.CancellationToken);

        // Assert
        itemId.ShouldBe("target-item");
        handler.Requests.Count.ShouldBe(2);
        handler.Requests[0].Body.ShouldContain("nodes{id content{... on Issue{id}}}pageInfo{hasNextPage endCursor}", StringComparison.Ordinal);
        handler.Requests[1].Body.ShouldContain("cursor-1", StringComparison.Ordinal);
    }

    [Fact]
    public async Task GitHub_project_target_accepts_case_insensitive_owner_login()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"id\": \"project-id\", \"number\": 1, \"owner\": { \"login\": \"owner\" } } } }");
        using var httpClient = new HttpClient(handler);
        var client = new GitHubProjectClient(httpClient);
        var target = new GitHubProjectTarget("project-id", "OWNER", 1);

        // Act
        await client.VerifyTarget(target, TestContext.Current.CancellationToken);

        // Assert
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GitHub_project_fields_fail_closed_when_graphql_reports_an_error()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"errors\": [{ \"type\": \"FORBIDDEN\" }] }");
        using var httpClient = new HttpClient(handler);
        var client = new GitHubProjectClient(httpClient);

        // Act
        Func<Task> action = () => client.GetFields(new GitHubProjectTarget("project-id", "owner", 1), TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("GitHub Project request failed (FORBIDDEN).");
    }

    [Fact]
    public async Task GitHub_project_client_returns_null_when_item_is_not_present()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"items\": { \"nodes\": [], \"pageInfo\": { \"hasNextPage\": false, \"endCursor\": null } } } } }");
        using var httpClient = new HttpClient(handler);
        var client = new GitHubProjectClient(httpClient);

        // Act
        var itemId = await client.FindItemId(new GitHubProjectTarget("project-id", "owner", 1), "missing", TestContext.Current.CancellationToken);

        // Assert
        itemId.ShouldBeNull();
    }

    [Fact]
    public async Task GitHub_project_target_rejects_mismatched_remote_number()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"id\": \"project-id\", \"number\": 2, \"owner\": { \"login\": \"owner\" } } } }");
        using var httpClient = new HttpClient(handler);
        var client = new GitHubProjectClient(httpClient);
        Func<Task> action = () => client.VerifyTarget(new GitHubProjectTarget("project-id", "owner", 1), TestContext.Current.CancellationToken);

        // Act
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("Configured GitHub Project target could not be verified.");
    }

    [Fact]
    public async Task GitHub_project_client_rejects_missing_issue_node_id()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"repository\": { \"issue\": {} } } }");
        using var httpClient = new HttpClient(handler);
        var client = new GitHubProjectClient(httpClient);
        Func<Task> action = () => client.GetIssueNodeId("owner/repository", 1, TestContext.Current.CancellationToken);

        // Act
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("GitHub issue mapping could not be found: #1.");
    }

    [Fact]
    public async Task GitHub_project_client_rejects_http_failure()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.Forbidden, "{}");
        using var httpClient = new HttpClient(handler);
        var client = new GitHubProjectClient(httpClient);
        Func<Task> action = () => client.GetFields(new GitHubProjectTarget("project-id", "owner", 1), TestContext.Current.CancellationToken);

        // Act
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("GitHub Project request failed with HTTP 403.");
    }

    [Fact]
    public async Task GitHub_project_client_rejects_missing_field_values()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": {} } }");
        using var httpClient = new HttpClient(handler);
        var client = new GitHubProjectClient(httpClient);
        Func<Task> action = () => client.GetFieldValues("item-id", TestContext.Current.CancellationToken);

        // Act
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("GitHub Project field values could not be read.");
    }

    [Fact]
    public async Task GitHub_project_client_rejects_missing_project_items()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": {} } }");
        using var httpClient = new HttpClient(handler);
        var client = new GitHubProjectClient(httpClient);
        Func<Task> action = () => client.FindItemId(new GitHubProjectTarget("project-id", "owner", 1), "issue-id", TestContext.Current.CancellationToken);

        // Act
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("GitHub Project items could not be read.");
    }

    [Fact]
    public async Task GitHub_roadmap_syncer_apply_adds_mapped_issue_to_configured_project_once()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        var configText = workspace.ReadFile("roadmap/config.json");
        workspace.WriteFile("roadmap/config.json", configText.Replace("\"sourceOfTruth\": \"projection\"", "\"sourceOfTruth\": \"projection\",\n      \"projectV2\": { \"id\": \"project-id\", \"owner\": \"owner\", \"number\": 1 }", StringComparison.Ordinal));
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace("\"labels\": [", "\"integrations\": { \"github\": { \"issue\": 997 } },\n  \"labels\": [", StringComparison.Ordinal));
        var project = RoadmapProject.Load(workspace.RootPath);
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"id\": \"project-id\", \"number\": 1, \"owner\": { \"login\": \"owner\" } } } }");
        handler.EnqueueJson(HttpStatusCode.NotFound, "{}");
        handler.EnqueueJson(HttpStatusCode.OK, GitHubSyncTestResponseFactory.IssueMetadata(997));
        handler.EnqueueJson(HttpStatusCode.OK, "{}");
        handler.EnqueueJson(HttpStatusCode.OK, GitHubProjectSyncTestOperations.CompleteProjectFields("""
            [
              { "id": "proposed", "name": "proposed" },
              { "id": "ready", "name": "ready" },
              { "id": "in-progress", "name": "in_progress" },
              { "id": "done", "name": "done" },
              { "id": "dropped", "name": "dropped" }
            ]
            """));
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"repository\": { \"issue\": { \"id\": \"issue-id\" } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"items\": { \"nodes\": [], \"pageInfo\": { \"hasNextPage\": false, \"endCursor\": null } } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"addProjectV2ItemById\": { \"item\": { \"id\": \"item-id\" } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"fieldValues\": { \"nodes\": [] } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"updateProjectV2ItemFieldValue\": { \"projectV2Item\": { \"id\": \"item-id\" } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"updateProjectV2ItemFieldValue\": { \"projectV2Item\": { \"id\": \"item-id\" } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"updateProjectV2ItemFieldValue\": { \"projectV2Item\": { \"id\": \"item-id\" } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"updateProjectV2ItemFieldValue\": { \"projectV2Item\": { \"id\": \"item-id\" } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"updateProjectV2ItemFieldValue\": { \"projectV2Item\": { \"id\": \"item-id\" } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"updateProjectV2ItemFieldValue\": { \"projectV2Item\": { \"id\": \"item-id\" } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"updateProjectV2ItemFieldValue\": { \"projectV2Item\": { \"id\": \"item-id\" } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"updateProjectV2ItemFieldValue\": { \"projectV2Item\": { \"id\": \"item-id\" } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"updateProjectV2ItemFieldValue\": { \"projectV2Item\": { \"id\": \"item-id\" } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"updateProjectV2ItemFieldValue\": { \"projectV2Item\": { \"id\": \"item-id\" } } } }");
        using var httpClient = new HttpClient(handler);
        var syncer = new GitHubRoadmapSyncer(project, httpClient);

        // Act
        var result = await syncer.Apply(TestContext.Current.CancellationToken);

        // Assert
        result.Messages.ShouldContain("projected owner/repository#997 to GitHub Project 1");
        handler.Requests.ShouldContain(request => request.PathAndQuery == "/graphql" && request.Method == HttpMethod.Post);
        handler.Requests.ShouldContain(request => request.Body?.Contains("singleSelectOptionId", StringComparison.Ordinal) == true);
        handler.Requests.ShouldContain(request => request.PathAndQuery == "/repos/owner/repository/issues/997/labels" && request.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task GitHub_roadmap_syncer_apply_skips_mapped_items_without_labels()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        var mappedItem = itemText
            .Replace("\"labels\": [", "\"integrations\": { \"github\": { \"issue\": 997 } },\n  \"labels\": [", StringComparison.Ordinal)
            .Replace("\"labels\": [\n    \"area: tooling\",\n    \"configuration\",\n    \"documentation\"\n  ]", "\"labels\": []", StringComparison.Ordinal);
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", mappedItem);
        var project = RoadmapProject.Load(workspace.RootPath);
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.NotFound, "{}");
        handler.EnqueueJson(HttpStatusCode.OK, GitHubSyncTestResponseFactory.IssueMetadata(997));
        using var httpClient = new HttpClient(handler);
        var syncer = new GitHubRoadmapSyncer(project, httpClient);

        // Act
        var result = await syncer.Apply(TestContext.Current.CancellationToken);

        // Assert
        result.Messages.ShouldContain("skipped owner/repository#997 from RM-001 because it has no labels");
        handler.Requests.Count.ShouldBe(2);
        handler.Requests.ShouldNotContain(request => request.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task GitHub_roadmap_syncer_apply_resets_the_timeout_for_each_item()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        var defaultItem = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", defaultItem.Replace("\"labels\": [", "\"integrations\": { \"github\": { \"issue\": 997 } },\n  \"labels\": [", StringComparison.Ordinal));
        var followUpItem = RoadmapTestContent.IssueWithGitHubMappingJson.Replace("\"issue\": 997", "\"issue\": 998", StringComparison.Ordinal);
        workspace.WriteFile("roadmap/items/RM-002-follow-up.json", followUpItem);
        workspace.WriteFile("roadmap/order.json", "{ \"items\": [\"RM-001\", \"RM-002\"] }");
        var project = RoadmapProject.Load(workspace.RootPath);
        var timeProvider = new FakeTimeProvider();
        using var handler = new TimeAdvancingHttpMessageHandler(timeProvider);
        using var httpClient = new HttpClient(handler);
        var syncer = new GitHubRoadmapSyncer(project, httpClient, timeProvider);

        // Act
        var result = await syncer.Apply(TestContext.Current.CancellationToken);

        // Assert
        result.Messages.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GitHub_roadmap_syncer_apply_throws_timeout_when_the_per_item_limit_is_reached()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace("\"labels\": [", "\"integrations\": { \"github\": { \"issue\": 997 } },\n  \"labels\": [", StringComparison.Ordinal));
        var project = RoadmapProject.Load(workspace.RootPath);
        var timeProvider = new FakeTimeProvider();
        using var handler = new TimingOutHttpMessageHandler(timeProvider);
        using var httpClient = new HttpClient(handler);
        var syncer = new GitHubRoadmapSyncer(project, httpClient, timeProvider);

        // Act
        Func<Task> action = () => syncer.Apply(TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<TimeoutException>();

        // Assert
        exception.Message.ShouldBe("GitHub sync timed out after 30 seconds.");
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "GitHub pull request check failed for #997: HTTP 401 (authentication required).")]
    [InlineData(HttpStatusCode.Forbidden, "GitHub pull request check failed for #997: HTTP 403 (access denied or rate limited).")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "GitHub pull request check failed for #997: HTTP 422 (request validation failed).")]
    [InlineData(HttpStatusCode.TooManyRequests, "GitHub pull request check failed for #997: HTTP 429 (rate limited).")]
    [InlineData(HttpStatusCode.BadGateway, "GitHub pull request check failed for #997: HTTP 502.")]
    public async Task GitHub_roadmap_syncer_apply_uses_safe_status_hints_for_pull_request_check_failures(HttpStatusCode statusCode, string expectedMessage)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace("\"labels\": [", "\"integrations\": { \"github\": { \"issue\": 997 } },\n  \"labels\": [", StringComparison.Ordinal));
        var project = RoadmapProject.Load(workspace.RootPath);
        using var handler = new TestHttpMessageHandler();
        handler.Enqueue(GitHubSyncTestResponseFactory.UntrustedError(statusCode));
        using var httpClient = new HttpClient(handler);
        var syncer = new GitHubRoadmapSyncer(project, httpClient);

        // Act
        Func<Task> action = () => syncer.Apply(TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe(expectedMessage);
        exception.Message.ShouldNotContain("ghp_reason_token", StringComparison.Ordinal);
        exception.Message.ShouldNotContain("ghp_body_token", StringComparison.Ordinal);
        exception.Message.ShouldNotContain("ghp_header_token", StringComparison.Ordinal);
        handler.Requests.Count.ShouldBe(1);
        handler.Requests[0].Method.ShouldBe(HttpMethod.Get);
        handler.Requests[0].PathAndQuery.ShouldBe("/repos/owner/repository/pulls/997");
    }

    [Fact]
    public async Task GitHub_roadmap_syncer_apply_does_not_expose_remote_details_for_label_failure()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace("\"labels\": [", "\"integrations\": { \"github\": { \"issue\": 997 } },\n  \"labels\": [", StringComparison.Ordinal));
        var project = RoadmapProject.Load(workspace.RootPath);
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.NotFound, "{}");
        handler.EnqueueJson(HttpStatusCode.OK, GitHubSyncTestResponseFactory.IssueMetadata(997));
        handler.Enqueue(GitHubSyncTestResponseFactory.UntrustedError(HttpStatusCode.UnprocessableEntity));
        using var httpClient = new HttpClient(handler);
        var syncer = new GitHubRoadmapSyncer(project, httpClient);

        // Act
        Func<Task> action = () => syncer.Apply(TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldBe("GitHub label sync failed for #997: HTTP 422 (request validation failed).");
        exception.Message.ShouldNotContain("ghp_reason_token", StringComparison.Ordinal);
        exception.Message.ShouldNotContain("ghp_body_token", StringComparison.Ordinal);
        exception.Message.ShouldNotContain("ghp_header_token", StringComparison.Ordinal);
        handler.Requests.Count.ShouldBe(3);
        handler.Requests[2].Method.ShouldBe(HttpMethod.Post);
    }

    [Fact]
    public async Task GitHub_roadmap_syncer_apply_rejects_pull_request_mappings()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace("\"labels\": [", "\"integrations\": { \"github\": { \"issue\": 997 } },\n  \"labels\": [", StringComparison.Ordinal));
        var project = RoadmapProject.Load(workspace.RootPath);
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{}");
        using var httpClient = new HttpClient(handler);
        var syncer = new GitHubRoadmapSyncer(project, httpClient);

        // Act
        Func<Task> action = () => syncer.Apply(TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("points to a pull request", StringComparison.Ordinal);
        handler.Requests.Count.ShouldBe(1);
        handler.Requests[0].Method.ShouldBe(HttpMethod.Get);
        handler.Requests[0].PathAndQuery.ShouldBe("/repos/owner/repository/pulls/997");
    }

    [Fact]
    public async Task GitHub_roadmap_syncer_apply_reports_empty_mapping_and_create_request_filter()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        using var handler = new TestHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var syncer = new GitHubRoadmapSyncer(RoadmapProject.Load(workspace.RootPath), httpClient);

        // Act
        var result = await syncer.Apply(TestContext.Current.CancellationToken);

        // Assert
        result.Messages.ShouldContain("No roadmap items have GitHub issue mappings or explicit creation requests.");
        result.Messages.Count.ShouldBe(1);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task GitHub_roadmap_syncer_apply_blocks_project_projection_when_roadmap_status_field_is_missing()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        GitHubProjectSyncTestOperations.EnableProjectTarget(workspace);
        GitHubProjectSyncTestOperations.MapDefaultItem(workspace);
        using var handler = new TestHttpMessageHandler();
        GitHubProjectSyncTestOperations.EnqueueExistingProjectItem(
            handler,
            "{ \"data\": { \"node\": { \"fields\": { \"nodes\": [] } } } }",
            "{ \"data\": { \"node\": { \"fieldValues\": { \"nodes\": [] } } } }");
        using var httpClient = new HttpClient(handler);
        var syncer = new GitHubRoadmapSyncer(RoadmapProject.Load(workspace.RootPath), httpClient);

        // Act
        var result = await syncer.Apply(TestContext.Current.CancellationToken);

        // Assert
        result.Messages.ShouldContain("skipped projection of owner/repository#997 to GitHub Project 1 because Roadmap status cannot be projected from roadmap source");
        result.Messages.ShouldContain("drift: owner/repository#997 Project field Roadmap status cannot be projected from roadmap source");
        handler.Requests.ShouldNotContain(request => request.Body?.Contains("mutation", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task GitHub_roadmap_syncer_apply_reports_number_conflicts_without_overwriting_the_project_value()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        GitHubProjectSyncTestOperations.EnableProjectTarget(workspace);
        GitHubProjectSyncTestOperations.MapDefaultItem(workspace);
        using var handler = new TestHttpMessageHandler();
        GitHubProjectSyncTestOperations.EnqueueExistingProjectItem(
            handler,
            GitHubProjectSyncTestOperations.ProjectFieldsWithValidStatus("""{ "id": "order", "name": "Roadmap order", "dataType": "NUMBER" }"""),
            "{ \"data\": { \"node\": { \"fieldValues\": { \"nodes\": [{ \"number\": 11, \"field\": { \"id\": \"order\", \"name\": \"Roadmap order\" } }, { \"optionId\": \"ready\", \"field\": { \"id\": \"status\", \"name\": \"Roadmap status\" } }] } } } }");
        using var httpClient = new HttpClient(handler);
        var syncer = new GitHubRoadmapSyncer(RoadmapProject.Load(workspace.RootPath), httpClient);

        // Act
        var result = await syncer.Apply(TestContext.Current.CancellationToken);

        // Assert
        result.Messages.ShouldContain("drift: owner/repository#997 Project field Roadmap order cannot be projected from roadmap source");
        handler.Requests.ShouldNotContain(request => request.Body?.Contains("updateProjectV2ItemFieldValue", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task GitHub_roadmap_syncer_apply_tolerates_project_number_float_round_trips()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        GitHubProjectSyncTestOperations.EnableProjectTarget(workspace);
        GitHubProjectSyncTestOperations.MapDefaultItem(workspace);
        using var handler = new TestHttpMessageHandler();
        GitHubProjectSyncTestOperations.EnqueueExistingProjectItem(
            handler,
            GitHubProjectSyncTestOperations.ProjectFieldsWithValidStatus("""{ "id": "confidence", "name": "RICE confidence", "dataType": "NUMBER" }"""),
            "{ \"data\": { \"node\": { \"fieldValues\": { \"nodes\": [{ \"number\": 0.8000005, \"field\": { \"id\": \"confidence\", \"name\": \"RICE confidence\" } }, { \"optionId\": \"ready\", \"field\": { \"id\": \"status\", \"name\": \"Roadmap status\" } }] } } } }");
        using var httpClient = new HttpClient(handler);
        var syncer = new GitHubRoadmapSyncer(RoadmapProject.Load(workspace.RootPath), httpClient);

        // Act
        var result = await syncer.Apply(TestContext.Current.CancellationToken);

        // Assert
        result.Messages.ShouldNotContain("drift: owner/repository#997 Project field RICE confidence cannot be projected from roadmap source");
        handler.Requests.ShouldNotContain(request => request.Body?.Contains("updateProjectV2ItemFieldValue", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task GitHub_roadmap_syncer_apply_reports_status_conflicts_without_overwriting_the_project_value()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        GitHubProjectSyncTestOperations.EnableProjectTarget(workspace);
        GitHubProjectSyncTestOperations.MapDefaultItem(workspace);
        using var handler = new TestHttpMessageHandler();
        GitHubProjectSyncTestOperations.EnqueueExistingProjectItem(
            handler,
            GitHubProjectSyncTestOperations.ProjectFieldsWithValidStatus(string.Empty),
            "{ \"data\": { \"node\": { \"fieldValues\": { \"nodes\": [{ \"optionId\": \"done\", \"field\": { \"id\": \"status\", \"name\": \"Roadmap status\" } }] } } } }");
        using var httpClient = new HttpClient(handler);
        var syncer = new GitHubRoadmapSyncer(RoadmapProject.Load(workspace.RootPath), httpClient);

        // Act
        var result = await syncer.Apply(TestContext.Current.CancellationToken);

        // Assert
        result.Messages.ShouldContain("drift: owner/repository#997 Project field Roadmap status cannot be projected from roadmap source");
        handler.Requests.ShouldNotContain(request => request.Body?.Contains("updateProjectV2ItemFieldValue", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task GitHub_roadmap_syncer_apply_reports_text_conflicts_without_overwriting_the_project_value()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        GitHubProjectSyncTestOperations.EnableProjectTarget(workspace);
        GitHubProjectSyncTestOperations.MapDefaultItem(workspace);
        using var handler = new TestHttpMessageHandler();
        GitHubProjectSyncTestOperations.EnqueueExistingProjectItem(
            handler,
            GitHubProjectSyncTestOperations.ProjectFieldsWithValidStatus("""{ "id": "tags", "name": "Roadmap tags", "dataType": "TEXT" }"""),
            "{ \"data\": { \"node\": { \"fieldValues\": { \"nodes\": [{ \"text\": \"manual\", \"field\": { \"id\": \"tags\", \"name\": \"Roadmap tags\" } }, { \"optionId\": \"ready\", \"field\": { \"id\": \"status\", \"name\": \"Roadmap status\" } }] } } } }");
        using var httpClient = new HttpClient(handler);
        var syncer = new GitHubRoadmapSyncer(RoadmapProject.Load(workspace.RootPath), httpClient);

        // Act
        var result = await syncer.Apply(TestContext.Current.CancellationToken);

        // Assert
        result.Messages.ShouldContain("drift: owner/repository#997 Project field Roadmap tags cannot be projected from roadmap source");
        handler.Requests.ShouldNotContain(request => request.Body?.Contains("updateProjectV2ItemFieldValue", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task GitHub_roadmap_syncer_apply_reports_missing_status_options_without_mutating_the_project()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        GitHubProjectSyncTestOperations.EnableProjectTarget(workspace);
        GitHubProjectSyncTestOperations.MapDefaultItem(workspace);
        using var handler = new TestHttpMessageHandler();
        GitHubProjectSyncTestOperations.EnqueueExistingProjectItem(
            handler,
            "{ \"data\": { \"node\": { \"fields\": { \"nodes\": [{ \"id\": \"status\", \"name\": \"Roadmap status\", \"dataType\": \"SINGLE_SELECT\", \"options\": [{ \"id\": \"planned\", \"name\": \"planned\" }] }] } } } }",
            "{ \"data\": { \"node\": { \"fieldValues\": { \"nodes\": [] } } } }");
        using var httpClient = new HttpClient(handler);
        var syncer = new GitHubRoadmapSyncer(RoadmapProject.Load(workspace.RootPath), httpClient);

        // Act
        var result = await syncer.Apply(TestContext.Current.CancellationToken);

        // Assert
        result.Messages.ShouldContain("drift: owner/repository#997 Project field Roadmap status cannot be projected from roadmap source");
        handler.Requests.ShouldNotContain(request => request.Body?.Contains("updateProjectV2ItemFieldValue", StringComparison.Ordinal) == true);
    }

    [Theory]
    [InlineData("""
        { "data": { "node": { "fields": { "nodes": [] } } } }
        """)]
    [InlineData("""
        { "data": { "node": { "fields": { "nodes": [
          { "id": "status-a", "name": "Roadmap status", "dataType": "SINGLE_SELECT" },
          { "id": "status-b", "name": "Roadmap status", "dataType": "SINGLE_SELECT" }
        ] } } } }
        """)]
    [InlineData("""
        { "data": { "node": { "fields": { "nodes": [
          { "id": "", "name": "Roadmap status", "dataType": "SINGLE_SELECT" }
        ] } } } }
        """)]
    [InlineData("""
        { "data": { "node": { "fields": { "nodes": [
          { "id": "status", "name": "Roadmap status", "dataType": "TEXT" }
        ] } } } }
        """)]
    public async Task GitHub_roadmap_syncer_apply_blocks_invalid_required_status_schema_before_project_mutations(string projectFields)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        GitHubProjectSyncTestOperations.EnableProjectTarget(workspace);
        GitHubProjectSyncTestOperations.MapDefaultItem(workspace);
        using var handler = new TestHttpMessageHandler();
        GitHubProjectSyncTestOperations.EnqueueMissingProjectItem(
            handler,
            projectFields,
            "{ \"data\": { \"node\": { \"fieldValues\": { \"nodes\": [] } } } }");
        using var httpClient = new HttpClient(handler);
        var syncer = new GitHubRoadmapSyncer(RoadmapProject.Load(workspace.RootPath), httpClient);

        // Act
        var result = await syncer.Apply(TestContext.Current.CancellationToken);

        // Assert
        result.Messages.ShouldContain("skipped projection of owner/repository#997 to GitHub Project 1 because Roadmap status cannot be projected from roadmap source");
        result.Messages.ShouldContain("drift: owner/repository#997 Project field Roadmap status cannot be projected from roadmap source");
        handler.Requests.ShouldNotContain(request => request.Body?.Contains("mutation", StringComparison.Ordinal) == true);
    }

    [Theory]
    [InlineData("""
        [
          { "id": "ready-a", "name": "ready" },
          { "id": "ready-b", "name": "ready" }
        ]
        """)]
    [InlineData("""
        [
          { "id": "ready", "name": "ready" },
          { "id": "ready", "name": "legacy" }
        ]
        """)]
    public async Task GitHub_roadmap_syncer_apply_reports_ambiguous_status_options_without_mutating_the_project(string statusOptions)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        GitHubProjectSyncTestOperations.EnableProjectTarget(workspace);
        GitHubProjectSyncTestOperations.MapDefaultItem(workspace);
        var projectFields = $$"""
            { "data": { "node": { "fields": { "nodes": [
              { "id": "status", "name": "Roadmap status", "dataType": "SINGLE_SELECT", "options": {{statusOptions}} }
            ] } } } }
            """;
        using var handler = new TestHttpMessageHandler();
        GitHubProjectSyncTestOperations.EnqueueExistingProjectItem(
            handler,
            projectFields,
            "{ \"data\": { \"node\": { \"fieldValues\": { \"nodes\": [] } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"updateProjectV2ItemFieldValue\": { \"projectV2Item\": { \"id\": \"item-id\" } } } }");
        using var httpClient = new HttpClient(handler);
        var syncer = new GitHubRoadmapSyncer(RoadmapProject.Load(workspace.RootPath), httpClient);

        // Act
        var result = await syncer.Apply(TestContext.Current.CancellationToken);

        // Assert
        result.Messages.ShouldContain("drift: owner/repository#997 Project field Roadmap status cannot be projected from roadmap source");
        handler.Requests.ShouldNotContain(request => request.Body?.Contains("updateProjectV2ItemFieldValue", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task GitHub_roadmap_syncer_apply_prevalidates_noncurrent_status_options_before_project_mutations()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        GitHubProjectSyncTestOperations.EnableProjectTarget(workspace);
        GitHubProjectSyncTestOperations.MapDefaultItem(workspace);
        var statusOptions = """
            [
              { "id": "proposed", "name": "proposed" },
              { "id": "ready", "name": "ready" },
              { "id": "in-progress", "name": "in_progress" },
              { "id": "done", "name": "done" },
              { "id": "done", "name": "legacy" },
              { "id": "dropped", "name": "dropped" }
            ]
            """;
        using var handler = new TestHttpMessageHandler();
        GitHubProjectSyncTestOperations.EnqueueMissingProjectItem(
            handler,
            GitHubProjectSyncTestOperations.CompleteProjectFields(statusOptions),
            "{ \"data\": { \"node\": { \"fieldValues\": { \"nodes\": [] } } } }");
        for (var updateIndex = 0; updateIndex < 10; updateIndex++)
        {
            handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"updateProjectV2ItemFieldValue\": { \"projectV2Item\": { \"id\": \"item-id\" } } } }");
        }

        using var httpClient = new HttpClient(handler);
        var syncer = new GitHubRoadmapSyncer(RoadmapProject.Load(workspace.RootPath), httpClient);

        // Act
        var result = await syncer.Apply(TestContext.Current.CancellationToken);

        // Assert
        result.Messages.ShouldContain("skipped projection of owner/repository#997 to GitHub Project 1 because Roadmap status cannot be projected from roadmap source");
        result.Messages.ShouldContain("drift: owner/repository#997 Project field Roadmap status cannot be projected from roadmap source");
        handler.Requests.ShouldNotContain(request => request.Body?.Contains("mutation", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task GitHub_roadmap_syncer_apply_continues_compatible_field_mutations_when_nonstatus_fields_are_missing()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        GitHubProjectSyncTestOperations.EnableProjectTarget(workspace);
        GitHubProjectSyncTestOperations.MapDefaultItem(workspace);
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"id\": \"project-id\", \"number\": 1, \"owner\": { \"login\": \"owner\" } } } }");
        handler.EnqueueJson(HttpStatusCode.NotFound, "{}");
        handler.EnqueueJson(HttpStatusCode.OK, GitHubSyncTestResponseFactory.IssueMetadata(997));
        handler.EnqueueJson(HttpStatusCode.OK, "{}");
        handler.EnqueueJson(HttpStatusCode.OK, """
            { "data": { "node": { "fields": { "nodes": [
              { "id": "order", "name": "Roadmap order", "dataType": "NUMBER" },
              { "id": "status", "name": "Roadmap status", "dataType": "SINGLE_SELECT", "options": [
                { "id": "proposed", "name": "proposed" },
                { "id": "ready", "name": "ready" },
                { "id": "in-progress", "name": "in_progress" },
                { "id": "done", "name": "done" },
                { "id": "dropped", "name": "dropped" }
              ] }
            ] } } } }
            """);
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"repository\": { \"issue\": { \"id\": \"issue-id\" } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"items\": { \"nodes\": [], \"pageInfo\": { \"hasNextPage\": false, \"endCursor\": null } } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"addProjectV2ItemById\": { \"item\": { \"id\": \"item-id\" } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"fieldValues\": { \"nodes\": [] } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"updateProjectV2ItemFieldValue\": { \"projectV2Item\": { \"id\": \"item-id\" } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"updateProjectV2ItemFieldValue\": { \"projectV2Item\": { \"id\": \"item-id\" } } } }");
        using var httpClient = new HttpClient(handler);
        var syncer = new GitHubRoadmapSyncer(RoadmapProject.Load(workspace.RootPath), httpClient);

        // Act
        var result = await syncer.Apply(TestContext.Current.CancellationToken);

        // Assert
        result.Messages.ShouldContain("projected owner/repository#997 to GitHub Project 1");
        result.Messages.ShouldContain("drift: owner/repository#997 Project field RICE reach cannot be projected from roadmap source");
        handler.Requests.ShouldContain(request =>
            request.Body?.Contains(
                "addProjectV2ItemById",
                StringComparison.Ordinal) == true);
        handler.Requests.Count(request => request.Body?.Contains("updateProjectV2ItemFieldValue", StringComparison.Ordinal) == true).ShouldBe(2);
    }

    [Fact]
    public async Task GitHub_roadmap_syncer_apply_skips_numeric_project_fields_for_untriaged_items()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        GitHubProjectSyncTestOperations.EnableProjectTarget(workspace);
        workspace.WriteFile("roadmap/items/RM-002-untriaged.json", RoadmapTestContent.UntriagedIssueJson);
        using var handler = new TestHttpMessageHandler();
        GitHubProjectSyncTestOperations.EnqueueMissingProjectItem(
            handler,
            GitHubProjectSyncTestOperations.CompleteProjectFields("""
                [
                  { "id": "proposed", "name": "proposed" },
                  { "id": "ready", "name": "ready" },
                  { "id": "in-progress", "name": "in_progress" },
                  { "id": "done", "name": "done" },
                  { "id": "dropped", "name": "dropped" }
                ]
                """),
            """
            { "data": { "node": { "fieldValues": { "nodes": [
              { "number": 10, "field": { "id": "order", "name": "Roadmap order" } },
              { "number": 10, "field": { "id": "reach", "name": "RICE reach" } },
              { "number": 3, "field": { "id": "impact", "name": "RICE impact" } },
              { "number": 0.8, "field": { "id": "confidence", "name": "RICE confidence" } },
              { "number": 2, "field": { "id": "effort", "name": "RICE effort" } },
              { "number": 12, "field": { "id": "score", "name": "RICE score" } }
            ] } } } }
            """);
        for (var updateIndex = 0; updateIndex < 4; updateIndex++)
        {
            handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"updateProjectV2ItemFieldValue\": { \"projectV2Item\": { \"id\": \"item-id\" } } } }");
        }

        using var httpClient = new HttpClient(handler);
        var syncer = new GitHubRoadmapSyncer(RoadmapProject.Load(workspace.RootPath), httpClient);

        // Act
        var result = await syncer.Apply(TestContext.Current.CancellationToken);
        var projectFieldUpdates = handler.Requests
            .Where(request => request.Body?.Contains("updateProjectV2ItemFieldValue", StringComparison.Ordinal) == true)
            .ToArray();

        // Assert
        result.Messages.ShouldContain("projected owner/repository#997 to GitHub Project 1");
        projectFieldUpdates.Length.ShouldBe(4);
        projectFieldUpdates.ShouldNotContain(request => request.Body?.Contains("\"fieldId\":\"order\"", StringComparison.Ordinal) == true);
        projectFieldUpdates.ShouldNotContain(request => request.Body?.Contains("\"fieldId\":\"reach\"", StringComparison.Ordinal) == true);
        projectFieldUpdates.ShouldNotContain(request => request.Body?.Contains("\"fieldId\":\"impact\"", StringComparison.Ordinal) == true);
        projectFieldUpdates.ShouldNotContain(request => request.Body?.Contains("\"fieldId\":\"confidence\"", StringComparison.Ordinal) == true);
        projectFieldUpdates.ShouldNotContain(request => request.Body?.Contains("\"fieldId\":\"effort\"", StringComparison.Ordinal) == true);
        projectFieldUpdates.ShouldNotContain(request => request.Body?.Contains("\"fieldId\":\"score\"", StringComparison.Ordinal) == true);
        handler.Requests.ShouldNotContain(request => request.Body?.Contains("clearProjectV2ItemFieldValue", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task GitHub_sync_preview_skips_numeric_project_fields_for_untriaged_items()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        using var syncOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var syncError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        GitHubProjectSyncTestOperations.EnableProjectTarget(workspace);
        workspace.WriteFile("roadmap/items/RM-002-untriaged.json", RoadmapTestContent.UntriagedIssueJson);
        using var handler = new TestHttpMessageHandler();
        GitHubProjectSyncTestOperations.EnqueueExistingProjectPreflight(
            handler,
            GitHubProjectSyncTestOperations.CompleteProjectFields("""
                [
                  { "id": "proposed", "name": "proposed" },
                  { "id": "ready", "name": "ready" },
                  { "id": "in-progress", "name": "in_progress" },
                  { "id": "done", "name": "done" },
                  { "id": "dropped", "name": "dropped" }
                ]
                """),
            "{ \"data\": { \"node\": { \"fieldValues\": { \"nodes\": [] } } } }");
        using var httpClient = new HttpClient(handler);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["github", "sync", "--root", workspace.RootPath], syncOutput, syncError, workspace.RootPath, httpClient, TestContext.Current.CancellationToken);
        var outputText = syncOutput.ToString();

        // Assert
        exitCode.ShouldBe(0);
        outputText.ShouldContain("preview: set owner/repository#997 Project field Roadmap status to ready", StringComparison.Ordinal);
        outputText.ShouldNotContain("Roadmap order", StringComparison.Ordinal);
        outputText.ShouldNotContain("RICE", StringComparison.Ordinal);
        handler.Requests.ShouldNotContain(request => request.Body?.Contains("mutation", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task GitHub_roadmap_syncer_apply_reports_incompatible_project_field_types_without_mutating_them()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        GitHubProjectSyncTestOperations.EnableProjectTarget(workspace);
        GitHubProjectSyncTestOperations.MapDefaultItem(workspace);
        using var handler = new TestHttpMessageHandler();
        GitHubProjectSyncTestOperations.EnqueueExistingProjectItem(
            handler,
            GitHubProjectSyncTestOperations.ProjectFieldsWithValidStatus("""{ "id": "order", "name": "Roadmap order", "dataType": "TEXT" }"""),
            "{ \"data\": { \"node\": { \"fieldValues\": { \"nodes\": [{ \"optionId\": \"ready\", \"field\": { \"id\": \"status\", \"name\": \"Roadmap status\" } }] } } } }");
        using var httpClient = new HttpClient(handler);
        var syncer = new GitHubRoadmapSyncer(RoadmapProject.Load(workspace.RootPath), httpClient);

        // Act
        var result = await syncer.Apply(TestContext.Current.CancellationToken);

        // Assert
        result.Messages.ShouldContain("drift: owner/repository#997 Project field Roadmap order cannot be projected from roadmap source");
        handler.Requests.ShouldNotContain(request => request.Body?.Contains("updateProjectV2ItemFieldValue", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task GitHub_roadmap_syncer_apply_rejects_held_issue_creation_locks_without_http_requests()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace("\"labels\": [", "\"integrations\": { \"github\": { \"issue\": \"create\" } },\n  \"labels\": [", StringComparison.Ordinal));
        var lockPath = Path.Combine(workspace.RootPath, "roadmap/items/RM-001-roadmap-gitops.json.lock");
        await File.WriteAllTextAsync(lockPath, string.Empty, TestContext.Current.CancellationToken);
        using var handler = new TestHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var syncer = new GitHubRoadmapSyncer(RoadmapProject.Load(workspace.RootPath), httpClient);

        // Act
        Func<Task> action = () => syncer.Apply(TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("GitHub issue creation is already in progress for RM-001.", StringComparison.Ordinal);
        exception.Message.ShouldContain(lockPath, StringComparison.Ordinal);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task GitHub_roadmap_syncer_apply_aborts_without_http_when_create_intent_changes()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var initOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var initError = new StringWriter(CultureInfo.InvariantCulture);
        var actualResult = await RepoConfigToolRunner.Run(["init", "--root", workspace.RootPath], initOutput, initError, workspace.RootPath, TestContext.Current.CancellationToken);
        actualResult.ShouldBe(0);
        RoadmapConfigTestOperations.EnableGitHubSync(workspace);
        var itemText = workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json");
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", itemText.Replace("\"labels\": [", "\"integrations\": { \"github\": { \"issue\": \"create\" } },\n  \"labels\": [", StringComparison.Ordinal));
        var project = RoadmapProject.Load(workspace.RootPath);
        workspace.WriteFile("roadmap/items/RM-001-roadmap-gitops.json", workspace.ReadFile("roadmap/items/RM-001-roadmap-gitops.json").Replace("\"issue\": \"create\"", "\"issue\": \"do-not-create\"", StringComparison.Ordinal));
        using var handler = new TestHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var syncer = new GitHubRoadmapSyncer(project, httpClient);

        // Act
        Func<Task> action = () => syncer.Apply(TestContext.Current.CancellationToken);
        var exception = await action.ShouldThrow<InvalidOperationException>();

        // Assert
        exception.Message.ShouldContain("GitHub issue mapping changed while synchronizing", StringComparison.Ordinal);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Get_by_label_rejects_unquoted_label_with_spaces()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var getOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var getError = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["get", "by-label", "area:", "tooling", "--root", workspace.RootPath], getOutput, getError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = getError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("Unrecognized command or argument 'tooling'.", StringComparison.Ordinal);
        errorText.ShouldNotContain("sharedkernel-repo:", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_reports_tool_name_for_config_errors()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var getOutput = new StringWriter(CultureInfo.InvariantCulture);
        using var getError = new StringWriter(CultureInfo.InvariantCulture);

        // Act
        var exitCode = await RepoConfigToolRunner.Run(["get", "next-priority", "--root", workspace.RootPath], getOutput, getError, workspace.RootPath, TestContext.Current.CancellationToken);
        var errorText = getError.ToString();

        // Assert
        exitCode.ShouldBe(1);
        errorText.ShouldContain("sharedkernel-repo: Roadmap config is invalid.", StringComparison.Ordinal);
    }
}
