namespace SharedKernel.ApiVersioning.Tests;

/// <summary>
/// Verifies API deprecation policy helpers.
/// </summary>
public sealed class ApiDeprecationPolicyExtensionsTests
{
    [Fact]
    public void Detects_present_deprecation_policy()
    {
        // Arrange
        var definition = new ApiVersionDefinition(new ApiVersion(1), ApiVersionStatus.Deprecated, new ApiDeprecationPolicy());

        // Act
        bool hasPolicy = definition.HasDeprecationPolicy();

        // Assert
        hasPolicy.ShouldBeTrue();
    }

    [Fact]
    public void Detects_sunset_date_on_or_before_reference_date()
    {
        // Arrange
        var definition = new ApiVersionDefinition(new ApiVersion(1), ApiVersionStatus.Deprecated, new ApiDeprecationPolicy(SunsetOn: new DateOnly(2026, 1, 1)));

        // Act
        bool hasSunset = definition.HasSunsetOnOrBefore(new DateOnly(2026, 1, 2));

        // Assert
        hasSunset.ShouldBeTrue();
    }
}
