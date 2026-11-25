using static SharedKernel.RepoConfig.Tests.SdkBootstrapTestOperations;

namespace SharedKernel.RepoConfig.Tests;

/// <summary>Exercises the SDK bootstrap scaffold with an isolated release feed and installation directory.</summary>
[Trait(TestTraitNames.CategoryName, TestTraits.CommandLineCategory)]
[Collection(RepoConfigToolEnvironmentGroup.Name)]
public sealed class SdkBootstrapCommandTests
{
    [Theory]
    [InlineData(true, 0, "is installed")]
    [InlineData(false, 1, "use --install")]
    public async Task Default_mode_verifies_the_pinned_version_without_downloading(bool sdkInstalled, int expectedExitCode, string expected)
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var tools = new TemporaryRepoConfigWorkspace();
        PrepareTools(workspace, tools, sdkInstalled);
        var creation = await Scaffold(workspace);

        // Act
        var result = await Run(workspace, tools);
        var message = result.Output + result.Error;

        // Assert
        creation.ExitCode.ShouldBe(0, creation.Error);
        result.ExitCode.ShouldBe(expectedExitCode, result.Error);
        message.ShouldContain(expected, StringComparison.Ordinal);
        workspace.FileExists("metadata-requested").ShouldBeFalse();
        Directory.Exists(Path.Combine(workspace.RootPath, InstallParent)).ShouldBeFalse();
    }

    [Fact]
    public async Task The_bootstrap_feature_requires_the_dotnet_language()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();

        // Act
        var result = await NewTestOperations.Run(workspace, "--feature", "sdk-bootstrap", "--force");

        // Assert
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("needs --language dotnet", StringComparison.Ordinal);
        workspace.FileExists("scripts/bootstrap-dotnet.sh").ShouldBeFalse();
    }

    [Fact]
    public async Task The_exact_bootstrap_feature_requires_a_selected_or_existing_sdk_pin()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        string[] options = ["--language", "dotnet", "--feature", "sdk-bootstrap", "--force"];

        // Act
        var missing = await NewTestOperations.Run(workspace, options);
        var wroteWithoutPin = workspace.FileExists("scripts/bootstrap-dotnet.sh");
        workspace.WriteFile("global.json", """{"sdk":{"version":"10.0.100"}}""");
        var existing = await NewTestOperations.Run(workspace, options);

        // Assert
        missing.ExitCode.ShouldBe(1);
        missing.Error.ShouldContain("requires global.json", StringComparison.Ordinal);
        wroteWithoutPin.ShouldBeFalse();
        existing.ExitCode.ShouldBe(0, existing.Error);
        workspace.FileExists("scripts/bootstrap-dotnet.sh").ShouldBeTrue();
    }

    [Fact]
    public async Task An_archive_hash_mismatch_stops_before_extraction()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var tools = new TemporaryRepoConfigWorkspace();
        PrepareTools(workspace, tools, sdkInstalled: false);
        WriteReleaseFeed(workspace, OfficialArchiveUrl, new string('0', 128));
        var creation = await Scaffold(workspace);

        // Act
        var result = await Run(workspace, tools, "--install");

        // Assert
        creation.ExitCode.ShouldBe(0, creation.Error);
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("SHA-512 checksum mismatch", StringComparison.Ordinal);
        workspace.FileExists("metadata-requested").ShouldBeTrue();
        workspace.FileExists("archive-requested").ShouldBeTrue();
        workspace.FileExists("extraction-attempted").ShouldBeFalse();
        workspace.FileExists(InstalledHost).ShouldBeFalse();
    }

    [Fact]
    public async Task An_untrusted_archive_URL_is_rejected_before_downloading()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var tools = new TemporaryRepoConfigWorkspace();
        PrepareTools(workspace, tools, sdkInstalled: false);
        WriteReleaseFeed(workspace, "https://example.invalid/dotnet/sdk.tar.gz", FixtureHash);
        var creation = await Scaffold(workspace);

        // Act
        var result = await Run(workspace, tools, "--install");

        // Assert
        creation.ExitCode.ShouldBe(0, creation.Error);
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("Unexpected SDK download URL", StringComparison.Ordinal);
        workspace.FileExists("metadata-requested").ShouldBeTrue();
        workspace.FileExists("archive-requested").ShouldBeFalse();
        workspace.FileExists("extraction-attempted").ShouldBeFalse();
    }

    [Fact]
    public async Task Verified_installation_is_available_side_by_side_with_existing_versions()
    {
        // Arrange
        using var workspace = new TemporaryRepoConfigWorkspace();
        using var tools = new TemporaryRepoConfigWorkspace();
        PrepareTools(workspace, tools, sdkInstalled: false);
        WriteReleaseFeed(workspace, OfficialArchiveUrl, FixtureHash);
        const string existing = InstallParent + "/10.0.101/linux-x64/fixture.txt";
        workspace.WriteFile(existing, "Synthetic existing installation.");
        var creation = await Scaffold(workspace);

        // Act
        var installation = await Run(workspace, tools, "--install");
        workspace.DeleteFile("archive-requested");
        var verification = await Run(workspace, tools);
        var baseline = await NewTestOperations.BaselineVerify(workspace);

        // Assert
        creation.ExitCode.ShouldBe(0, creation.Error);
        installation.ExitCode.ShouldBe(0, installation.Error);
        installation.Output.ShouldContain("Installed SDK 10.0.100", StringComparison.Ordinal);
        workspace.FileExists("extraction-attempted").ShouldBeTrue();
        workspace.FileExists(InstalledHost).ShouldBeTrue();
        workspace.ReadFile(existing).ShouldBe("Synthetic existing installation.");
        verification.ExitCode.ShouldBe(0, verification.Error);
        workspace.FileExists("archive-requested").ShouldBeFalse();
        baseline.ExitCode.ShouldBe(0, baseline.Error);
    }
}
