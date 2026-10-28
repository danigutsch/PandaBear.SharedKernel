using Xunit.v3;

namespace PandaBear.Testing;

/// <summary>
/// Declares what the test verifies. Emits the <c>Focus</c> trait, like <c>[Trait("Focus", ...)]</c>.
/// </summary>
/// <param name="focus">Declares what the test verifies.</param>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class TestFocusAttribute(TestFocus focus) : Attribute, ITraitAttribute
{
    private readonly string value = focus.ToTraitValue();

    /// <summary>
    /// Gets the declared value.
    /// </summary>
    public TestFocus Focus { get; } = focus;

    /// <inheritdoc />
    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits() =>
        [new KeyValuePair<string, string>(TestTraitNames.FocusName, value)];
}
