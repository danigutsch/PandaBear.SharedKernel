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

    /// <summary>Maps a kebab-case scope value such as <c>ui-integration</c> to its enum member name <c>UiIntegration</c>.</summary>
    public static string ToMemberName(string scope)
    {
        var parts = scope.Split(['-'], StringSplitOptions.RemoveEmptyEntries);

        return string.Concat(parts.Select(static part => char.ToUpperInvariant(part[0]) + part.Substring(1)));
    }
}
