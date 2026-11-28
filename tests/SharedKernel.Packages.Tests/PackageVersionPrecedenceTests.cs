using static SharedKernel.Packages.Tests.PackageFactory;

namespace SharedKernel.Packages.Tests;

/// <summary>Verifies the source's SemVer precedence and its unknown-order behavior.</summary>
[Trait(TestTraitNames.CapabilityName, "Packages")]
public sealed class PackageVersionPrecedenceTests
{
    [Theory]
    [InlineData("1.1.0", "1.10.0", -1)]
    [InlineData("2.0.0", "1.999999999999999999999.0", 1)]
    [InlineData("999999999999999999999.0.0", "999999999999999999998.0.0", 1)]
    [InlineData("1.2.3+one", "1.2.3+two", 0)]
    [InlineData("1.2.3-rc.1", "1.2.3", -1)]
    [InlineData("1.2.3-beta.2", "1.2.3-beta.11", -1)]
    [InlineData("1.2.3-beta.2", "1.2.3-beta.alpha", -1)]
    [InlineData("1.2.3-beta.alpha", "1.2.3-beta.2", 1)]
    [InlineData("1.2.3-beta.alpha", "1.2.3-beta.beta", -1)]
    [InlineData("1.2.3-beta", "1.2.3-beta.2", -1)]
    [InlineData("01.2.3", "1.2.3", null)]
    [InlineData("1.2.3-beta.01", "1.2.3", null)]
    [InlineData("1.2", "1.2.3", null)]
    [InlineData("1.2.3+", "1.2.3", null)]
    public void SemVer_precedence_ignores_build_metadata_and_rejects_unknown_formats(string left, string right, int? expected)
    {
        // Arrange
        var first = Version(left);
        var second = Version(right);

        // Act
        var order = PackageVersionPrecedence.Compare(PackageManagerKind.Cargo, first, second);
        var direction = order is { } value ? Math.Sign(value) : (int?)null;

        // Assert
        direction.ShouldBe(expected);
    }
}
