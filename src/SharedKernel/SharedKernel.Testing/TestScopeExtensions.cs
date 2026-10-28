namespace PandaBear.Testing;

/// <summary>
/// Maps <see cref="TestScope" /> to its <c>Scope</c> trait value.
/// </summary>
public static class TestScopeExtensions
{
    /// <summary>
    /// Gets the <c>Scope</c> trait value for <paramref name="scope" />.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="scope" /> is not a defined value.</exception>
    public static string ToTraitValue(this TestScope scope) => scope switch
    {
        TestScope.Unit => "unit",
        TestScope.Behavior => "behavior",
        TestScope.Component => "component",
        TestScope.Contract => "contract",
        TestScope.UiIntegration => "ui-integration",
        TestScope.Integration => "integration",
        TestScope.System => "system",
        TestScope.Architecture => "architecture",
        _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, "Undefined test scope."),
    };
}
