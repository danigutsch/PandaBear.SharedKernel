using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace PandaBear.Testing.SourceGenerator;

/// <summary>The naming rules of generated members and types.</summary>
internal static class ProxyNaming
{
    /// <summary>The members of <c>CallHooks&lt;T&gt;</c> that a hooks member of the same name could not be reached past.</summary>
    public static readonly string[] ReservedHookNames = ["Equals", "Generated", "GetHashCode", "GetType", "ReferenceEquals", "ToString"];

    /// <summary>The inherited members that a call property of the same name would hide.</summary>
    public static readonly string[] ReservedPropertyNames = ["Equals", "Finalize", "GetHashCode", "GetType", "MemberwiseClone", "ReferenceEquals", "ToString"];

    /// <summary>Escapes an identifier that is a C# keyword or contextual keyword.</summary>
    /// <param name="name">The identifier.</param>
    /// <returns>The identifier, prefixed with <c>@</c> when needed.</returns>
    public static string Escape(string name) =>
        SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None || SyntaxFacts.GetContextualKeywordKind(name) != SyntaxKind.None
            ? "@" + name
            : name;

    /// <summary>Turns a fully qualified type name into an identifier fragment: every run of other characters becomes one underscore.</summary>
    /// <param name="text">The type name.</param>
    /// <returns>The fragment.</returns>
    public static string Sanitize(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (char.IsLetterOrDigit(character) || character == '_')
            {
                builder.Append(character);
            }
            else if (builder.Length > 0 && builder[builder.Length - 1] != '_')
            {
                builder.Append('_');
            }
        }

        return builder.ToString().TrimEnd('_');
    }

    /// <summary>Upper-cases the first letter of a name.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The name in Pascal case.</returns>
    public static string Pascal(string name) =>
        name.Length > 0 && char.IsLower(name[0]) ? char.ToUpperInvariant(name[0]) + name.Substring(1) : name;

    /// <summary>Names a parameter type in an overload suffix, such as <c>Int32</c>, <c>StringArray</c> or <c>ListGuid</c>.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The type token.</returns>
    public static string TypeToken(ITypeSymbol type) => type switch
    {
        IArrayTypeSymbol array => TypeToken(array.ElementType) + "Array",
        INamedTypeSymbol { IsGenericType: true } named => Pascal(named.Name) + string.Concat(named.TypeArguments.Select(TypeToken)),
        _ => Pascal(type.Name),
    };

    /// <summary>Returns a name that is not used yet and marks it used: a taken name gets <c>_2</c>, <c>_3</c> and so on.</summary>
    /// <param name="preferred">The preferred name.</param>
    /// <param name="used">The names already used.</param>
    /// <returns>The unique name.</returns>
    public static string Unique(string preferred, HashSet<string> used)
    {
        var name = preferred;
        for (var number = 2; !used.Add(name); number++)
        {
            name = preferred + "_" + number.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return name;
    }
}
