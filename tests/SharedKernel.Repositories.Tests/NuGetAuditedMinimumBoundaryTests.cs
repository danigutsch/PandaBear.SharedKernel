namespace PandaBear.Repositories.Tests;

/// <summary>Verifies when source and declaration evidence is insufficient for the automatic minimum.</summary>
public sealed class NuGetAuditedMinimumBoundaryTests
{
    [Theory]
    [InlineData("1.2.0", false)]
    [InlineData("[1.2.0]", true)]
    public async Task Exact_declarations_preserve_the_existing_version_style(string version, bool exact)
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var file = Path.Combine(directory.Path, "Versions.props");
        var ct = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(file, $"<Project><ItemGroup><PackageVersion Include='Owner' Version='{version}'/></ItemGroup></Project>", ct);

        // Act
        var declarations = await NuGetTrialProps.ExactDeclarations(file, ["Owner"], ct);

        // Assert
        declarations.ShouldNotBeNull();
        declarations.Contains("Owner").ShouldBe(exact);
    }

    [Theory]
    [InlineData("<PackageVersion Include='Other' Version='1.0.0'/>")]
    [InlineData("<PackageVersion Include='Owner' />")]
    [InlineData("<PackageVersion Include='Owner' Version='$(InheritedVersion)'/>")]
    [InlineData("<PackageVersion Include='Owner' Version='[1.0.0,2.0.0)'/>")]
    [InlineData("<PackageVersion Include='Owner' Version='1.0.0'/><PackageVersion Update='Owner' Version='2.0.0'/>")]
    public async Task Exact_declarations_require_one_literal_version_for_every_owner(string item)
    {
        // Arrange
        using var directory = new TemporaryDirectory();
        var file = Path.Combine(directory.Path, "Versions.props");
        var ct = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(file, $"<Project><ItemGroup>{item}</ItemGroup></Project>", ct);

        // Act
        var declarations = await NuGetTrialProps.ExactDeclarations(file, ["Owner"], ct);

        // Assert
        declarations.ShouldBeNull();
    }

    [Theory]
    [InlineData("9.0.400")]
    [InlineData("unknown")]
    public async Task Version_catalog_reports_an_unsupported_runtime_without_querying_a_source(string version)
    {
        // Arrange
        NuGetVersionRequestPackage[] requests = [new("Owner", ["1.0.0"], [])];

        // Act
        var results = await NuGetVersionCatalog.Read(Path.GetTempPath(), version, requests, TestContext.Current.CancellationToken);

        // Assert
        var result = results.ShouldHaveSingleItem();
        result.Package.ShouldBe("Owner");
        result.Status.ShouldBe("sdkUnsupported");
        result.Versions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Version_catalog_withholds_results_when_the_request_exceeds_its_limit()
    {
        // Arrange
        var scope = new NuGetVersionSources([], [new string('a', 2 * 1024 * 1024)]);
        NuGetVersionRequestPackage[] requests = [new("Owner", ["1.0.0"], [scope])];

        // Act
        var results = await NuGetVersionCatalog.Read(Path.GetTempPath(), "11.0.100", requests, TestContext.Current.CancellationToken);

        // Assert
        var result = results.ShouldHaveSingleItem();
        result.Status.ShouldBe("inputLimitReached");
        result.Versions.ShouldBeEmpty();
    }
}
