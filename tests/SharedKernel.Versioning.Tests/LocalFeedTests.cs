using System.Xml.Linq;

namespace PandaBear.Versioning.Tests;

/// <summary>
/// Tests generation and validation of the local SharedKernel package feed configuration.
/// </summary>
[Trait(SharedKernelTestTraitNames.CapabilityName, TestTraits.VersioningCapability)]
public static class LocalFeedTests
{
    [Fact]
    public static async Task Missing_internal_packages_cannot_fall_back_to_public_feed()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        Directory.CreateDirectory(temporaryDirectory.OutputDirectory);

        // Act
        await LocalFeed.WriteNuGetConfig(
            temporaryDirectory.OutputDirectory,
            temporaryDirectory.PackageDirectory,
            ["PandaBear.SharedKernel.Results"],
            new Uri("https://example.test/v3/index.json"));
        var config = await File.ReadAllTextAsync(Path.Combine(temporaryDirectory.OutputDirectory, "NuGet.config"), TestContext.Current.CancellationToken);

        // Assert
        config.ShouldContain("pattern=\"PandaBear.SharedKernel.*\"", StringComparison.Ordinal);
    }

    [Fact]
    public static async Task Generated_configuration_audits_against_the_nuget_source_alone()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        Directory.CreateDirectory(temporaryDirectory.OutputDirectory);
        var source = new Uri("https://example.test/v3/index.json");

        // Act
        await LocalFeed.WriteNuGetConfig(
            temporaryDirectory.OutputDirectory,
            temporaryDirectory.PackageDirectory,
            ["PandaBear.SharedKernel.Results"],
            source);
        var config = XDocument.Load(Path.Combine(temporaryDirectory.OutputDirectory, "NuGet.config"));

        // Assert
        var auditSources = config.Root.ShouldNotBeNull().Elements("auditSources").ShouldHaveSingleItem().Elements().ToArray();
        auditSources.Select(static element => element.Name.LocalName).ShouldBe(
            ["clear", "add"],
            "Without audit sources NuGet audits against the package sources and suppresses NU1905, and the local folder has no vulnerability data.");
        auditSources[1].Attribute("value").ShouldNotBeNull().Value.ShouldBe(source.AbsoluteUri);
    }

    [Fact]
    public static async Task Restore_project_audits_every_dependency_at_every_severity_as_errors()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        Directory.CreateDirectory(temporaryDirectory.OutputDirectory);

        // Act
        await LocalFeed.WriteRestoreProject(temporaryDirectory.OutputDirectory, ["PandaBear.SharedKernel.Results"], "1.2.3", ["net11.0"]);
        var project = await File.ReadAllTextAsync(
            Path.Combine(temporaryDirectory.OutputDirectory, "SharedKernel.LocalFeedRestore.csproj"),
            TestContext.Current.CancellationToken);

        // Assert
        project.ShouldContain("<TreatWarningsAsErrors>true</TreatWarningsAsErrors>", StringComparison.Ordinal, "An audit that could not run (NU1900, NU1905) or an advisory fails the restore.");
        project.ShouldContain("<NuGetAudit>true</NuGetAudit>", StringComparison.Ordinal);
        project.ShouldContain("<NuGetAuditMode>all</NuGetAuditMode>", StringComparison.Ordinal);
        project.ShouldNotContain("NuGetAuditLevel", StringComparison.Ordinal, "NuGet's default level, low, reports advisories of every severity.");
    }

    [Fact]
    public static void Reads_package_IDs_from_valid_packages()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        var firstPackage = NuGetPackageBuilder.WritePackage(
            temporaryDirectory.PackageDirectory,
            "PandaBear.SharedKernel.Results",
            "1.2.3",
            ("PandaBear.SharedKernel.Functional", "1.2.3"));
        var secondPackage = NuGetPackageBuilder.WritePackage(
            temporaryDirectory.PackageDirectory,
            "PandaBear.SharedKernel.Functional",
            "1.2.3");

        // Act
        var packageIds = LocalFeed.ReadPackageIds([firstPackage, secondPackage], "1.2.3");

        // Assert
        packageIds.ShouldBe(["PandaBear.SharedKernel.Functional", "PandaBear.SharedKernel.Results"]);
    }

    [Fact]
    public static void Rejects_package_without_nuspec()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        var package = NuGetPackageBuilder.WritePackageWithoutNuspec(
            temporaryDirectory.PackageDirectory,
            "PandaBear.SharedKernel.Results.1.2.3.nupkg");

        // Act
        Action action = () => LocalFeed.ReadPackageIds([package], "1.2.3");

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
            "PandaBear.SharedKernel.Results",
            "1.2.2");

        // Act
        Action action = () => LocalFeed.ReadPackageIds([package], "1.2.3");

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
            "PandaBear.SharedKernel.Results",
            "1.2.3",
            ("PandaBear.SharedKernel.Functional", "1.2.2"));

        // Act
        Action action = () => LocalFeed.ReadPackageIds([package], "1.2.3");

        // Assert
        action.ShouldThrow<ArgumentException>().Message.ShouldContain("dependency version 1.2.3", StringComparison.Ordinal);
    }

    [Fact]
    public static void Rejects_duplicate_package_IDs()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        var firstPackage = NuGetPackageBuilder.WritePackage(
            temporaryDirectory.PackageDirectory,
            "PandaBear.SharedKernel.Results",
            "1.2.3");
        var secondPackage = NuGetPackageBuilder.WritePackage(
            temporaryDirectory.OutputDirectory,
            "PandaBear.SharedKernel.Results",
            "1.2.3");

        // Act
        Action action = () => LocalFeed.ReadPackageIds([firstPackage, secondPackage], "1.2.3");

        // Assert
        action.ShouldThrow<ArgumentException>().Message.ShouldContain("duplicate packages", StringComparison.Ordinal);
    }

    [Fact]
    public static void Detects_restored_package_case_insensitively()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        var packageCache = Path.Combine(temporaryDirectory.Root, "packages");
        Directory.CreateDirectory(Path.Combine(packageCache, "pandabear.sharedkernel.results", "1.2.3"));

        // Act
        var restored = LocalFeed.PackageWasRestored(packageCache, "PandaBear.SharedKernel.Results", "1.2.3");

        // Assert
        restored.ShouldBeTrue();
    }

    [Fact]
    public static async Task Rejects_relative_nuget_source_URL()
    {
        // Arrange
        using var temporaryDirectory = new TemporaryReleasePrepDirectory();
        var relativeUri = new Uri("v3/index.json", UriKind.Relative);

        // Act
        Func<Task> action = () => LocalFeed.WriteNuGetConfig(
            temporaryDirectory.OutputDirectory,
            temporaryDirectory.PackageDirectory,
            ["PandaBear.SharedKernel.Results"],
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
        await LocalFeed.WriteRestoreProject(
            temporaryDirectory.OutputDirectory,
            ["PandaBear.SharedKernel.Results"],
            "1.2.3",
            ["net11.0", "net12.0"]);
        await LocalFeed.WriteNuGetConfig(
            temporaryDirectory.OutputDirectory,
            temporaryDirectory.PackageDirectory,
            ["PandaBear.SharedKernel.Results"],
            new Uri("https://example.test/v3/index.json"));

        // Assert
        var project = await File.ReadAllTextAsync(
            Path.Combine(temporaryDirectory.OutputDirectory, "SharedKernel.LocalFeedRestore.csproj"),
            TestContext.Current.CancellationToken);
        var nugetConfig = await File.ReadAllTextAsync(
            Path.Combine(temporaryDirectory.OutputDirectory, "NuGet.config"),
            TestContext.Current.CancellationToken);
        project.ShouldContain("PackageReference Include=\"PandaBear.SharedKernel.Results\" Version=\"1.2.3\"", StringComparison.Ordinal);
        project.ShouldContain("<TargetFrameworks>net11.0;net12.0</TargetFrameworks>", StringComparison.Ordinal);
        nugetConfig.ShouldContain("<clear />", StringComparison.Ordinal);
        nugetConfig.ShouldContain("pattern=\"PandaBear.SharedKernel.Results\"", StringComparison.Ordinal);
        nugetConfig.ShouldContain("https://example.test/v3/index.json", StringComparison.Ordinal);
    }
}
