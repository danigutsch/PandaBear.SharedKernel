namespace SharedKernel.ApiVersioning.Tests;

/// <summary>
/// Verifies API version value behavior.
/// </summary>
public sealed class ApiVersionTests
{
    [Theory]
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
    [InlineData("")]
    [InlineData("v")]
    [InlineData("1.2.3")]
    [InlineData("one")]
    [InlineData("1.-1")]
    public void Rejects_invalid_version_text(string value)
    {
        // Act
        bool parsed = ApiVersion.TryParse(value, out ApiVersion version);

        // Assert
        parsed.ShouldBeFalse();
        version.ShouldBe(default);
    }
}
