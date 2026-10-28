using Xunit.v3;

namespace PandaBear.Testing;

/// <summary>
/// Declares the surface a test exercises, such as an API description or a transport. Emits the <c>Surface</c> trait, like <c>[Trait("Surface", ...)]</c>.
/// Values are repository-specific; define each as one constant and reuse it.
/// </summary>
/// <param name="value">The trait value, for example a kebab-case name.</param>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method)]
public sealed class TestSurfaceAttribute(string value) : Attribute, ITraitAttribute
{
    /// <summary>
    /// Gets the declared value.
    /// </summary>
    public string Value
    {
        get;
    } = !string.IsNullOrWhiteSpace(value)
        ? value
        : throw new ArgumentException("Value must not be empty or whitespace.", nameof(value));

    /// <inheritdoc />
    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits() =>
        [new KeyValuePair<string, string>(TestTraitNames.SurfaceName, Value)];
}
