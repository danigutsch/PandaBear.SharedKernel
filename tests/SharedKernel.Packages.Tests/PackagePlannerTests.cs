using static PandaBear.Packages.Tests.PackageFactory;

namespace PandaBear.Packages.Tests;

/// <summary>Tests the read-only comparison of requirements with installed packages.</summary>
[Trait("Capability", "Packages")]
public sealed class PackagePlannerTests
{
    [Fact]
    public void A_missing_package_is_an_install()
    {
        // Act
        var changes = PackagePlanner.Plan([Require("git")], []);

        // Assert
        changes.ShouldBe([new PackageChange(PackageChangeKind.Install, Require("git"))]);
    }

    [Fact]
    public void An_installed_package_without_a_required_version_is_met()
    {
        // Act
        var changes = PackagePlanner.Plan([Require("git")], [Installed("git", "1:2.43.0")]);

        // Assert
        changes.ShouldBeEmpty();
    }

    [Fact]
    public void An_installed_package_at_the_required_version_is_met()
    {
        // Act
        var changes = PackagePlanner.Plan([Require("git", "2.43.0")], [Installed("git", "2.43.0")]);

        // Assert
        changes.ShouldBeEmpty();
    }

    [Fact]
    public void A_different_installed_version_is_a_version_change_that_names_the_installed_version()
    {
        // Act
        var changes = PackagePlanner.Plan([Require("git", "2.44.0")], [Installed("git", "2.43.0")]);

        // Assert
        changes.ShouldBe([new PackageChange(PackageChangeKind.ChangeVersion, Require("git", "2.44.0"), Version("2.43.0"))]);
    }

    [Fact]
    public void Extra_installed_packages_are_not_differences()
    {
        // Act
        var changes = PackagePlanner.Plan([Require("git")], [Installed("git", "1"), Installed("vim", "9")]);

        // Assert
        changes.ShouldBeEmpty();
    }

    [Fact]
    public void Changes_are_ordered_by_package_name_whatever_the_input_order()
    {
        // Act
        var changes = PackagePlanner.Plan([Require("zsh"), Require("bat"), Require("curl")], []);

        // Assert
        changes.Select(static c => c.Requirement.Name.Value).ShouldBe(["bat", "curl", "zsh"]);
    }

    [Fact]
    public void A_second_plan_after_the_changes_are_applied_is_empty()
    {
        // Arrange
        PackageRequirement[] requirements = [Require("git", "2.43.0"), Require("bat")];
        var first = PackagePlanner.Plan(requirements, [Installed("git", "2.40.0")]);

        // Act
        var second = PackagePlanner.Plan(requirements, [Installed("git", "2.43.0"), Installed("bat", "0.24.0")]);

        // Assert
        first.Count.ShouldBe(2);
        second.ShouldBeEmpty();
    }

    [Fact]
    public void A_package_required_twice_is_rejected()
    {
        // Act
        var act = () => PackagePlanner.Plan([Require("git"), Require("git", "1")], []);

        // Assert
        act.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void A_package_installed_twice_is_rejected()
    {
        // Act
        var act = () => PackagePlanner.Plan([Require("git")], [Installed("git", "1"), Installed("git", "2")]);

        // Assert
        act.ShouldThrow<ArgumentException>();
    }
}
