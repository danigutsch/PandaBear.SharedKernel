namespace SharedKernel.Versioning.Tests;

[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, "Versioning")]
public static class VersionCalculationTests
{
    [Fact]
    public static void Calculates_highest_release_impact_from_commit_history()
    {
        // Arrange
        var baseVersion = new SemanticVersion(0, 1, 0);
        string[] commits =
        [
            "Merge pull request from branch",
            "fix: correct package version",
            "feat(versioning): emit JSON output",
        ];

        // Act
        var output = VersionCalculation.Calculate(baseVersion, commits, "alpha.1", "abc123");

        // Assert
        output.ReleaseImpact.ShouldBe(ReleaseImpact.Minor);
        output.SemVer.ShouldBe("0.2.0-alpha.1");
        output.PackageVersion.ShouldBe("0.2.0-alpha.1");
        output.AssemblyVersion.ShouldBe("0.0.0.0");
        output.FileVersion.ShouldBe("0.2.0.0");
        output.InformationalVersion.ShouldBe("0.2.0-alpha.1+sha.abc123");
    }

    [Fact]
    public static void Calculates_major_release_for_breaking_commit()
    {
        // Arrange
        var baseVersion = new SemanticVersion(1, 4, 2);
        string[] commits = ["fix: correct bug", "feat(api)!: remove old route"];

        // Act
        var output = VersionCalculation.Calculate(baseVersion, commits);

        // Assert
        output.ReleaseImpact.ShouldBe(ReleaseImpact.Major);
        output.SemVer.ShouldBe("2.0.0");
        output.AssemblyVersion.ShouldBe("2.0.0.0");
    }

    [Fact]
    public static void Appends_sha_to_existing_build_metadata()
    {
        // Arrange
        var version = new SemanticVersion(1, 2, 3, BuildMetadata: "build.7");

        // Act
        var output = VersionOutput.Create(version, ReleaseImpact.Patch, "abc123");

        // Assert
        output.SemVer.ShouldBe("1.2.3+build.7");
        output.InformationalVersion.ShouldBe("1.2.3+build.7.sha.abc123");
    }
}
