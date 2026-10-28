using System.Collections.Immutable;

namespace PandaBear.Testing.Analyzers;

/// <summary>
/// The <c>Scope</c> trait values of <c>PandaBear.Testing.TestScope</c>, in enum order. The analyzer cannot reference the enum.
/// </summary>
internal static class TestScopeValues
{
    public static ImmutableArray<string> All
    {
        get;
    } =
    [
        "unit",
        "behavior",
        "component",
        "contract",
        "ui-integration",
        "integration",
        "system",
        "architecture",
    ];
}
