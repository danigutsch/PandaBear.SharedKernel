namespace SharedKernel.Results;

/// <summary>
/// Marks an assembly as providing a generated centralized error catalog.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class ResultErrorCatalogProviderAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ResultErrorCatalogProviderAttribute"/> class.
    /// </summary>
    /// <param name="providerType">The generated provider type for the assembly.</param>
    public ResultErrorCatalogProviderAttribute(Type providerType)
    {
        ArgumentNullException.ThrowIfNull(providerType);

        ProviderType = providerType;
    }

    /// <summary>
    /// Gets the generated provider type for the assembly.
    /// </summary>
    public Type ProviderType { get; }
}
