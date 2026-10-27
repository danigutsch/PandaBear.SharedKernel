using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Time.Testing;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Runs the repo-config tool end to end in temporary workspaces, covering init, verify, get, set and the GitHub sync, import and project integration.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
[Collection(RepoConfigToolEnvironmentGroup.Name)]
public sealed class RepoConfigToolApplicationTests
{
    /// <summary>Verifying an empty workspace exits 1 and reports the missing roadmap directory and roadmap/config.json.</summary>
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

    /// <summary>After init, verify exits 0 with "Repository config is valid." and the config and first roadmap item files exist.</summary>
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

    /// <summary>Init writes config and item JSON schemas that declare the 2020-12 draft, contain no github.com links and include required, integrations, size, uniqueItems and a 0.1 minimum.</summary>
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

    /// <summary>The roadmap README written by init tells readers to run "sharedkernel-repo verify" and does not use a "dotnet run --project" command.</summary>
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

    /// <summary>With an untriaged RM-002 added next to RM-001, verify passes and get next-priority lists RM-001 but not RM-002.</summary>
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

    /// <summary>A title containing a newline and an escape character is printed by get next-priority as one line with the escapes rendered as \u000A and \u001B.</summary>
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

    /// <summary>An itemIdPrefix containing a newline and an escape character makes verify exit 1 with the allowed-characters message, writing no raw escape character or newline to standard error.</summary>
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

    /// <summary>An item id containing a newline and an escape character is shown by get blocking-overview on one line as "RM-002\u000A\u001B blocked by RM-001".</summary>
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

    /// <summary>Adding an order or scoring input to an untriaged item makes verify exit 1 with "Untriaged roadmap items must not define priority inputs."</summary>
    /// <param name="priorityInput">The JSON property text inserted before the item's theme.</param>
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

    /// <summary>Listing the untriaged RM-002 in order.json makes verify exit 1 with "order.json must not contain untriaged item: RM-002."</summary>
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

    /// <summary>When untriaged RM-002 blocks RM-001, get blockers-of RM-001 prints "RM-002 | issue | ready | untriaged" without order or score values.</summary>
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

    /// <summary>The $schema reference in the config and the first item written by init each resolve to an existing schema file on disk.</summary>
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

    /// <summary>Running set github.repository example/repository exits 0 and writes that repository value into roadmap/config.json.</summary>
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

    /// <summary>After set github.repository, verify still passes and the config keeps enabled false and sourceOfTruth "projection" next to the new repository.</summary>
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

    /// <summary>Adding a projectFieldMapping object to the GitHub integration makes verify exit 1 with "integrations.github.projectFieldMapping is not supported."</summary>
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

    /// <summary>Setting sourceOfTruth to "manual" makes verify exit 1 with "integrations.github.sourceOfTruth must be projection."</summary>
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

    /// <summary>Removing sourceOfTruth from the GitHub integration still passes verify, with exit 0 and empty standard error.</summary>
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

    /// <summary>Setting github.repository to a value with a space, query, fragment, backslash or double hyphen exits 1 with "github.repository must be shaped as owner/repository." and leaves the value out of the config.</summary>
    /// <param name="repository">The invalid repository value passed to set.</param>
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

    /// <summary>An item that depends on the nonexistent RM-404 makes verify exit 1 with "Unknown dependency: RM-404."</summary>
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

    /// <summary>Copying RM-001 to a second file makes verify exit 1 with "Duplicate roadmap item id: RM-001." as a diagnostic rather than a tool-level error.</summary>
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

    /// <summary>A numeric parent value (7) makes verify exit 1 with "parent must be a string when present."</summary>
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

    /// <summary>A whitespace-only parent value makes verify exit 1 with "parent must not be blank when present."</summary>
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

    /// <summary>Setting RM-001's parent to RM-001 makes verify exit 1 with "Roadmap item cannot reference itself as parent."</summary>
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

    /// <summary>When RM-001's parent is RM-002 and RM-002 loops back to RM-001, verify exits 1 with "parent cycle includes RM-001."</summary>
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

    /// <summary>A GitHub mapping with a subIssues list makes verify exit 1 with "integrations.github.subIssues is not supported; use parent links."</summary>
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

    /// <summary>A scoring confidence of 0.09 makes verify exit 1 with "confidence must be between 0.1 and 1.0."</summary>
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

    /// <summary>Removing itemIdPrefix from roadmap/config.json makes verify exit 1 with "Missing required string property: itemIdPrefix."</summary>
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

    /// <summary>Cutting the integrations object out of roadmap/config.json makes verify exit 1 with "Missing required object property: integrations."</summary>
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

    /// <summary>Duplicating "done" in project.closedStatuses makes verify exit 1 with "project.closedStatuses contains a duplicate value: done."</summary>
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

    /// <summary>A number (7) inside project.closedStatuses makes verify exit 1 with "project.closedStatuses must contain only non-empty strings."</summary>
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

    /// <summary>A closedStatuses entry "done " with a trailing space makes verify exit 1 with "project.closedStatuses must not contain leading or trailing whitespace."</summary>
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

    /// <summary>Adding a tagFields array to the project config makes verify exit 1 with "project.tagFields is not supported."</summary>
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

    /// <summary>The label "area: tooling " with a trailing space makes verify exit 1 with "labels must not contain leading or trailing whitespace."</summary>
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

    /// <summary>An item whose integrations value is an array, or whose github entry is an array, makes verify exit 1 with the matching "must be a JSON object when present." message.</summary>
    /// <param name="integrations">The JSON text inserted as the item's integrations property.</param>
    /// <param name="expectedMessage">The error text standard error must contain.</param>
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

    /// <summary>An item with an empty id and an array integrations value reports both "Missing required string property: id." and the integrations object error.</summary>
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

    /// <summary>With GitHub sync enabled, a repository value with a space, query, fragment, backslash or double hyphen makes verify exit 1 with "integrations.github.repository must be shaped as owner/repository when GitHub sync is enabled."</summary>
    /// <param name="repository">The invalid repository value written into the config.</param>
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

    /// <summary>With GitHub sync enabled, a projectV2 target that is not an object or has a missing or wrong-typed id, an invalid owner, or a non-positive or non-integer number makes verify exit 1 with the matching projectV2 message.</summary>
    /// <param name="projectV2">The JSON text written as the projectV2 property.</param>
    /// <param name="expectedError">The error text standard error must contain.</param>
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

    /// <summary>GitHubRepositoryName.IsValid accepts "owner-name/repository", an owner with a single hyphen.</summary>
    [Fact]
    public void GitHub_repository_name_accepts_single_hyphenated_owner()
    {
        // Act
        var isValid = GitHubRepositoryName.IsValid("owner-name/repository");

        // Assert
        isValid.ShouldBeTrue();
    }

    /// <summary>A dependencies list naming RM-404 twice reports the duplicate value and reports "Unknown dependency: RM-404." only once.</summary>
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

    /// <summary>A theme file containing a JSON array makes verify exit 1 with "Theme root must be a JSON object." for roadmap/themes/repo-operations.json.</summary>
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

    /// <summary>Deleting the repo-operations theme file makes verify exit 1 with "Unknown theme: repo-operations." for the item that uses it.</summary>
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

    /// <summary>When epic RM-001 blocks issue RM-002, get next-unblocked lists RM-001 and omits RM-002.</summary>
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

    /// <summary>Get next-work lists RM-001, which blocks open RM-002, before the better-ordered enabler RM-003, and omits blocked RM-002.</summary>
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

    /// <summary>When RM-001 only blocks the done item RM-002, get next-work lists the better-ordered enabler RM-003 before RM-001.</summary>
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

    /// <summary>When RM-001 and RM-003 each unblock open work, get next-work lists RM-003, which has the better order, before RM-001.</summary>
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

    /// <summary>Get blockers-of RM-002 lists its blocker, epic RM-001.</summary>
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

    /// <summary>Get blockers-of RM-999 exits 1 with empty standard output and exactly "sharedkernel-repo: Unknown roadmap item id: RM-999" on standard error.</summary>
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

    /// <summary>When the output writer throws cancellation although the caller's token is not cancelled, github sync exits 1 with "sharedkernel-repo: The operation was canceled." on standard error.</summary>
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

    /// <summary>When the caller's token is already cancelled, github sync exits 1 and writes nothing to standard error.</summary>
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

    /// <summary>When the caller cancels while the first project-target HTTP request is answered, github sync exits 1 and writes nothing to standard output or error.</summary>
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

    /// <summary>When the caller cancels while the project fields response is being read, github sync preview exits 1 and writes nothing to standard output or error.</summary>
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

    /// <summary>When the caller cancels while the project fields response is being read, GitHubRoadmapSyncer.Apply throws an OperationCanceledException carrying the caller's token.</summary>
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

    /// <summary>With RM-001 mapped to issue 997 and no HTTP client supplied, github sync exits 0 and prints "preview: sync labels for owner/repository#997 from RM-001".</summary>
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

    /// <summary>With GH_TOKEN and GITHUB_TOKEN unset, GitHubRoadmapSyncer.Apply throws InvalidOperationException saying to set GH_TOKEN or GITHUB_TOKEN before authenticated GitHub sync.</summary>
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

    /// <summary>With a valid intake configuration but GH_TOKEN and GITHUB_TOKEN unset, github import exits 1 with empty standard output and the message to set a token before authenticated GitHub import.</summary>
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

    /// <summary>When issue 997 is already in project 1 with no field values, github sync previews that membership and setting the Roadmap order field to 10 without sending any GraphQL mutation.</summary>
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

    /// <summary>When issue 997 is not yet in project 1, github sync previews adding it and setting Roadmap order to 10 "after adding it", using four requests and no mutation.</summary>
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

    /// <summary>When a status option other than the current one is ambiguous (two options share the id "done"), github sync previews skipping the projection of issue 997 because Roadmap status cannot be projected, using two requests and no mutation.</summary>
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

    /// <summary>When the remote project reports number 2 instead of the configured 1, github sync exits 1 with "Configured GitHub Project target could not be verified." after a single request and no mutation.</summary>
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

    /// <summary>When the project's Roadmap order field already holds 11, github sync exits 0 and prints a drift line saying the field cannot be projected from the roadmap source, sending no mutation.</summary>
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

    /// <summary>When the HTTP transport throws a request exception whose message is a token, github sync exits 1 with "GitHub sync request failed." and does not print the token.</summary>
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

    /// <summary>With a project target but no issue mappings, github sync reports that no items are mapped and still previews that the Roadmap order field cannot be projected, using two requests and no mutation.</summary>
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

    /// <summary>With an item set to create its issue and a project target, github sync previews the issue creation and the unprojectable Roadmap order field, using two requests and no mutation.</summary>
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

    /// <summary>When the Roadmap status field has null, empty, blank-id, duplicate-name or shared-id options, github sync exits 0 and previews that the Roadmap status field cannot be projected, sending no mutation.</summary>
    /// <param name="statusOptions">The JSON options array returned for the Roadmap status field.</param>
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

    /// <summary>An order.json listing only RM-404 makes verify exit 1 with "Unknown ordered item: RM-404." and "Missing ordered item: RM-001."</summary>
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

    /// <summary>With the GitHub integration disabled by init, github sync exits 1 with "GitHub sync is disabled in roadmap/config.json."</summary>
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

    /// <summary>Removing the enabled property from the GitHub integration makes github sync exit 1 with "GitHub sync is disabled in roadmap/config.json."</summary>
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

    /// <summary>Two items both mapped to GitHub issue 997 make verify exit 1 with "Duplicate GitHub issue mapping: 997."</summary>
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

    /// <summary>An item whose theme is changed to "missing-theme" makes verify exit 1 with "Unknown theme: missing-theme."</summary>
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

    /// <summary>An item with its theme property removed reports "Missing required string property: theme." and no "Unknown theme:" follow-up error.</summary>
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

    /// <summary>An order.json holding a JSON array makes verify exit 1 with "order.json root must be a JSON object." as a diagnostic, not a tool-level error.</summary>
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

    /// <summary>Truncated JSON in config, item, theme and order files makes verify exit 1 with an "Invalid JSON:" diagnostic prefixed by each file's path, not a tool-level error.</summary>
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

    /// <summary>An order.json that lists lower-priority RM-001 before higher-priority RM-002 makes verify exit 1 with "order.json items must match priority order: order ascending, score descending, then id."</summary>
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

    /// <summary>When RM-001 and RM-000 have equal order but RM-000 has lower reach, an order.json listing RM-001 first passes verify with exit 0.</summary>
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

    /// <summary>When RM-001 and RM-002 each list the other as a blocker, verify exits 1 with a "blockedBy cycle includes" diagnostic.</summary>
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

    /// <summary>Get next-priority with limit 3 lists RM-001 first, then RM-002 with the same score, then RM-000 with a lower score, matching the order file.</summary>
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

    /// <summary>Of two otherwise identical blocker items, get next-blockers with limit 2 puts RM-002 before RM-003 even though RM-003's file sorts first.</summary>
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

    /// <summary>Of two otherwise identical issue items, get low-hanging-fruit for type issue puts RM-002 before RM-003 even though RM-003's file sorts first.</summary>
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

    /// <summary>Among equal-score, equal-effort issues, get pareto with limit 2 returns RM-003 then RM-006, which share the lowest order, and leaves out RM-002 with order 20.</summary>
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

    /// <summary>With RM-001 blocking RM-003 through RM-006 and a limit of 10, get pareto lists the unblocked RM-002 and leaves out RM-001.</summary>
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

    /// <summary>Preview of two mapped items lists issue 997 from RM-001 before issue 998 from RM-000, following the order file rather than id.</summary>
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

    /// <summary>Apply first checks that issue 997 is not a pull request (404 on the pulls path), then reads the issue and posts the "area: tooling" label, without any PATCH request.</summary>
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

    /// <summary>Preview of an item whose issue is "create" reports "preview: create GitHub issue for owner/repository from RM-001" and sends no HTTP request.</summary>
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

    /// <summary>When GitHub has the extra label "manual" and a case-different "AREA: TOOLING", apply reports drift only for "manual" and sends no DELETE, PATCH or PUT request.</summary>
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

    /// <summary>Apply posts a new issue titled from RM-001 when its issue is "create", reports "created owner/repository#1200 from RM-001" and writes issue 1200 back into the item file.</summary>
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

    /// <summary>When issue creation exceeds the 30 second limit, apply throws TimeoutException, keeps the "create" intent and the .lock file, and a second sync throws InvalidOperationException without any HTTP request.</summary>
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

    /// <summary>When the item file changes from "create" to issue 1200 after the project was loaded, apply creates no issue and syncs labels on issue 1200 instead.</summary>
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

    /// <summary>When the item file is changed to issue 1300 while GitHub creates issue 1200, apply throws InvalidOperationException saying the mapping changed, keeps 1300 in the file and leaves the .lock file.</summary>
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

    /// <summary>When GitHub answers issue creation with HTTP 422, apply throws "GitHub issue creation failed: HTTP 422 (request validation failed)." after one request and keeps "create" in the item file.</summary>
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

    /// <summary>When GitHub answers issue creation with HTTP 500, apply throws "GitHub issue creation failed: HTTP 500.", leaves the .lock file, and a second sync throws InvalidOperationException without any HTTP request.</summary>
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

    /// <summary>With a projectV2 target configured and the item already in the project, preview reports "preview: ensure owner/repository#997 is in GitHub Project 1".</summary>
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

    /// <summary>GitHubProjectClient.GetFieldValues throws "GitHub Project request failed (FORBIDDEN)." when the GraphQL response carries a FORBIDDEN error.</summary>
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

    /// <summary>FindItemId follows the page cursor of the first response and returns "target-item" from the second page, using exactly two requests.</summary>
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

    /// <summary>Three FindItemId lookups against a two-page project return item-1, item-2 and null while reading the pages only once, with two requests in total.</summary>
    [Fact]
    public async Task GitHub_project_client_pages_item_membership_once_for_many_lookups()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"items\": { \"nodes\": [{ \"id\": \"item-1\", \"content\": { \"id\": \"issue-1\" } }], \"pageInfo\": { \"hasNextPage\": true, \"endCursor\": \"cursor-1\" } } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"items\": { \"nodes\": [{ \"id\": \"item-2\", \"content\": { \"id\": \"issue-2\" } }], \"pageInfo\": { \"hasNextPage\": false, \"endCursor\": null } } } } }");
        using var httpClient = new HttpClient(handler);
        var client = new GitHubProjectClient(httpClient);
        var target = new GitHubProjectTarget("project-id", "owner", 1);

        // Act
        var first = await client.FindItemId(target, "issue-1", TestContext.Current.CancellationToken);
        var second = await client.FindItemId(target, "issue-2", TestContext.Current.CancellationToken);
        var missing = await client.FindItemId(target, "issue-3", TestContext.Current.CancellationToken);

        // Assert
        first.ShouldBe("item-1");
        second.ShouldBe("item-2");
        missing.ShouldBeNull();
        handler.Requests.Count.ShouldBe(2);
    }

    /// <summary>After AddIssue adds issue-1, FindItemId returns the new item id "new-item" from memory instead of reading the project again, using two requests in total.</summary>
    [Fact]
    public async Task GitHub_project_client_remembers_an_added_item_for_later_lookups()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"items\": { \"nodes\": [], \"pageInfo\": { \"hasNextPage\": false, \"endCursor\": null } } } } }");
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"addProjectV2ItemById\": { \"item\": { \"id\": \"new-item\" } } } }");
        using var httpClient = new HttpClient(handler);
        var client = new GitHubProjectClient(httpClient);
        var target = new GitHubProjectTarget("project-id", "owner", 1);

        // Act
        var before = await client.FindItemId(target, "issue-1", TestContext.Current.CancellationToken);
        before.ShouldBeNull();
        await client.AddIssue(target, "issue-1", TestContext.Current.CancellationToken);
        var itemId = await client.FindItemId(target, "issue-1", TestContext.Current.CancellationToken);

        // Assert
        itemId.ShouldBe("new-item");
        handler.Requests.Count.ShouldBe(2);
    }

    /// <summary>Calling GetFields twice for the same target returns the same cached instance and sends only one request.</summary>
    [Fact]
    public async Task GitHub_project_client_reads_field_definitions_once_per_target()
    {
        // Arrange
        using var handler = new TestHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "{ \"data\": { \"node\": { \"fields\": { \"nodes\": [{ \"id\": \"field-1\", \"name\": \"Roadmap score\", \"dataType\": \"NUMBER\" }] } } } }");
        using var httpClient = new HttpClient(handler);
        var client = new GitHubProjectClient(httpClient);
        var target = new GitHubProjectTarget("project-id", "owner", 1);

        // Act
        var first = await client.GetFields(target, TestContext.Current.CancellationToken);
        var second = await client.GetFields(target, TestContext.Current.CancellationToken);

        // Assert
        second.ShouldBeSameAs(first);
        handler.Requests.Count.ShouldBe(1);
    }

    /// <summary>VerifyTarget accepts a target owner "OWNER" when the remote project reports the login "owner".</summary>
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

    /// <summary>GitHubProjectClient.GetFields throws "GitHub Project request failed (FORBIDDEN)." when the GraphQL response carries a FORBIDDEN error.</summary>
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

    /// <summary>FindItemId returns null when the project's only page has no items.</summary>
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

    /// <summary>VerifyTarget throws "Configured GitHub Project target could not be verified." when the remote project number is 2 but the target says 1.</summary>
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

    /// <summary>GetIssueNodeId throws "GitHub issue mapping could not be found: #1." when the response issue has no id.</summary>
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

    /// <summary>GetFields throws "GitHub Project request failed with HTTP 403." when the server answers 403 Forbidden.</summary>
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

    /// <summary>GetFieldValues throws "GitHub Project field values could not be read." when the response node is empty.</summary>
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

    /// <summary>FindItemId throws "GitHub Project items could not be read." when the response node has no items.</summary>
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

    /// <summary>With a projectV2 target configured, apply syncs labels on issue 997, adds it to the project, sets the project fields including the single-select status, and reports "projected owner/repository#997 to GitHub Project 1".</summary>
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

    /// <summary>A mapped item with an empty labels list is reported as "skipped owner/repository#997 from RM-001 because it has no labels", after two GET requests and no POST.</summary>
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

    /// <summary>With two mapped items whose HTTP calls each advance the fake clock, apply finishes and returns two messages, so the timeout restarts per item.</summary>
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

    /// <summary>When the HTTP call for a mapped item runs past the fake clock's limit, apply throws TimeoutException with "GitHub sync timed out after 30 seconds."</summary>
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

    /// <summary>A failing pull request check for issue 997 (401, 403, 422, 429 or 502) throws InvalidOperationException with a fixed status hint and none of the token strings from the reason, body or headers, after one GET.</summary>
    /// <param name="statusCode">The HTTP status the pull request check returns.</param>
    /// <param name="expectedMessage">The exact exception message expected for that status.</param>
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

    /// <summary>When the label POST fails with HTTP 422, apply throws "GitHub label sync failed for #997: HTTP 422 (request validation failed)." without the token strings from the response reason, body or headers.</summary>
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

    /// <summary>When the pulls path for issue 997 returns 200, apply throws InvalidOperationException saying the mapping "points to a pull request" after a single GET.</summary>
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

    /// <summary>With no item mapped or set to create, apply returns the single message "No roadmap items have GitHub issue mappings or explicit creation requests." and sends no request.</summary>
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

    /// <summary>When the project has no fields, apply reports the projection of issue 997 as skipped and the Roadmap status field as drift, and sends no GraphQL mutation.</summary>
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

    /// <summary>When the project's Roadmap order number field already holds 11, apply reports drift for that field and sends no field-value update.</summary>
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

    /// <summary>A project RICE confidence value of 0.8000005, a float round-trip of 0.8, produces no drift message and no field-value update.</summary>
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

    /// <summary>When the project's Roadmap status is already "done" while the roadmap says otherwise, apply reports drift for that field and sends no field-value update.</summary>
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

    /// <summary>When the project's Roadmap tags text field already holds "manual", apply reports drift for that field and sends no field-value update.</summary>
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

    /// <summary>When the Roadmap status field offers only a "planned" option, apply reports drift for the status field and sends no field-value update.</summary>
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

    /// <summary>When the Roadmap status field is absent, duplicated, has an empty id or is a text field, apply reports the projection as skipped and the status field as drift, and sends no GraphQL mutation.</summary>
    /// <param name="projectFields">The GraphQL response describing the project's fields.</param>
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

    /// <summary>When two status options share the name "ready" or one id covers two names, apply reports drift for Roadmap status and sends no field-value update.</summary>
    /// <param name="statusOptions">The JSON options array of the Roadmap status field.</param>
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

    /// <summary>When a status option other than the current one is ambiguous (the id "done" is used twice), apply skips the projection and reports status drift before sending any mutation, even with update responses queued.</summary>
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

    /// <summary>When the project defines only the order and status fields, apply still projects issue 997, sends exactly two field-value updates and reports the missing RICE reach field as drift.</summary>
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

    /// <summary>With an untriaged item present, apply sends exactly four field-value updates and none for the order, reach, impact, confidence, effort or score fields, and sends no clear mutation.</summary>
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

    /// <summary>With an untriaged item present, github sync previews setting only the Roadmap status to ready and prints nothing about Roadmap order or RICE fields, sending no mutation.</summary>
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

    /// <summary>When the Roadmap order field is a TEXT field instead of NUMBER, apply reports drift for it and sends no field-value update.</summary>
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

    /// <summary>When the .lock file for an item set to create already exists, apply throws InvalidOperationException naming RM-001 and the lock path, and sends no HTTP request.</summary>
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

    /// <summary>When the item file changes from "create" to "do-not-create" after the project was loaded, apply throws InvalidOperationException saying the mapping changed while synchronizing, and sends no HTTP request.</summary>
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

    /// <summary>Passing the label "area: tooling" as two unquoted words to get by-label exits 1 with "Unrecognized command or argument 'tooling'." and no tool-name prefix.</summary>
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

    /// <summary>Running get next-priority in an empty workspace exits 1 with "sharedkernel-repo: Roadmap config is invalid." on standard error.</summary>
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
