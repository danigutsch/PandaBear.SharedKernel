using Xunit.v3;

namespace PandaBear.Testing;

/// <summary>
/// Declares the bounded context or product area a test belongs to. Emits the <c>Area</c> trait, like <c>[Trait("Area", ...)]</c>.
/// Values are repository-specific; define each as one constant and reuse it.
/// </summary>
/// <param name="value">The trait value, for example a kebab-case name.</param>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method)]
public sealed class TestAreaAttribute(string value) : Attribute, ITraitAttribute
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
        [new KeyValuePair<string, string>(TestTraitNames.AreaName, Value)];
}
