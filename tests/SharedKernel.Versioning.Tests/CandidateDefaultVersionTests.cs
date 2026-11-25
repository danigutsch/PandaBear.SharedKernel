namespace SharedKernel.Versioning.Tests;

/// <summary>
/// Tests the default local candidate version, which must stay short enough for NuGet's package path limit.
/// </summary>
public static class CandidateDefaultVersionTests
{
    // NuGet fails a pack with NU5123 when package id, version and file path together exceed about 200 characters.
    // The longest library packages already spend about 148 characters on id and file path, which leaves about 52
    // characters for the version; 48 keeps a margin for slightly longer package or file names.
    private const int MaxDefaultVersionLength = 48;

    private const string Revision = "e7a988df258a8a6c1f4b5d7e9c0a2b4c6d8e0f12";

    [Fact]
    public static void Default_version_uses_the_version_core_second_resolution_time_and_short_revision()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 11, 14, 19, 25, 37, 365, TimeSpan.Zero);

        // Act
        var version = CandidateValidator.DefaultVersion("0.1.0-alpha.1", now, Revision);

        // Assert
        version.ShouldBe("0.1.0-alpha.local.t20261114192537.ge7a988d");
    }

    [Fact]
    public static void Default_version_converts_the_time_to_UTC()
    {
        // Arrange
        var now = new DateTimeOffset(2026, 11, 14, 20, 25, 37, TimeSpan.FromHours(1));

        // Act
        var version = CandidateValidator.DefaultVersion("0.1.0", now, Revision);

        // Assert
        version.ShouldBe("0.1.0-alpha.local.t20261114192537.ge7a988d");
    }

    [Fact]
    public static void Default_version_stays_within_the_package_path_budget()
    {
        // Arrange
        var now = new DateTimeOffset(9999, 12, 31, 23, 59, 59, 999, TimeSpan.Zero);

        // Act
        var version = CandidateValidator.DefaultVersion("10.10.10-preview.1", now, Revision);

        // Assert
        version.Length.ShouldBeLessThanOrEqualTo(MaxDefaultVersionLength);
    }
}
