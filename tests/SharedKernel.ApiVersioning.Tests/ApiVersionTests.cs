namespace SharedKernel.ApiVersioning.Tests;

/// <summary>
/// Verifies API version value behavior.
/// </summary>
public sealed class ApiVersionTests
{
    [Theory]
    [InlineData("  v1.2  ", 1, 2, "v1.2", "1.2")]
    [InlineData("1", 1, 0, "v1", "1.0")]
    [InlineData("v1", 1, 0, "v1", "1.0")]
    [InlineData("V2.3", 2, 3, "v2.3", "2.3")]
    public void Parses_supported_version_text(string value, int expectedMajor, int expectedMinor, string expectedRouteSegment, string expectedText)
    {
        // Act
        ApiVersion version = ApiVersion.Parse(value);

        // Assert
        version.Major.ShouldBe(expectedMajor);
        version.Minor.ShouldBe(expectedMinor);
        version.RouteSegment.ShouldBe(expectedRouteSegment);
        version.ToString().ShouldBe(expectedText);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("v")]
    [InlineData("v1.")]
    [InlineData(".1")]
    [InlineData("1.2.3")]
    [InlineData("one")]
    [InlineData("1.-1")]
    public void Rejects_invalid_version_text(string? value)
    {
        // Act
        bool parsed = ApiVersion.TryParse(value, out ApiVersion version);

        // Assert
        parsed.ShouldBeFalse();
        version.ShouldBe(default);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(1, -1)]
    public void Rejects_negative_version_components(int major, int minor)
    {
        // Act
        Action action = () => _ = new ApiVersion(major, minor);

        // Assert
        action.ShouldThrow<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Parse_rejects_null_version_text()
    {
        // Act
        Action action = () => _ = ApiVersion.Parse(null!);

        // Assert
        action.ShouldThrow<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_rejects_blank_version_text(string value)
    {
        // Act
        Action action = () => _ = ApiVersion.Parse(value!);

        // Assert
        action.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Parse_rejects_invalid_version_text()
    {
        // Act
        Action action = () => _ = ApiVersion.Parse("one");

        // Assert
        action.ShouldThrow<ArgumentException>();
    }

    [Fact]
    public void Compares_versions_by_major_then_minor()
    {
        // Arrange
        var version1 = new ApiVersion(1);
        var version1Point1 = new ApiVersion(1, 1);
        var version2 = new ApiVersion(2);

        // Assert
        version1.ShouldBeLessThan(version1Point1);
        version1Point1.ShouldBeLessThanOrEqualTo(version2);
        version2.ShouldBeGreaterThan(version1Point1);
        version2.ShouldBeGreaterThanOrEqualTo(version1Point1);
    }
}
