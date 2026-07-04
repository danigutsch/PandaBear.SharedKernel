using System.Diagnostics;
using System.Reflection;

namespace SharedKernel.Versioning.Tests;

[Trait(Testing.SharedKernelTestTraitNames.CapabilityName, "Versioning")]
public static class VersionMetadataTests
{
    [Fact]
    public static void Stamps_assembly_metadata_from_central_version_properties()
    {
        // Arrange
        var assembly = typeof(SemanticVersion).Assembly;

        // Act
        var assemblyNameVersion = assembly.GetName().Version?.ToString();
        var fileVersion = FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion;
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        // Assert
        assemblyNameVersion.ShouldBe("0.0.0.0");
        fileVersion.ShouldBe("0.1.0.0");
        informationalVersion.ShouldNotBeNull();
        informationalVersion.StartsWith("0.1.0-alpha.0", StringComparison.Ordinal).ShouldBeTrue();
    }
}
