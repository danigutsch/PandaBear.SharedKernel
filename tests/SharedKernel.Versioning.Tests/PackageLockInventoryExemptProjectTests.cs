namespace PandaBear.Versioning.Tests;

/// <summary>Tests that the package inventory reads restored assets for source projects that keep no lock file, and guards against uncovered packages.</summary>
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.VersioningCapability)]
public static class PackageLockInventoryExemptProjectTests
{
    [Fact]
    public static async Task Reads_restored_assets_for_a_source_project_without_a_lock_file()
    {
        // Arrange
        using var workspace = new TemporaryReleasePrepDirectory();
        await InventoryFiles.WriteProject(workspace.Root, "src/Lib");
        await InventoryFiles.WriteAssets(workspace.Root, "src/Lib", "Shipped.Package@1.0.0", "Other.Package@2.0.0");
        await InventoryFiles.WriteLock(workspace.Root, "tests/Lib.Tests", "Shipped.Package@1.0.0", "Other.Package@2.0.0");

        // Act
        var packages = PackageLockInventory.Read(workspace.Root);

        // Assert
        packages.Select(package => package.Id + "@" + package.Version).ShouldBe(["Other.Package@2.0.0", "Shipped.Package@1.0.0"]);
        packages[1].LockFiles.ShouldBe(["src/Lib/obj/project.assets.json"]);
    }

    [Fact]
    public static async Task Reads_the_lock_file_and_not_the_assets_when_a_source_project_has_a_lock_file()
    {
        // Arrange
        using var workspace = new TemporaryReleasePrepDirectory();
        await InventoryFiles.WriteProject(workspace.Root, "src/Lib");
        await InventoryFiles.WriteLock(workspace.Root, "src/Lib", "Locked.Package@1.0.0");
        await InventoryFiles.WriteAssets(workspace.Root, "src/Lib", "Assets.Package@9.0.0");

        // Act
        var packages = PackageLockInventory.Read(workspace.Root);

        // Assert
        var package = packages.ShouldHaveSingleItem();
        package.Id.ShouldBe("Locked.Package");
        package.LockFiles.ShouldBe(["src/Lib/packages.lock.json"]);
    }

    [Fact]
    public static async Task Merges_a_lock_and_an_assets_entry_for_the_same_package_version()
    {
        // Arrange
        using var workspace = new TemporaryReleasePrepDirectory();
        await InventoryFiles.WriteProject(workspace.Root, "src/Locked");
        await InventoryFiles.WriteLock(workspace.Root, "src/Locked", "Shared.Package@1.0.0");
        await InventoryFiles.WriteProject(workspace.Root, "src/Exempt");
        await InventoryFiles.WriteAssets(workspace.Root, "src/Exempt", "Shared.Package@1.0.0");

        // Act
        var packages = PackageLockInventory.Read(workspace.Root);

        // Assert
        packages.ShouldHaveSingleItem().LockFiles.ShouldBe(["src/Exempt/obj/project.assets.json", "src/Locked/packages.lock.json"]);
    }

    [Fact]
    public static async Task Fails_with_a_restore_hint_when_a_source_project_has_neither_lock_nor_assets()
    {
        // Arrange
        using var workspace = new TemporaryReleasePrepDirectory();
        await InventoryFiles.WriteProject(workspace.Root, "src/Locked");
        await InventoryFiles.WriteLock(workspace.Root, "src/Locked", "Locked.Package@1.0.0");
        await InventoryFiles.WriteProject(workspace.Root, "src/Unrestored");

        // Act
        Action read = () => PackageLockInventory.Read(workspace.Root);
        var exception = read.ShouldThrow<ArgumentException>();

        // Assert
        exception.Message.ShouldContain("dotnet restore", StringComparison.Ordinal);
        exception.Message.ShouldContain("src/Unrestored/Unrestored.csproj", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Fails_listing_project_and_package_when_assets_hold_a_package_that_no_lock_file_covers()
    {
        // Arrange
        using var workspace = new TemporaryReleasePrepDirectory();
        await InventoryFiles.WriteProject(workspace.Root, "src/Lib");
        await InventoryFiles.WriteAssets(workspace.Root, "src/Lib", "Covered.Package@1.0.0", "Uncovered.Package@3.0.0");
        await InventoryFiles.WriteLock(workspace.Root, "tests/Lib.Tests", "Covered.Package@1.0.0", "Uncovered.Package@2.0.0");

        // Act
        Action read = () => PackageLockInventory.Read(workspace.Root);
        var exception = read.ShouldThrow<ArgumentException>();

        // Assert
        exception.Message.ShouldContain("src/Lib/Lib.csproj", StringComparison.Ordinal);
        exception.Message.ShouldContain("Uncovered.Package@3.0.0", StringComparison.Ordinal);
        exception.Message.ShouldNotContain("Covered.Package@1.0.0", StringComparison.Ordinal);
        exception.Message.ShouldContain("lockExemptProjects", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Accepts_assets_packages_that_another_source_lock_file_covers()
    {
        // Arrange
        using var workspace = new TemporaryReleasePrepDirectory();
        await InventoryFiles.WriteProject(workspace.Root, "src/Locked");
        await InventoryFiles.WriteLock(workspace.Root, "src/Locked", "Shared.Package@1.0.0");
        await InventoryFiles.WriteProject(workspace.Root, "src/Exempt");
        await InventoryFiles.WriteAssets(workspace.Root, "src/Exempt", "Shared.Package@1.0.0");

        // Act
        var packages = PackageLockInventory.Read(workspace.Root);

        // Assert
        packages.ShouldHaveSingleItem().Id.ShouldBe("Shared.Package");
    }

    [Fact]
    public static async Task Ignores_generated_lock_files_when_checking_coverage()
    {
        // Arrange
        using var workspace = new TemporaryReleasePrepDirectory();
        await InventoryFiles.WriteProject(workspace.Root, "src/Lib");
        await InventoryFiles.WriteAssets(workspace.Root, "src/Lib", "Uncovered.Package@1.0.0");
        await InventoryFiles.WriteLock(workspace.Root, "tests/Lib.Tests/obj", "Uncovered.Package@1.0.0");

        // Act
        Action read = () => PackageLockInventory.Read(workspace.Root);
        var exception = read.ShouldThrow<ArgumentException>();

        // Assert
        exception.Message.ShouldContain("Uncovered.Package@1.0.0", StringComparison.Ordinal);
    }
}
