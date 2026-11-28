using static SharedKernel.Packages.Tests.PackageFactory;

namespace SharedKernel.Packages.Tests;

/// <summary>Tests the immutable set of package requirements.</summary>
[Trait(TestTraitNames.CapabilityName, "Packages")]
public sealed class PackageSetTests
{
    [Fact]
    public void A_set_snapshots_its_input_and_compares_requirements_in_name_order()
    {
        // Arrange
        var first = Require("curl", "8.2");
        var second = Require("git");
        PackageRequirement[] source = [second, first];

        // Act
        var set = PackageSet.Create(source).ShouldBeSuccess();
        var reordered = PackageSet.Create([first, second]).ShouldBeSuccess();
        source[0] = Require("vim");

        // Assert
        set.Requirements.ShouldBe([first, second]);
        set.ShouldBe(reordered);
        set.GetHashCode().ShouldBe(reordered.GetHashCode());
    }

    [Fact]
    public void The_same_name_at_two_versions_is_not_a_valid_set()
    {
        // Arrange
        PackageRequirement[] requirements = [Require("git", "1.0"), Require("git", "2.0")];

        // Act
        var result = PackageSet.Create(requirements, "fixture");

        // Assert
        result.IsFailure.ShouldBeTrue();
        var messages = result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull();
        messages["fixture"].ShouldHaveSingleItem().ShouldContain("git", StringComparison.Ordinal);
    }

    [Fact]
    public void An_unlisted_manager_has_an_empty_package_set()
    {
        // Arrange
        var declaration = Declared((PackageManagerKind.Apt, ["git", "curl"]));

        // Act
        var expected = PackageSet.Create([Require("curl"), Require("git")]).ShouldBeSuccess();
        var absent = declaration.SetFor(PackageManagerKind.Brew);

        // Assert
        declaration.SetFor(PackageManagerKind.Apt).ShouldBe(expected);
        absent.ShouldBe(PackageSet.Empty);
        absent.Requirements.ShouldBeEmpty();
    }
}
