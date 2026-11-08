namespace PandaBear.Packages.Tests;

/// <summary>Tests the stable text IDs of the package managers.</summary>
[Trait("Capability", "Packages")]
public sealed class PackageManagersTests
{
    [Theory]
    [InlineData(PackageManagerKind.Apt, "apt")]
    [InlineData(PackageManagerKind.Cargo, "cargo")]
    [InlineData(PackageManagerKind.Npm, "npm")]
    [InlineData(PackageManagerKind.Flatpak, "flatpak")]
    public void Each_manager_has_its_id_and_the_id_parses_back_to_it(PackageManagerKind manager, string id)
    {
        // Act
        var parsed = PackageManagers.TryParse(id, out var found);

        // Assert
        PackageManagers.Id(manager).ShouldBe(id);
        parsed.ShouldBeTrue();
        found.ShouldBe(manager);
    }

    [Fact]
    public void The_list_of_ids_covers_every_defined_manager_in_enum_order()
    {
        // Act
        var ids = Enum.GetValues<PackageManagerKind>().Select(PackageManagers.Id).ToArray();

        // Assert
        PackageManagers.Ids.ShouldBe(ids);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("APT")]
    [InlineData(" apt")]
    [InlineData("pip")]
    public void An_unknown_or_differently_written_id_does_not_parse(string? id)
    {
        // Act
        var parsed = PackageManagers.TryParse(id, out var found);

        // Assert
        parsed.ShouldBeFalse("Ids are compared exactly.");
        found.ShouldBeNull();
    }

    [Fact]
    public void An_undefined_manager_has_no_id()
    {
        // Act
        Func<object?> act = () => PackageManagers.Id((PackageManagerKind)99);

        // Assert
        act.ShouldThrow<ArgumentOutOfRangeException>();
    }
}
