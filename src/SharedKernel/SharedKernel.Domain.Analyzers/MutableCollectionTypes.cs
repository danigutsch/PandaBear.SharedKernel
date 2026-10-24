using Microsoft.CodeAnalysis;

namespace PandaBear.Domain.Analyzers;

/// <summary>
/// Resolves the collection types that decide whether an exposed property type is mutable.
/// </summary>
internal sealed class MutableCollectionTypes(Compilation compilation)
{
    private static readonly string[] ReadOnlyNamespaces =
    [
        "System.Collections.Immutable",
        "System.Collections.Frozen"
    ];

    private readonly INamedTypeSymbol? genericCollection = compilation.GetTypeByMetadataName("System.Collections.Generic.ICollection`1");
    private readonly INamedTypeSymbol? nonGenericCollection = compilation.GetTypeByMetadataName("System.Collections.ICollection");
    private readonly INamedTypeSymbol? list = compilation.GetTypeByMetadataName("System.Collections.Generic.List`1");
    private readonly INamedTypeSymbol?[] readOnlyWrappers =
    [
        compilation.GetTypeByMetadataName("System.Collections.ObjectModel.ReadOnlyCollection`1"),
        compilation.GetTypeByMetadataName("System.Collections.ObjectModel.ReadOnlyDictionary`2"),
        compilation.GetTypeByMetadataName("System.Collections.ObjectModel.ReadOnlySet`1")
    ];

    /// <summary>
    /// Determines whether a property type lets callers add, remove, or replace items.
    /// </summary>
    /// <remarks>
    /// Arrays and <c>List&lt;T&gt;</c> return <see langword="false"/> because CA1819 and CA1002 own them.
    /// </remarks>
    public bool IsMutableCollection(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType
            || SymbolEqualityComparer.Default.Equals(namedType.OriginalDefinition, list)
            || IsReadOnly(namedType))
        {
            return false;
        }

        return IsOrImplements(namedType, genericCollection) || IsOrImplements(namedType, nonGenericCollection);
    }

    private bool IsReadOnly(INamedTypeSymbol type)
    {
        var containingNamespace = type.ContainingNamespace?.ToDisplayString();
        if (ReadOnlyNamespaces.Contains(containingNamespace, StringComparer.Ordinal))
        {
            return true;
        }

        for (var current = type; current is not null; current = current.BaseType)
        {
            if (readOnlyWrappers.Contains(current.OriginalDefinition, SymbolEqualityComparer.Default))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsOrImplements(INamedTypeSymbol type, INamedTypeSymbol? collectionInterface)
    {
        if (collectionInterface is null)
        {
            return false;
        }

        return SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, collectionInterface)
            || type.AllInterfaces.Any(candidate => SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, collectionInterface));
    }
}
