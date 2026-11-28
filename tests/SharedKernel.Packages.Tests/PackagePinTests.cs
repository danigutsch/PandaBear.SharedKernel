using static SharedKernel.Packages.Tests.PackageFactory;

namespace SharedKernel.Packages.Tests;

/// <summary>Tests validation and equality of exact package artifact pins.</summary>
[Trait(TestTraitNames.CapabilityName, "Packages")]
public sealed class PackagePinTests
{
    [Theory]
    [InlineData("sha256", 64)]
    [InlineData("sha512", 128)]
    public void A_pin_keeps_the_version_and_canonicalizes_the_digest(string algorithm, int length)
    {
        // Arrange
        var version = Version("1:2.3-4");
        var mixedCase = $"{algorithm}:{new string('F', length)}";
        var lowercase = $"{algorithm}:{new string('f', length)}";

        // Act
        var pin = PackagePin.Create(version, mixedCase).ShouldBeSuccess();
        var equivalent = PackagePin.Create(version, lowercase).ShouldBeSuccess();

        // Assert
        pin.Version.ShouldBe(version);
        pin.Checksum.ShouldBe(lowercase);
        pin.ShouldBe(equivalent);
        pin.GetHashCode().ShouldBe(equivalent.GetHashCode());
    }

    [Theory]
    [InlineData("md5", 32, 'a')]
    [InlineData("sha256", 63, 'a')]
    [InlineData("sha256", 65, 'a')]
    [InlineData("sha256", 64, 'g')]
    [InlineData("sha256", 64, ' ')]
    [InlineData("sha512", 127, 'b')]
    public void Unsupported_or_malformed_checksums_are_rejected(string algorithm, int length, char character)
    {
        // Arrange
        var checksum = $"{algorithm}:{new string(character, length)}";

        // Act
        var result = PackagePin.Create(Version("1.0"), checksum);

        // Assert
        result.IsFailure.ShouldBeTrue();
        var messages = result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull();
        messages["checksum"].ShouldHaveSingleItem().ShouldNotContain(checksum, StringComparison.Ordinal);
    }

    [Fact]
    public void A_pin_requires_both_values()
    {
        // Arrange
        var checksum = $"sha256:{new string('a', 64)}";

        // Act
        var missingVersion = PackagePin.Create(null, checksum);
        var missingChecksum = PackagePin.Create(Version("1.0"), null);

        // Assert
        missingVersion.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()["version"].ShouldHaveSingleItem();
        missingChecksum.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()["checksum"].ShouldHaveSingleItem();
    }

    [Fact]
    public void A_different_artifact_version_or_digest_changes_the_requirement_identity()
    {
        // Arrange
        var name = Name("git");
        var version = Version("1.0");
        var checksum = $"sha256:{new string('a', 64)}";
        var otherChecksum = $"sha256:{new string('b', 64)}";

        // Act
        var requirement = PackageRequirement.Pinned(name, PackagePin.Create(version, checksum).ShouldBeSuccess());
        var otherVersion = PackageRequirement.Pinned(name, PackagePin.Create(Version("1.1"), checksum).ShouldBeSuccess());
        var otherArtifact = PackageRequirement.Pinned(name, PackagePin.Create(version, otherChecksum).ShouldBeSuccess());

        // Assert
        requirement.Version.ShouldBe(version);
        requirement.Pin.ShouldNotBeNull().Checksum.ShouldBe(checksum);
        requirement.ShouldNotBe(otherVersion);
        requirement.ShouldNotBe(otherArtifact);
        requirement.ShouldNotBe(new PackageRequirement(name, version));
    }
}
