namespace PandaBear.Testing;

/// <summary>
/// Maps <see cref="TestScope" /> to its <c>Scope</c> trait value.
/// </summary>
public static class TestScopeExtensions
{
    /// <summary>
    /// Gets the <c>Scope</c> trait value for <paramref name="scope" />, the kebab-case member name.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="scope" /> is not a defined value.</exception>
    public static string ToTraitValue(this TestScope scope) => TraitValue.Of(scope, nameof(scope));
}
