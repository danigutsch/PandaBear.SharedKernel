using Xunit.v3;

namespace PandaBear.Testing;

/// <summary>
/// Declares the test seam a test exercises. Emits the same <c>Scope</c> trait as <c>[Trait("Scope", ...)]</c>,
/// so <c>--filter-trait</c> and reports treat both forms alike.
/// </summary>
/// <param name="scope">The seam the test exercises.</param>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method)]
public sealed class TestScopeAttribute(TestScope scope) : Attribute, ITraitAttribute
{
    private readonly string value = scope.ToTraitValue();

    /// <summary>
    /// Gets the declared scope.
    /// </summary>
    public TestScope Scope { get; } = scope;

    /// <inheritdoc />
    public IReadOnlyCollection<KeyValuePair<string, string>> GetTraits() =>
        [new KeyValuePair<string, string>(TestTraitNames.ScopeName, value)];
}
