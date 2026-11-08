namespace PandaBear.Versioning.Tests;

/// <summary>Tests the lock coverage guard that fails when a lock-free source project resolves a package that no committed lock file resolves.</summary>
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.VersioningCapability)]
public static class VerifyLockCoverageTests
{
    [Fact]
    public static async Task Passes_and_counts_the_lock_free_projects_whose_packages_a_committed_lock_covers()
    {
        // Arrange
        using var workspace = new TemporaryReleasePrepDirectory();
        await InventoryFiles.WriteProject(workspace.Root, "src/Exempt");
        await InventoryFiles.WriteAssets(workspace.Root, "src/Exempt", "Shared.Package@1.0.0");
        await InventoryFiles.WriteLock(workspace.Root, "tests/Exempt.Tests", "Shared.Package@1.0.0");

        // Act
        var result = await VersioningCommandLineRunner.Invoke(["verify-lock-coverage", "--repo-root", workspace.Root], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Error.ShouldBe(string.Empty);
        result.Output.ShouldContain("1 lock-free project", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Fails_naming_the_project_package_and_remedy_when_a_pair_is_uncovered()
    {
        // Arrange
        using var workspace = new TemporaryReleasePrepDirectory();
        await InventoryFiles.WriteProject(workspace.Root, "src/Exempt");
        await InventoryFiles.WriteAssets(workspace.Root, "src/Exempt", "Covered.Package@1.0.0", "Drifted.Package@3.0.0");
        await InventoryFiles.WriteLock(workspace.Root, "tests/Exempt.Tests", "Covered.Package@1.0.0", "Drifted.Package@2.0.0");

        // Act
        var result = await VersioningCommandLineRunner.Invoke(["verify-lock-coverage", "--repo-root", workspace.Root], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("src/Exempt/Exempt.csproj: Drifted.Package@3.0.0", StringComparison.Ordinal);
        result.Error.ShouldNotContain("Covered.Package@1.0.0", StringComparison.Ordinal);
        result.Error.ShouldContain("lockExemptProjects", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Fails_with_a_restore_hint_instead_of_passing_when_a_lock_free_project_has_no_assets()
    {
        // Arrange
        using var workspace = new TemporaryReleasePrepDirectory();
        await InventoryFiles.WriteProject(workspace.Root, "src/Exempt");
        await InventoryFiles.WriteLock(workspace.Root, "tests/Exempt.Tests", "Shared.Package@1.0.0");

        // Act
        var result = await VersioningCommandLineRunner.Invoke(["verify-lock-coverage", "--repo-root", workspace.Root], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(2);
        result.Error.ShouldContain("dotnet restore", StringComparison.Ordinal);
        result.Error.ShouldContain("src/Exempt/Exempt.csproj", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Passes_a_repository_where_every_source_project_keeps_its_lock_file()
    {
        // Arrange
        using var workspace = new TemporaryReleasePrepDirectory();
        await InventoryFiles.WriteProject(workspace.Root, "src/Locked");
        await InventoryFiles.WriteLock(workspace.Root, "src/Locked", "Locked.Package@1.0.0");

        // Act
        var result = await VersioningCommandLineRunner.Invoke(["verify-lock-coverage", "--repo-root", workspace.Root], TestContext.Current.CancellationToken);

        // Assert
        result.ExitCode.ShouldBe(0);
        result.Output.ShouldContain("0 lock-free project", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Ignores_the_assets_of_a_project_that_keeps_a_lock_file()
    {
        // Arrange
        using var workspace = new TemporaryReleasePrepDirectory();
        await InventoryFiles.WriteProject(workspace.Root, "src/Locked");
        await InventoryFiles.WriteLock(workspace.Root, "src/Locked", "Locked.Package@1.0.0");
        await InventoryFiles.WriteAssets(workspace.Root, "src/Locked", "Unlocked.Package@9.0.0");

        // Act
        var verified = PackageLockInventory.VerifyLockCoverage(workspace.Root);

        // Assert
        verified.ShouldBe(0);
    }

    [Fact]
    public static void Rejects_a_blank_repository_root()
    {
        // Act
        Action verify = () => PackageLockInventory.VerifyLockCoverage(" ");

        // Assert
        verify.ShouldThrow<ArgumentException>();
    }
}
