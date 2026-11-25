using System.Text.Json;
using static SharedKernel.RepoConfig.Tests.HookProfileTestOperations;
using static SharedKernel.RepoConfig.Tests.NewTestOperations;

namespace SharedKernel.RepoConfig.Tests;

/// <summary>Tests language profiles and the write-once Dev Container option.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
[Collection(RepoConfigToolEnvironmentGroup.Name)]
public sealed class NewLanguageAndDevcontainerTests
{
    private const string DevcontainerPath = ".devcontainer/devcontainer.json";
    private const string Digest = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string Image = "registry.example.test:5000/tools/shell@sha256:" + Digest;

    [Fact]
    public async Task Shell_defaults_normalize_shell_sources_to_lf()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();

        // Act
        var result = await Run(workspace, "--language", "shell", "--force");
        var attributes = await RunGit(workspace, ["check-attr", "text", "eol", "--", "setup.sh", "lib/setup.bash", "test/cli.bats"]);
        var verification = await BaselineVerify(workspace);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        attributes.ExitCode.ShouldBe(0, attributes.Error);
        attributes.Output.ShouldContain("setup.sh: text: set", StringComparison.Ordinal);
        attributes.Output.ShouldContain("lib/setup.bash: eol: lf", StringComparison.Ordinal);
        attributes.Output.ShouldContain("test/cli.bats: text: set", StringComparison.Ordinal);
        workspace.FileExists("global.json").ShouldBeFalse();
        workspace.FileExists(DevcontainerPath).ShouldBeFalse();
        verification.ExitCode.ShouldBe(0, verification.Error);
    }

    [Fact]
    public async Task Nix_defaults_ignore_build_outputs_and_keep_sources_trackable()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();

        // Act
        var result = await Run(workspace, "--language", "nix", "--force");
        var ignored = await RunGit(workspace, ["check-ignore", "--", "result", "packages/result-1-dev", "flake.nix", "flake.lock"]);
        var verification = await BaselineVerify(workspace);

        // Assert
        result.ExitCode.ShouldBe(0, result.Error);
        ignored.ExitCode.ShouldBe(0, ignored.Error);
        ignored.Output.ReplaceLineEndings("\n").ShouldBe("result\npackages/result-1-dev\n");
        workspace.FileExists("NuGet.Config").ShouldBeFalse();
        workspace.FileExists(DevcontainerPath).ShouldBeFalse();
        verification.ExitCode.ShouldBe(0, verification.Error);
    }

    [Theory]
    [InlineData("shell", "global-json")]
    [InlineData("shell", "build-props")]
    [InlineData("nix", "nuget-config")]
    [InlineData("nix", "sdk-bootstrap")]
    public async Task New_rejects_dotnet_features_for_other_languages_before_writing(string language, string feature)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();

        // Act
        var result = await Run(workspace, "--language", language, "--with", feature, "--sdk-version", "10.0.100", "--force");

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain($"feature '{feature}' needs --language dotnet", StringComparison.Ordinal);
        Directory.EnumerateFileSystemEntries(workspace.RootPath).ShouldBeEmpty();
    }

    [Fact]
    public async Task New_can_preview_and_create_devcontainer_with_language_defaults()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        string[] options = ["--name", "example-project", "--language", "shell", "--with", "devcontainer", "--devcontainer-image", Image];

        // Act
        var preview = await Run(workspace, options);
        var previewCreatedFile = workspace.FileExists(DevcontainerPath);
        var creation = await Run(workspace, [.. options, "--force"]);

        // Assert
        preview.ExitCode.ShouldBe(0, preview.Error);
        preview.Output.ShouldContain($"preview: would create {DevcontainerPath}.", StringComparison.Ordinal);
        previewCreatedFile.ShouldBeFalse();
        creation.ExitCode.ShouldBe(0, creation.Error);
        workspace.FileExists(".gitattributes").ShouldBeTrue();
        var content = workspace.ReadFile(DevcontainerPath);
        using var configuration = JsonDocument.Parse(content);
        configuration.RootElement.GetProperty("name").GetString().ShouldBe("example-project");
        configuration.RootElement.GetProperty("image").GetString().ShouldBe(Image);
        workspace.ReadFile(".config/sharedkernel-repo.json").ShouldNotContain(DevcontainerPath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Devcontainer_exact_feature_selection_preserves_edits_on_baseline_apply()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        const string edited = "{\n  \"image\": \"" + Image + "\",\n  \"remoteUser\": \"developer\"\n}\n";

        // Act
        var creation = await Run(workspace, "--language", "nix", "--feature", "readme", "--feature", "devcontainer", "--devcontainer-image", Image, "--force");
        var seeded = workspace.ReadFile(DevcontainerPath);
        workspace.WriteFile(DevcontainerPath, edited);
        using var output = new InvariantStringWriter();
        using var error = new InvariantStringWriter();
        var apply = await RepoConfigToolRunner.Run(["baseline", "apply", "--root", workspace.RootPath, "--force"], output, error,
            Path.GetTempPath(), TestContext.Current.CancellationToken);

        // Assert
        creation.ExitCode.ShouldBe(0, creation.Error);
        seeded.ShouldContain(Image, StringComparison.Ordinal);
        workspace.FileExists(".gitignore").ShouldBeFalse();
        apply.ShouldBe(0, error.ToString());
        workspace.ReadFile(DevcontainerPath).ShouldBe(edited);
    }

    [Theory]
    [InlineData("registry.example.test/tools/shell:latest")]
    [InlineData("shell@sha256:short")]
    [InlineData("shell@sha256:0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdeF")]
    [InlineData("https://registry.example.test/tools/shell@sha256:" + Digest)]
    [InlineData("shell@sha256:" + Digest + "\n")]
    public async Task New_rejects_invalid_devcontainer_images_before_writing(string image)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();

        // Act
        var result = await Run(workspace, "--with", "devcontainer", "--devcontainer-image", image, "--force");

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("digest-pinned", StringComparison.Ordinal);
        Directory.EnumerateFileSystemEntries(workspace.RootPath).ShouldBeEmpty();
    }

    [Fact]
    public async Task New_requires_an_image_when_devcontainer_is_selected()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();

        // Act
        var result = await Run(workspace, "--with", "devcontainer");

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("feature 'devcontainer' needs --devcontainer-image", StringComparison.Ordinal);
        Directory.EnumerateFileSystemEntries(workspace.RootPath).ShouldBeEmpty();
    }

    [Fact]
    public async Task New_requires_explicit_devcontainer_selection_when_an_image_is_supplied()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();

        // Act
        var result = await Run(workspace, "--devcontainer-image", Image, "--force");

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("--devcontainer-image needs the devcontainer feature", StringComparison.Ordinal);
        Directory.EnumerateFileSystemEntries(workspace.RootPath).ShouldBeEmpty();
    }

    [Fact]
    public async Task New_refuses_an_existing_devcontainer_before_creating_other_files()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile(DevcontainerPath, "{\"name\":\"owner-managed\"}\n");

        // Act
        var result = await Run(workspace, "--with", "devcontainer", "--devcontainer-image", Image, "--force");

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain($"conflict: {DevcontainerPath} exists", StringComparison.Ordinal);
        workspace.ReadFile(DevcontainerPath).ShouldBe("{\"name\":\"owner-managed\"}\n");
        workspace.FileExists("README.md").ShouldBeFalse();
        Directory.Exists(Path.Combine(workspace.RootPath, ".git")).ShouldBeFalse();
    }

    [Fact]
    public async Task New_preview_refuses_a_file_where_the_devcontainer_directory_must_be()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        workspace.WriteFile(".devcontainer", "existing content\n");

        // Act
        var result = await Run(workspace, "--with", "devcontainer", "--devcontainer-image", Image);

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain($"conflict: {DevcontainerPath}", StringComparison.Ordinal);
        workspace.ReadFile(".devcontainer").ShouldBe("existing content\n");
        workspace.FileExists("README.md").ShouldBeFalse();
    }
}
