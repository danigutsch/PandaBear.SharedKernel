namespace SharedKernel.Versioning.Tests;

[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, "Versioning")]
public static class SemanticVersionTests
{
    [Fact]
    public static void Parses_semantic_version_with_prerelease_and_metadata()
    {
        // Arrange
        const string value = "1.2.3-alpha.1+sha.abc123";

        // Act
        var version = SemanticVersion.Parse(value);

        // Assert
        version.Major.ShouldBe(1);
        version.Minor.ShouldBe(2);
        version.Patch.ShouldBe(3);
        version.Prerelease.ShouldBe("alpha.1");
        version.BuildMetadata.ShouldBe("sha.abc123");
    }

    [Theory]
    [InlineData(ReleaseImpact.None, "1.2.3")]
    [InlineData(ReleaseImpact.Patch, "1.2.4")]
    [InlineData(ReleaseImpact.Minor, "1.3.0")]
    [InlineData(ReleaseImpact.Major, "2.0.0")]
    public static void Applies_release_impact_to_stable_version_core(ReleaseImpact impact, string expectedVersion)
    {
        // Arrange
        var version = new SemanticVersion(1, 2, 3, "alpha.1", "sha.abc123");

        // Act
        var nextVersion = version.Bump(impact);

        // Assert
        nextVersion.ToString().ShouldBe(expectedVersion);
    }
}
