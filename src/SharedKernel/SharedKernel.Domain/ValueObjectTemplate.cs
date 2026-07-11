namespace SharedKernel.Domain;

/// <summary>
/// Identifies reusable technical templates for generated scalar value objects.
/// </summary>
public enum ValueObjectTemplate
{
    /// <summary>
    /// No specialized template is applied.
    /// </summary>
    None = 0,

    /// <summary>
    /// Generates API contract version parsing and route-segment formatting.
    /// </summary>
    ApiVersion = 1,

    /// <summary>
    /// Requires a non-empty string value.
    /// </summary>
    NonEmptyString = 2,

    /// <summary>
    /// Requires a lowercase slug made from letters, digits, and hyphens.
    /// </summary>
    Slug = 3,

    /// <summary>
    /// Requires a non-default strongly typed identifier value.
    /// </summary>
    StronglyTypedId = 4,

    /// <summary>
    /// Requires a short alphabetic ISO-style code.
    /// </summary>
    IsoCode = 5,
}
