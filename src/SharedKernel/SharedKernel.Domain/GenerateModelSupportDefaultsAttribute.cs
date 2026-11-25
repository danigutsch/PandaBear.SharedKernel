namespace SharedKernel.Domain;

/// <summary>
/// Configures assembly-level defaults for generated model support.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class GenerateModelSupportDefaultsAttribute : Attribute
{
    /// <summary>
    /// Gets or sets a value indicating whether identity/equality support is generated for identified models by default.
    /// </summary>
    public bool Identity { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether parsing support is generated for annotated value objects by default.
    /// </summary>
    public bool ValueObjectParsing { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether JSON support is generated for annotated value objects by default.
    /// </summary>
    public bool ValueObjectJson { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether EF Core support is generated for annotated value objects by default.
    /// </summary>
    public bool ValueObjectEfCore { get; set; }
}
