namespace SharedKernel.ApiVersioning;

/// <summary>
/// Provides helpers for API deprecation policy metadata.
/// </summary>
public static class ApiDeprecationPolicyExtensions
{
    /// <summary>
    /// Determines whether the API version has deprecation metadata.
    /// </summary>
    /// <param name="definition">The API version definition.</param>
    /// <returns><see langword="true"/> when deprecation metadata exists; otherwise, <see langword="false"/>.</returns>
    public static bool HasDeprecationPolicy(this ApiVersionDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return definition.Deprecation is not null;
    }

    /// <summary>
    /// Determines whether the API version sunset date is on or before the supplied date.
    /// </summary>
    /// <param name="definition">The API version definition.</param>
    /// <param name="date">The date to compare.</param>
    /// <returns><see langword="true"/> when the sunset date has passed; otherwise, <see langword="false"/>.</returns>
    public static bool HasSunsetOnOrBefore(this ApiVersionDefinition definition, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return definition.Deprecation?.SunsetOn is DateOnly sunsetOn && sunsetOn <= date;
    }
}
