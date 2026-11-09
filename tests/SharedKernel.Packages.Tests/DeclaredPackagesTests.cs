using static PandaBear.Packages.Tests.PackageFactory;

namespace PandaBear.Packages.Tests;

/// <summary>Tests that a declaration gives every package one owner.</summary>
[Trait("Capability", "Packages")]
public sealed class DeclaredPackagesTests
{
    [Fact]
    public void Packages_under_different_managers_with_different_names_are_accepted_and_ordered()
    {
        // Act
        var result = DeclaredPackages.Create(Declare((PackageManagerKind.Cargo, ["zoxide", "bat"]), (PackageManagerKind.Apt, ["git"])));
        var declared = result.ShouldBeSuccess();

        // Assert
        declared.Managers.ShouldBe([PackageManagerKind.Apt, PackageManagerKind.Cargo]);
        declared.For(PackageManagerKind.Cargo).Select(static r => r.Name.Value).ShouldBe(["bat", "zoxide"]);
        declared.For(PackageManagerKind.Npm).ShouldBeEmpty();
    }

    [Fact]
    public void The_same_name_under_two_managers_is_rejected_and_names_both()
    {
        // Act
        var result = DeclaredPackages.Create(Declare((PackageManagerKind.Apt, ["ripgrep", "git"]), (PackageManagerKind.Cargo, ["ripgrep"])));

        // Assert
        var messages = result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()["packages"];
        messages.ShouldBe(["ripgrep is declared for more than one package manager: apt, cargo."]);
    }

    [Fact]
    public void The_same_name_twice_under_one_manager_is_rejected()
    {
        // Act
        var result = DeclaredPackages.Create(Declare((PackageManagerKind.Apt, ["git", "git"])));

        // Assert
        result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()["packages"].ShouldBe(["git is declared more than once for apt."]);
    }

    [Fact]
    public void Every_overlap_is_reported_at_once_in_name_order()
    {
        // Act
        var result = DeclaredPackages.Create(Declare(
            (PackageManagerKind.Flatpak, ["org.example.App"]),
            (PackageManagerKind.Cargo, ["curl", "bat"]),
            (PackageManagerKind.Apt, ["curl", "bat"])));

        // Assert
        var messages = result.ErrorDetails.ShouldNotBeNull().ValidationErrors.ShouldNotBeNull()["packages"];
        messages.ShouldBe(["bat is declared for more than one package manager: apt, cargo.", "curl is declared for more than one package manager: apt, cargo."]);
    }

    [Fact]
    public void A_version_pin_does_not_make_two_declarations_of_one_name_different()
    {
        // Act
        var result = DeclaredPackages.Create(Declare((PackageManagerKind.Apt, ["git=1"]), (PackageManagerKind.Cargo, ["git=2"])));

        // Assert
        result.IsFailure.ShouldBeTrue("The owner is the name, not the version.");
    }

    [Fact]
    public void An_empty_manager_is_ignored_and_equal_declarations_are_equal_whatever_the_order()
    {
        // Act
        var first = DeclaredPackages.Create(Declare((PackageManagerKind.Apt, ["git", "curl"]), (PackageManagerKind.Npm, []))).Value;
        var second = DeclaredPackages.Create(Declare((PackageManagerKind.Apt, ["curl", "git"]))).Value;

        // Assert
        first.Managers.ShouldBe([PackageManagerKind.Apt]);
        first.ShouldBe(second);
    }

    [Fact]
    public void A_manager_that_is_not_a_defined_value_is_rejected_as_an_argument_error()
    {
        // Act
        Func<object?> act = () => DeclaredPackages.Create(Declare(((PackageManagerKind)99, ["git"])));

        // Assert
        act.ShouldThrow<ArgumentOutOfRangeException>();
    }
}
