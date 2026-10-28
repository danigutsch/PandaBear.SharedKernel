using static PandaBear.RepoConfig.Tests.BaselineTestOperations;

namespace PandaBear.RepoConfig.Tests;

/// <summary>Tests the <c>baseline verify</c> and <c>baseline apply</c> commands.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
[Collection(RepoConfigToolEnvironmentGroup.Name)]
public sealed class BaselineCommandTests
{
    private static readonly string[] ManagedPaths = [".gitattributes", "docs/README.md", "CODEOWNERS"];

    [Theory]
    [InlineData("verify")]
    [InlineData("apply")]
    public async Task Baseline_commands_fail_when_the_configuration_has_no_baseline_section(string verb)
    {
        // Arrange
        using var workspace = CreateWorkspace("""{ "version": 1 }""");

        // Act
        var (exitCode, output, error) = await Run(workspace, verb);

        // Assert
        exitCode.ShouldBe(1, "A command fails when its own section is missing.");
        output.ShouldBe(string.Empty, "Nothing is reported on standard output.");
        error.ShouldContain("has no baseline section", StringComparison.Ordinal, "The error names the missing section.");
    }

    [Fact]
    public async Task Apply_previews_every_creation_and_writes_nothing_without_force()
    {
        // Arrange
        using var workspace = CreateWorkspace();

        // Act
        var (exitCode, output, error) = await Run(workspace, "apply");

        // Assert
        exitCode.ShouldBe(0, "A preview succeeds.");
        error.ShouldBe(string.Empty);
        output.ShouldBe(
            "preview: would create .gitattributes." + Environment.NewLine
            + "preview: would create docs/README.md." + Environment.NewLine
            + "preview: would create CODEOWNERS." + Environment.NewLine
            + "preview: 3 would be created, 0 updated, 0 overwritten, and 0 unchanged; --force writes them." + Environment.NewLine);
        workspace.FileExists(".gitattributes").ShouldBeFalse("A preview writes nothing.");
        workspace.FileExists("docs/README.md").ShouldBeFalse("A preview writes nothing.");
    }

    [Fact]
    public async Task Apply_with_force_writes_marked_files_with_substituted_variables_that_verify_accepts()
    {
        // Arrange
        using var workspace = CreateWorkspace();

        // Act
        var (exitCode, output, error) = await Run(workspace, "apply", force: true);
        var verification = await Run(workspace, "verify");

        // Assert
        exitCode.ShouldBe(0);
        error.ShouldBe(string.Empty);
        output.ShouldBe(
            "Created .gitattributes." + Environment.NewLine
            + "Created docs/README.md." + Environment.NewLine
            + "Created CODEOWNERS." + Environment.NewLine
            + "example-repo: 3 created, 0 updated, 0 overwritten, and 0 unchanged files." + Environment.NewLine);
        workspace.ReadFile(".gitattributes").ShouldStartWith("# Managed by sharedkernel-repo baseline (sha256 ", StringComparison.Ordinal, "Hash-comment marker on a non-Markdown file.");
        var readme = workspace.ReadFile("docs/README.md");
        readme.ShouldStartWith("<!-- Managed by sharedkernel-repo baseline (sha256 ", StringComparison.Ordinal, "HTML-comment marker on a Markdown file.");
        readme.ShouldContain("# example-repo", StringComparison.Ordinal, "The repository name variable is substituted.");
        workspace.ReadFile("CODEOWNERS").ShouldContain("# default branch: trunk", StringComparison.Ordinal, "The default branch variable is substituted in sourced content.");
        workspace.ReadFile("CODEOWNERS").ShouldNotContain("@@", StringComparison.Ordinal, "No placeholder remains.");
        verification.ExitCode.ShouldBe(0, "Applied files match the baseline.");
        verification.Output.ShouldBe("Repository baseline matches 3 files." + Environment.NewLine);
    }

    [Fact]
    public async Task Apply_on_an_up_to_date_repository_changes_nothing()
    {
        // Arrange
        using var workspace = CreateWorkspace();
        _ = await Run(workspace, "apply", force: true);
        var before = ManagedPaths.Select(workspace.ReadFile).ToArray();
        var timestamps = ManagedPaths
            .Select(path => File.GetLastWriteTimeUtc(Path.Combine(workspace.RootPath, path))).ToArray();

        // Act
        var (exitCode, output, error) = await Run(workspace, "apply", force: true);

        // Assert
        exitCode.ShouldBe(0);
        error.ShouldBe(string.Empty);
        output.ShouldBe("example-repo: 0 created, 0 updated, 0 overwritten, and 3 unchanged files." + Environment.NewLine);
        ManagedPaths.Select(workspace.ReadFile).ToArray()
            .ShouldBe(before, "Content is identical.");
        ManagedPaths
            .Select(path => File.GetLastWriteTimeUtc(Path.Combine(workspace.RootPath, path))).ToArray()
            .ShouldBe(timestamps, "Unchanged files are not rewritten.");
    }

    [Fact]
    public async Task Verify_reports_each_drifted_file_and_exits_1()
    {
        // Arrange
        using var workspace = CreateWorkspace();
        _ = await Run(workspace, "apply", force: true);
        workspace.DeleteFile("CODEOWNERS");
        workspace.WriteFile(".gitattributes", "* -text\n");

        // Act
        var (exitCode, output, error) = await Run(workspace, "verify");

        // Assert
        exitCode.ShouldBe(1, "Drift fails verification.");
        output.ShouldBe(string.Empty);
        error.ShouldBe(
            "Repository baseline verification failed:" + Environment.NewLine
            + "- .gitattributes: exists without the baseline marker." + Environment.NewLine
            + "- CODEOWNERS: missing." + Environment.NewLine);
    }

    [Fact]
    public async Task Apply_with_force_refuses_to_overwrite_an_unmanaged_file_and_writes_nothing()
    {
        // Arrange
        using var workspace = CreateWorkspace();
        workspace.WriteFile("CODEOWNERS", "* @someone-else\n");

        // Act
        var (exitCode, _, error) = await Run(workspace, "apply", force: true);

        // Assert
        exitCode.ShouldBe(1, "A conflict fails the command.");
        error.ShouldBe("conflict: CODEOWNERS exists without the baseline marker; set overwriteExisting on its entry to replace it." + Environment.NewLine);
        workspace.ReadFile("CODEOWNERS").ShouldBe("* @someone-else\n", "The unmanaged file is untouched.");
        workspace.FileExists(".gitattributes").ShouldBeFalse("A conflict anywhere writes nothing.");
    }

    [Fact]
    public async Task Apply_with_force_overwrites_an_unmanaged_file_when_its_entry_opts_in()
    {
        // Arrange
        using var workspace = CreateWorkspace(Configuration.Replace(
            """{ "path": "CODEOWNERS", "source": ".config/baseline/CODEOWNERS" }""",
            """{ "path": "CODEOWNERS", "source": ".config/baseline/CODEOWNERS", "overwriteExisting": true }""",
            StringComparison.Ordinal));
        workspace.WriteFile("CODEOWNERS", "* @someone-else\n");

        // Act
        var (exitCode, output, error) = await Run(workspace, "apply", force: true);

        // Assert
        exitCode.ShouldBe(0);
        error.ShouldBe(string.Empty);
        output.ShouldContain("Overwrote CODEOWNERS.", StringComparison.Ordinal);
        output.ShouldContain("1 overwritten", StringComparison.Ordinal);
        workspace.ReadFile("CODEOWNERS").ShouldContain("@example-owners", StringComparison.Ordinal, "The baseline content replaced the file.");
    }

    [Fact]
    public async Task Apply_updates_a_managed_file_whose_source_changed()
    {
        // Arrange
        using var workspace = CreateWorkspace();
        _ = await Run(workspace, "apply", force: true);
        workspace.WriteFile(".config/baseline/CODEOWNERS", "* @new-owners\n");

        // Act
        var (exitCode, output, error) = await Run(workspace, "apply", force: true);

        // Assert
        exitCode.ShouldBe(0);
        error.ShouldBe(string.Empty);
        output.ShouldContain("Updated CODEOWNERS.", StringComparison.Ordinal);
        workspace.ReadFile("CODEOWNERS").ShouldContain("@new-owners", StringComparison.Ordinal);
        (await Run(workspace, "verify")).ExitCode.ShouldBe(0, "The updated file is current.");
    }

    [Fact]
    public async Task Apply_reports_a_hand_edited_managed_file_as_a_conflict()
    {
        // Arrange
        using var workspace = CreateWorkspace();
        _ = await Run(workspace, "apply", force: true);
        workspace.WriteFile("CODEOWNERS", workspace.ReadFile("CODEOWNERS") + "* @hand-edit\n");
        workspace.WriteFile(".config/baseline/CODEOWNERS", "* @new-owners\n");

        // Act
        var (exitCode, _, error) = await Run(workspace, "apply", force: true);

        // Assert
        exitCode.ShouldBe(1);
        error.ShouldBe("conflict: CODEOWNERS was edited after the baseline wrote it; set overwriteExisting on its entry to replace it." + Environment.NewLine);
        workspace.ReadFile("CODEOWNERS").ShouldContain("@hand-edit", StringComparison.Ordinal, "The edit is preserved.");
    }

    [Fact]
    public async Task Apply_fails_on_an_unknown_variable_in_a_source_file()
    {
        // Arrange
        using var workspace = CreateWorkspace();
        workspace.WriteFile(".config/baseline/CODEOWNERS", "* @@ownerName@@\n");

        // Act
        var (exitCode, _, error) = await Run(workspace, "apply", force: true);

        // Assert
        exitCode.ShouldBe(1);
        error.ShouldContain("CODEOWNERS", StringComparison.Ordinal, "The error names the file.");
        error.ShouldContain("@@ownerName@@", StringComparison.Ordinal, "The error names the unknown variable.");
        workspace.FileExists(".gitattributes").ShouldBeFalse("Nothing is written.");
    }

    [Fact]
    public async Task Apply_with_force_refuses_a_path_through_a_symbolic_link()
    {
        // Arrange
        using var workspace = CreateWorkspace();
        using var outside = new TemporaryRepoConfigWorkspace();
        Directory.CreateSymbolicLink(Path.Combine(workspace.RootPath, "docs"), outside.RootPath);

        // Act
        var (exitCode, _, error) = await Run(workspace, "apply", force: true);

        // Assert
        exitCode.ShouldBe(1);
        error.ShouldContain("Symbolic links are not followed", StringComparison.Ordinal);
        outside.FileExists("README.md").ShouldBeFalse("Nothing is written through the link.");
    }

    [Theory]
    [InlineData("""{ "path": ".gitattributes", "profile": "nope" }""", "$.baseline.files[0].profile")]
    [InlineData("""{ "path": ".gitattributes" }""", "$.baseline.files[0] must name exactly one of profile and source")]
    [InlineData("""{ "path": ".gitattributes", "profile": "gitignore", "source": "a/b" }""", "$.baseline.files[0] must name exactly one of profile and source")]
    [InlineData("""{ "path": "../x", "profile": "gitignore" }""", "$.baseline.files[0].path")]
    [InlineData("""{ "path": ".git/config", "profile": "gitignore" }""", "$.baseline.files[0].path")]
    [InlineData("""{ "path": "x.json", "profile": "gitignore" }""", "$.baseline.files[0].path")]
    [InlineData("""{ "path": ".gitignore", "source": "/etc/passwd" }""", "$.baseline.files[0].source")]
    [InlineData("""{ "path": ".gitignore", "profile": "gitignore", "extra": 1 }""", "$.baseline.files[0]")]
    public async Task Baseline_commands_reject_an_invalid_file_entry(string entry, string expectedFragment)
    {
        // Arrange
        using var workspace = CreateWorkspace(
            $$"""{ "version": 1, "baseline": { "repositoryName": "example-repo", "files": [ {{entry}} ] } }""");

        // Act
        var (exitCode, _, error) = await Run(workspace, "verify");

        // Assert
        exitCode.ShouldBe(1);
        error.ShouldContain(expectedFragment, StringComparison.Ordinal, "The error names the JSON path of the problem.");
    }

    [Theory]
    [InlineData("""{ "repositoryName": "bad name", "files": [ { "path": ".gitignore", "profile": "gitignore" } ] }""", "$.baseline.repositoryName")]
    [InlineData("""{ "repositoryName": "ok", "defaultBranch": "-x", "files": [ { "path": ".gitignore", "profile": "gitignore" } ] }""", "$.baseline.defaultBranch")]
    [InlineData("""{ "repositoryName": "ok", "files": [] }""", "$.baseline.files must name at least one file")]
    [InlineData("""{ "repositoryName": "ok", "files": [ { "path": "A", "profile": "gitignore" }, { "path": "a", "profile": "readme" } ] }""", "$.baseline.files[1].path repeats")]
    public async Task Baseline_commands_reject_invalid_section_values(string section, string expectedFragment)
    {
        // Arrange
        using var workspace = CreateWorkspace($$"""{ "version": 1, "baseline": {{section}} }""");

        // Act
        var (exitCode, _, error) = await Run(workspace, "verify");

        // Assert
        exitCode.ShouldBe(1);
        error.ShouldContain(expectedFragment, StringComparison.Ordinal, "The error names the JSON path of the problem.");
    }
}
