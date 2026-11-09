using PandaBear.Results;

namespace PandaBear.Packages.Tests;

/// <summary>Tests the mapping from package managers to OSV ecosystems.</summary>
[Trait("Capability", "Packages")]
public sealed class OsvEcosystemsTests
{
    [Theory]
    [InlineData(PackageManagerKind.Cargo, null, "crates.io")]
    [InlineData(PackageManagerKind.Npm, null, "npm")]
    [InlineData(PackageManagerKind.Apt, "Debian:12", "Debian:12")]
    [InlineData(PackageManagerKind.Apt, "Ubuntu:24.04:LTS", "Ubuntu:24.04:LTS")]
    [InlineData(PackageManagerKind.Apt, "Ubuntu:Pro:18.04:LTS", "Ubuntu:Pro:18.04:LTS")]
    [InlineData(PackageManagerKind.Apt, "Debian:sid", "Debian:sid")]
    public void A_supported_manager_maps_to_its_osv_ecosystem(PackageManagerKind manager, string? release, string expected)
    {
        // Act
        var result = OsvEcosystems.For(manager, release);

        // Assert
        result.ShouldBeSuccess().ShouldBe(expected);
    }

    [Theory]
    [InlineData(PackageManagerKind.Apt, null)]
    [InlineData(PackageManagerKind.Apt, "")]
    [InlineData(PackageManagerKind.Apt, "Debian")]
    [InlineData(PackageManagerKind.Apt, "Debian:12:extra")]
    [InlineData(PackageManagerKind.Apt, "Fedora:40")]
    [InlineData(PackageManagerKind.Apt, "debian:12")]
    [InlineData(PackageManagerKind.Apt, "Debian:12/../x")]
    [InlineData(PackageManagerKind.Apt, "Debian:")]
    [InlineData(PackageManagerKind.Flatpak, null)]
    [InlineData((PackageManagerKind)99, null)]
    public void An_unsupported_manager_or_apt_release_is_an_invalid_result(PackageManagerKind manager, string? release)
    {
        // Act
        var result = OsvEcosystems.For(manager, release);

        // Assert
        result.ShouldBeFailure(ResultStatus.Invalid, "invalid");
    }

    [Fact]
    public void An_overlong_apt_release_is_invalid()
    {
        // Act
        var result = OsvEcosystems.For(PackageManagerKind.Apt, "Debian:" + new string('1', 80));

        // Assert
        result.ShouldBeFailure(ResultStatus.Invalid, "invalid");
    }
}
