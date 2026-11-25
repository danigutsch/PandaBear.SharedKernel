using System.Xml.Linq;

namespace PandaBear.Versioning.Tests;

/// <summary>
/// Tests reading the central package versions that a packed consumer of one target framework uses.
/// </summary>
[Trait(TestTraitNames.CapabilityName, TestTraits.VersioningCapability)]
public static class CentralPackageVersionsTests
{
    private const string Central = """
        <Project>
          <ItemGroup>
            <PackageVersion Include="Microsoft.Extensions.Options" Version="10.0.15" />
            <PackageVersion Include="Npgsql" Version="10.0.3" />
          </ItemGroup>
          <ItemGroup Condition="'$(TargetFramework)' == 'net11.0'">
            <PackageVersion Update="Microsoft.Extensions.Options" Version="11.0.0" />
          </ItemGroup>
        </Project>
        """;

    [Fact]
    public static void Read_uses_the_included_versions_for_a_framework_without_a_group()
    {
        // Act
        var versions = CentralPackageVersions.Read(XDocument.Parse(Central), "net10.0");

        // Assert
        versions["Microsoft.Extensions.Options"].ShouldBe("10.0.15");
        versions["Npgsql"].ShouldBe("10.0.3");
        versions.Count.ShouldBe(2);
    }

    [Fact]
    public static void Read_applies_the_updates_of_the_matching_framework_group()
    {
        // Act
        var versions = CentralPackageVersions.Read(XDocument.Parse(Central), "net11.0");

        // Assert
        versions["Microsoft.Extensions.Options"].ShouldBe("11.0.0");
        versions["Npgsql"].ShouldBe("10.0.3");
    }

    [Fact]
    public static void Read_rejects_a_condition_it_cannot_evaluate()
    {
        // Arrange
        var document = XDocument.Parse("""
            <Project>
              <ItemGroup Condition="Exists('custom.props')">
                <PackageVersion Include="Npgsql" Version="10.0.3" />
              </ItemGroup>
            </Project>
            """);

        // Act
        Action act = () => CentralPackageVersions.Read(document, "net10.0");

        // Assert
        act.ShouldThrow<InvalidOperationException>().Message.ShouldBe("Directory.Packages.props has a PackageVersion condition that is not '$(TargetFramework)' == '<framework>': Exists('custom.props')");
    }

    [Fact]
    public static void Read_rejects_an_update_of_a_package_that_no_item_includes()
    {
        // Arrange
        var document = XDocument.Parse("""
            <Project>
              <ItemGroup Condition="'$(TargetFramework)' == 'net11.0'">
                <PackageVersion Update="Npgsql" Version="11.0.0" />
              </ItemGroup>
            </Project>
            """);

        // Act
        Action act = () => CentralPackageVersions.Read(document, "net11.0");

        // Assert
        act.ShouldThrow<InvalidOperationException>().Message.ShouldBe("Directory.Packages.props updates Npgsql, which no PackageVersion includes.");
    }
}
