using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace PandaBear.Testing.Analyzers;

/// <summary>Shares the SKTEST017 assertion, coverage, and blocker checks between the diagnostic and its code fix.</summary>
internal static class AssertionUserMessageOverload
{
    public const string ParameterName = "userMessage";

    private const string AssertionPrefix = "Should";

    // True when the method is a public assertion without a covering overload. messageIndex is where the parameter
    // goes; blocker is the reason no code fix can add it, or null.
    public static bool IsMissing(IMethodSymbol method, out int messageIndex, out string? blocker)
    {
        messageIndex = -1;
        blocker = null;
        if (!IsAssertion(method) || method.Parameters.Any(IsMessageParameter))
        {
            return false;
        }

        var parameters = method.Parameters;
        var index = parameters.Length > 1 && IsType(parameters[parameters.Length - 1].Type, "System.Threading", "CancellationToken")
            ? parameters.Length - 1
            : parameters.Length;

        // C# signatures ignore parameter names, so at most one sibling has the expected parameter types.
        var sameSignature = FindSibling(method, candidate => HasMessageShape(candidate, parameters, index));
        if (sameSignature is not null && string.Equals(sameSignature.Parameters[index].Name, ParameterName, StringComparison.Ordinal))
        {
            return false;
        }

        messageIndex = index;
        blocker = sameSignature is not null
            ? "'" + Display(sameSignature) + "' already uses that signature"
            : GetCaptureBlocker(parameters)
                ?? GetReturnBlocker(method)
                ?? GetNotNullBlocker(parameters)
                ?? GetComparisonBlocker(method)
                ?? GetOptionalBlocker(parameters, index);
        return true;
    }

    // Finds a same-named sibling with the same type-parameter count and one more parameter whose parameters have the
    // shape. Constructing it with the method's own type parameters makes the types comparable whatever their names are.
    private static IMethodSymbol? FindSibling(IMethodSymbol method, Func<ImmutableArray<IParameterSymbol>, bool> hasShape)
    {
        ITypeSymbol[]? typeArguments = null;
        foreach (var member in method.ContainingType.GetMembers(method.Name))
        {
            if (member is IMethodSymbol sibling
                && sibling.Arity == method.Arity
                && sibling.Parameters.Length == method.Parameters.Length + 1
                && hasShape((sibling.Arity == 0 ? sibling : sibling.Construct(typeArguments ??= [.. method.TypeParameters])).Parameters))
            {
                return sibling;
            }
        }

        return null;
    }

    private static bool IsAssertion(IMethodSymbol method) =>
        method.IsExtensionMethod
        && method.Name.Length > AssertionPrefix.Length
        && method.Name.StartsWith(AssertionPrefix, StringComparison.Ordinal)
        && char.IsUpper(method.Name[AssertionPrefix.Length])
        && method is { MethodKind: MethodKind.Ordinary, IsImplicitlyDeclared: false, IsStatic: true, DeclaredAccessibility: Accessibility.Public }
        && method.ContainingType.DeclaredAccessibility == Accessibility.Public
        && !IsBoolean(method.ReturnType)
        && !method.GetAttributes().Any(static attribute => IsType(attribute.AttributeClass, "System", "ObsoleteAttribute"));

    private static bool IsBoolean(ITypeSymbol type) =>
        type.SpecialType == SpecialType.System_Boolean
        || (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            && nullable.TypeArguments[0].SpecialType == SpecialType.System_Boolean);

    private static bool IsMessageParameter(IParameterSymbol parameter) =>
        parameter.Type.SpecialType == SpecialType.System_String
        && string.Equals(parameter.Name, ParameterName, StringComparison.Ordinal);

    // The candidate has the method's parameters with a string inserted at messageIndex.
    private static bool HasMessageShape(ImmutableArray<IParameterSymbol> candidate, ImmutableArray<IParameterSymbol> parameters, int messageIndex)
    {
        for (var index = 0; index < candidate.Length; index++)
        {
            var matches = index == messageIndex
                ? candidate[index] is { RefKind: RefKind.None, Type.SpecialType: SpecialType.System_String }
                : HasSameType(candidate[index], parameters[index < messageIndex ? index : index - 1]);
            if (!matches)
            {
                return false;
            }
        }

        return true;
    }

    // The candidate has the method's parameters followed by a StringComparison.
    private static bool HasComparisonShape(ImmutableArray<IParameterSymbol> candidate, ImmutableArray<IParameterSymbol> parameters)
    {
        for (var index = 0; index < parameters.Length; index++)
        {
            if (!HasSameType(candidate[index], parameters[index]))
            {
                return false;
            }
        }

        return candidate[parameters.Length].RefKind == RefKind.None
            && IsType(candidate[parameters.Length].Type, "System", "StringComparison");
    }

    private static bool HasSameType(IParameterSymbol candidate, IParameterSymbol parameter) =>
        candidate.RefKind == parameter.RefKind && SymbolEqualityComparer.Default.Equals(candidate.Type, parameter.Type);

    private static string? GetCaptureBlocker(ImmutableArray<IParameterSymbol> parameters)
    {
        foreach (var parameter in parameters)
        {
            if (parameter.RefKind != RefKind.None
                || parameter.Type.IsRefLikeType
                || parameter.Type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer)
            {
                return "a lambda cannot capture parameter '" + parameter.Name + "'";
            }
        }

        return null;
    }

    // A lambda that returns a ValueTask or another awaitable binds to Func<T>, so the helper would not await it and the
    // message would be lost silently.
    private static string? GetReturnBlocker(IMethodSymbol method)
    {
        var returnType = method.ReturnType;
        var shape = method.ReturnsByRef ? "ref " + Display(returnType)
            : method.ReturnsByRefReadonly ? "ref readonly " + Display(returnType)
            : returnType.IsRefLikeType ? Display(returnType)
            : method.IsIterator ? Display(returnType) + " iterator"
            : method.IsAsync && method.ReturnsVoid ? "async void"
            : IsAwaitable(returnType) && !IsType(returnType, "System.Threading.Tasks", "Task") ? Display(returnType)
            : null;
        return shape is null ? null : "the userMessage helper cannot forward a '" + shape + "' result";
    }

    private static bool IsAwaitable(ITypeSymbol type)
    {
        for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (current.GetMembers("GetAwaiter").Any(static member => member is IMethodSymbol { IsStatic: false, Parameters.IsEmpty: true }))
            {
                return true;
            }
        }

        return false;
    }

    private static string? GetNotNullBlocker(ImmutableArray<IParameterSymbol> parameters)
    {
        foreach (var parameter in parameters)
        {
            if (parameter.GetAttributes().Any(static attribute => IsType(attribute.AttributeClass, "System.Diagnostics.CodeAnalysis", "NotNullAttribute")))
            {
                return "parameter '" + parameter.Name + "' has [NotNull], whose null state does not flow out of a lambda; write the overload by hand";
            }
        }

        return null;
    }

    private static string? GetComparisonBlocker(IMethodSymbol method) =>
        FindSibling(method, candidate => HasComparisonShape(candidate, method.Parameters)) is { } comparisonOverload
            ? "forwarding to it would omit the StringComparison that CA1307 requires; forward to '" + Display(comparisonOverload) + "' by hand"
            : null;

    private static string? GetOptionalBlocker(ImmutableArray<IParameterSymbol> parameters, int messageIndex)
    {
        var hasOptionalToken = messageIndex < parameters.Length && parameters[messageIndex].IsOptional;
        return !hasOptionalToken && parameters.Take(messageIndex).Any(static parameter => parameter.IsOptional)
            ? "its optional parameters would become required in the overload, which RS0027 reports on the original"
            : null;
    }

    private static bool IsType(ITypeSymbol? type, string containingNamespace, string name) =>
        type is INamedTypeSymbol { ContainingType: null } namedType
        && string.Equals(namedType.Name, name, StringComparison.Ordinal)
        && string.Equals(namedType.ContainingNamespace?.ToDisplayString(), containingNamespace, StringComparison.Ordinal);

    private static string Display(ISymbol symbol) => symbol.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
}
