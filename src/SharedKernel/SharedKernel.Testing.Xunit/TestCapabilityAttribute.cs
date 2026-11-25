using Xunit.v3;

namespace SharedKernel.Testing;

/// <summary>
/// Declares a named behavior a test exercises. Emits the <c>Capability</c> trait, like <c>[Trait("Capability", ...)]</c>.
/// Values are repository-specific. Define each as one constant and reuse it.
/// </summary>
/// <param name="value">The declared capability value.</param>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method)]
public sealed class TestCapabilityAttribute(string value) : Attribute, ITraitAttribute
{
    /// <summary>
    /// Gets the declared value.
    /// </summary>
    public string Value { get; } = !string.IsNullOrWhiteSpace(value)
        ? value
        : throw new ArgumentException("Value must not be empty or whitespace.", nameof(value));

    /// <inheritdoc />
    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits() =>
        [new KeyValuePair<string, string>(TestTraitNames.CapabilityName, Value)];
}
