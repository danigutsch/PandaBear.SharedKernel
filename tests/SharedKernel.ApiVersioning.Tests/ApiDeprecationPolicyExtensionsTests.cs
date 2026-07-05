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
    public void Detects_missing_deprecation_policy()
    {
        // Arrange
        var definition = new ApiVersionDefinition(new ApiVersion(1));

        // Act
        bool hasPolicy = definition.HasDeprecationPolicy();

        // Assert
        hasPolicy.ShouldBeFalse();
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

    [Fact]
    public void Returns_false_when_sunset_date_is_after_reference_date()
    {
        // Arrange
        var definition = new ApiVersionDefinition(new ApiVersion(1), ApiVersionStatus.Deprecated, new ApiDeprecationPolicy(SunsetOn: new DateOnly(2026, 1, 3)));

        // Act
        bool hasSunset = definition.HasSunsetOnOrBefore(new DateOnly(2026, 1, 2));

        // Assert
        hasSunset.ShouldBeFalse();
    }

    [Fact]
    public void Returns_false_when_sunset_date_is_missing()
    {
        // Arrange
        var definition = new ApiVersionDefinition(new ApiVersion(1), ApiVersionStatus.Deprecated, new ApiDeprecationPolicy());

        // Act
        bool hasSunset = definition.HasSunsetOnOrBefore(new DateOnly(2026, 1, 2));

        // Assert
        hasSunset.ShouldBeFalse();
    }

    [Fact]
    public void Returns_false_when_deprecation_policy_is_missing()
    {
        // Arrange
        var definition = new ApiVersionDefinition(new ApiVersion(1));

        // Act
        bool hasSunset = definition.HasSunsetOnOrBefore(new DateOnly(2026, 1, 2));

        // Assert
        hasSunset.ShouldBeFalse();
    }

    [Fact]
    public void Rejects_null_version_definition_for_policy_check()
    {
        // Arrange
        ApiVersionDefinition? definition = null;

        // Act
        Action action = () => _ = definition!.HasDeprecationPolicy();

        // Assert
        action.ShouldThrow<ArgumentNullException>();
    }

    [Fact]
    public void Rejects_null_version_definition_for_sunset_check()
    {
        // Arrange
        ApiVersionDefinition? definition = null;

        // Act
        Action action = () => _ = definition!.HasSunsetOnOrBefore(new DateOnly(2026, 1, 2));

        // Assert
        action.ShouldThrow<ArgumentNullException>();
    }
}
