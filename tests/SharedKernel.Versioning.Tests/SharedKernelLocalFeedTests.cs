namespace SharedKernel.Versioning.Tests;

[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, TestTraits.VersioningCapability)]
public static class SharedKernelLocalFeedTests
{
    [Fact]
    public static void Reads_package_ids_from_valid_packages()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        var firstPackage = NuGetPackageBuilder.WritePackage(
            temporaryDirectory.PackageDirectory,
            "SharedKernel.Results",
            "1.2.3",
            ("SharedKernel.Functional", "1.2.3"));
        var secondPackage = NuGetPackageBuilder.WritePackage(
            temporaryDirectory.PackageDirectory,
            "SharedKernel.Functional",
            "1.2.3");

        // Act
        var packageIds = SharedKernelLocalFeed.ReadPackageIds([firstPackage, secondPackage], "1.2.3");

        // Assert
        packageIds.ShouldBe(["SharedKernel.Functional", "SharedKernel.Results"]);
    }

    [Fact]
    public static void Rejects_package_without_nuspec()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        var package = NuGetPackageBuilder.WritePackageWithoutNuspec(
            temporaryDirectory.PackageDirectory,
            "SharedKernel.Results.1.2.3.nupkg");

        // Act
        Action action = () => SharedKernelLocalFeed.ReadPackageIds([package], "1.2.3");

        // Assert
        action.ShouldThrow<ArgumentException>().Message.ShouldContain("expected one .nuspec", StringComparison.Ordinal);
    }

    [Fact]
    public static void Rejects_package_version_mismatch()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        var package = NuGetPackageBuilder.WritePackage(
            temporaryDirectory.PackageDirectory,
            "SharedKernel.Results",
            "1.2.2");

        // Act
        Action action = () => SharedKernelLocalFeed.ReadPackageIds([package], "1.2.3");

        // Assert
        action.ShouldThrow<ArgumentException>().Message.ShouldContain("expected version 1.2.3", StringComparison.Ordinal);
    }

    [Fact]
    public static void Rejects_internal_dependency_version_mismatch()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        var package = NuGetPackageBuilder.WritePackage(
            temporaryDirectory.PackageDirectory,
            "SharedKernel.Results",
            "1.2.3",
            ("SharedKernel.Functional", "1.2.2"));

        // Act
        Action action = () => SharedKernelLocalFeed.ReadPackageIds([package], "1.2.3");

        // Assert
        action.ShouldThrow<ArgumentException>().Message.ShouldContain("dependency version 1.2.3", StringComparison.Ordinal);
    }

    [Fact]
    public static void Rejects_duplicate_package_ids()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        var firstPackage = NuGetPackageBuilder.WritePackage(
            temporaryDirectory.PackageDirectory,
            "SharedKernel.Results",
            "1.2.3");
        var secondPackage = NuGetPackageBuilder.WritePackage(
            temporaryDirectory.OutputDirectory,
            "SharedKernel.Results",
            "1.2.3");

        // Act
        Action action = () => SharedKernelLocalFeed.ReadPackageIds([firstPackage, secondPackage], "1.2.3");

        // Assert
        action.ShouldThrow<ArgumentException>().Message.ShouldContain("duplicate packages", StringComparison.Ordinal);
    }

    [Fact]
    public static void Detects_restored_package_case_insensitively()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        var packageCache = Path.Combine(temporaryDirectory.Root, "packages");
        Directory.CreateDirectory(Path.Combine(packageCache, "sharedkernel.results", "1.2.3"));

        // Act
        var restored = SharedKernelLocalFeed.PackageWasRestored(packageCache, "SharedKernel.Results", "1.2.3");

        // Assert
        restored.ShouldBeTrue();
    }

    [Fact]
    public static async Task Rejects_relative_nuget_source_url()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        var relativeUri = new Uri("v3/index.json", UriKind.Relative);

        // Act
        Func<Task> action = () => SharedKernelLocalFeed.WriteNuGetConfig(
            temporaryDirectory.OutputDirectory,
            temporaryDirectory.PackageDirectory,
            ["SharedKernel.Results"],
            relativeUri);

        // Assert
        var exception = await action.ShouldThrow<ArgumentException>();
        exception.Message.ShouldContain("NuGet source URL must be absolute.", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Writes_restore_project_and_nuget_config()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        Directory.CreateDirectory(temporaryDirectory.OutputDirectory);

        // Act
        await SharedKernelLocalFeed.WriteRestoreProject(
            temporaryDirectory.OutputDirectory,
            ["SharedKernel.Results"],
            "1.2.3");
        await SharedKernelLocalFeed.WriteNuGetConfig(
            temporaryDirectory.OutputDirectory,
            temporaryDirectory.PackageDirectory,
            ["SharedKernel.Results"],
            new Uri("https://example.test/v3/index.json"));

        // Assert
        var project = await File.ReadAllTextAsync(
            Path.Combine(temporaryDirectory.OutputDirectory, "SharedKernel.LocalFeedRestore.csproj"),
            TestContext.Current.CancellationToken);
        var nugetConfig = await File.ReadAllTextAsync(
            Path.Combine(temporaryDirectory.OutputDirectory, "NuGet.config"),
            TestContext.Current.CancellationToken);
        project.ShouldContain("PackageReference Include=\"SharedKernel.Results\" Version=\"1.2.3\"", StringComparison.Ordinal);
        nugetConfig.ShouldContain("<clear />", StringComparison.Ordinal);
        nugetConfig.ShouldContain("pattern=\"SharedKernel.Results\"", StringComparison.Ordinal);
        nugetConfig.ShouldContain("https://example.test/v3/index.json", StringComparison.Ordinal);
    }
}
