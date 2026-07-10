namespace SharedKernel.Domain;

/// <summary>
/// Requests generated support code for a scalar value object.
/// </summary>
[AttributeUsage(AttributeTargets.Struct)]
public sealed class GenerateValueObjectAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the scalar type stored by the generated value object.
    /// </summary>
    public Type? UnderlyingType { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether parse helpers should be generated.
    /// </summary>
    public bool Parsing { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a System.Text.Json converter should be generated.
    /// </summary>
    public bool Json { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an EF Core value converter should be generated.
    /// </summary>
    public bool EfCore { get; set; }

    /// <summary>
    /// Gets or sets the reusable technical template applied to the generated value object.
    /// </summary>
    public ValueObjectTemplate Template { get; set; }
}
