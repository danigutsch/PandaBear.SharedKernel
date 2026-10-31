using System.Diagnostics.CodeAnalysis;

namespace PandaBear.Results;

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
    /// <exception cref="ArgumentNullException">When <paramref name="providerType"/> is <see langword="null"/>.</exception>
    public ResultErrorCatalogProviderAttribute(
        [DynamicallyAccessedMembers(
            DynamicallyAccessedMemberTypes.PublicParameterlessConstructor |
            DynamicallyAccessedMemberTypes.PublicProperties)] Type providerType)
    {
        ArgumentNullException.ThrowIfNull(providerType);

        ProviderType = providerType;
    }

    /// <summary>
    /// Gets the generated provider type for the assembly.
    /// </summary>
    [DynamicallyAccessedMembers(
        DynamicallyAccessedMemberTypes.PublicParameterlessConstructor |
        DynamicallyAccessedMemberTypes.PublicProperties)]
    public Type ProviderType
    {
        get;
    }
}
