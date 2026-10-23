using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace PandaBear.Testing.Analyzers;

/// <summary>Shares the SKTEST017 assertion, coverage, and blocker checks between the diagnostic and its code fix.</summary>
internal static class AssertionUserMessageOverload
{
    public const string ParameterName = "userMessage";

    private const string AssertionPrefix = "Should";

    // A call that omits one of these trailing parameters is reported by the rule that requires it.
    private static readonly (string Namespace, string Name, string Rule)[] CultureParameters =
    [
        ("System", "StringComparison", "CA1307"),
        ("System", "IFormatProvider", "CA1305"),
        ("System.Globalization", "CultureInfo", "CA1304"),
    ];

    // True when the method is a public assertion without a covering overload. messageIndex is where the parameter
    // goes; blocker is the reason no code fix can add it, or null.
    public static bool IsMissing(IMethodSymbol method, Compilation compilation, out int messageIndex, out string? blocker)
    {
        messageIndex = -1;
        blocker = null;
        if (!IsAssertion(method) || method.Parameters.Skip(1).Any(IsMessageParameter))
        {
            return false;
        }

        var parameters = method.Parameters;
        var index = parameters.Length > 1 && IsType(parameters[parameters.Length - 1].Type, "System.Threading", "CancellationToken")
            ? parameters.Length - 1
            : parameters.Length;

        // C# signatures ignore parameter names, so at most one sibling has the expected parameter types. Only a public
        // extension method covers the assertion; any other sibling with that signature blocks the overload.
        var sameSignature = FindSibling(method, candidate => HasMessageShape(candidate, parameters, index));
        if (sameSignature is { DeclaredAccessibility: Accessibility.Public, IsExtensionMethod: true }
            && string.Equals(sameSignature.Parameters[index].Name, ParameterName, StringComparison.Ordinal))
        {
            return false;
        }

        messageIndex = index;
        blocker = sameSignature is not null
            ? "'" + Display(sameSignature) + "' already uses that signature"
            : GetDeclarationBlocker(method)
                ?? GetCaptureBlocker(method)
                ?? GetReturnBlocker(method)
                ?? GetNotNullBlocker(parameters)
                ?? GetDoesNotReturnBlocker(method)
                ?? GetAllowNullBlocker(parameters)
                ?? GetSuppressionBlocker(method)
                ?? GetComparisonBlocker(method)
                ?? GetOptionalBlocker(parameters, index)
                ?? GetOverloadBlocker(method)
                ?? GetRebindBlocker(method, index, compilation);
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

    // The candidate has the method's parameters followed by one of the named type.
    private static bool HasTrailingParameterShape(
        ImmutableArray<IParameterSymbol> candidate,
        ImmutableArray<IParameterSymbol> parameters,
        string containingNamespace,
        string name)
    {
        for (var index = 0; index < parameters.Length; index++)
        {
            if (!HasSameType(candidate[index], parameters[index]))
            {
                return false;
            }
        }

        return candidate[parameters.Length].RefKind == RefKind.None
            && IsType(candidate[parameters.Length].Type, containingNamespace, name);
    }

    private static bool HasSameType(IParameterSymbol candidate, IParameterSymbol parameter) =>
        candidate.RefKind == parameter.RefKind && IsSameSignatureType(candidate.Type, parameter.Type);

    // C# signatures treat dynamic as object and ignore tuple element names and nullability; SymbolEqualityComparer.Default
    // ignores only nullability.
    private static bool IsSameSignatureType(ITypeSymbol left, ITypeSymbol right) => (left, right) switch
    {
        _ when IsObjectOrDynamic(left) && IsObjectOrDynamic(right) => true,
        (IArrayTypeSymbol leftArray, IArrayTypeSymbol rightArray) =>
            leftArray.Rank == rightArray.Rank && IsSameSignatureType(leftArray.ElementType, rightArray.ElementType),
        (INamedTypeSymbol { IsGenericType: true } leftType, INamedTypeSymbol { IsGenericType: true } rightType) =>
            SymbolEqualityComparer.Default.Equals(leftType.OriginalDefinition, rightType.OriginalDefinition)
            && HaveSameTypeArguments(leftType, rightType),
        _ => SymbolEqualityComparer.Default.Equals(left, right),
    };

    // Compares the type arguments of the types and of their containing types, which share one definition.
    private static bool HaveSameTypeArguments(INamedTypeSymbol left, INamedTypeSymbol right)
    {
        for (INamedTypeSymbol? leftType = left, rightType = right;
            leftType is not null && rightType is not null;
            leftType = leftType.ContainingType, rightType = rightType.ContainingType)
        {
            for (var index = 0; index < leftType.TypeArguments.Length; index++)
            {
                if (!IsSameSignatureType(leftType.TypeArguments[index], rightType.TypeArguments[index]))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool IsObjectOrDynamic(ITypeSymbol type) =>
        type.SpecialType == SpecialType.System_Object || type.TypeKind == TypeKind.Dynamic;

    // The fix copies a declaration that has a body, and the overload's parameter needs a name that no parameter has.
    private static string? GetDeclarationBlocker(IMethodSymbol method) =>
        method.IsPartialDefinition || method.PartialDefinitionPart is not null
            ? "a partial method needs a hand-written overload"
            : method.Parameters.Any(static parameter => string.Equals(parameter.Name, ParameterName, StringComparison.Ordinal))
                ? "a parameter already has the name '" + ParameterName + "'"
                : null;

    private static string? GetCaptureBlocker(IMethodSymbol method)
    {
        if (method.IsVararg)
        {
            return "a lambda cannot forward '__arglist'";
        }

        foreach (var parameter in method.Parameters)
        {
            if (parameter.RefKind != RefKind.None
                || IsRefLike(parameter.Type)
                || parameter.Type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer)
            {
                return "a lambda cannot capture parameter '" + parameter.Name + "'";
            }
        }

        return null;
    }

    // A type parameter that allows ref structs can be one.
    private static bool IsRefLike(ITypeSymbol type) =>
        type.IsRefLikeType || type is ITypeParameterSymbol { AllowsRefLikeType: true };

    // A lambda that returns a ValueTask or another awaitable binds to Func<T>, so the helper would not await it and the
    // message would be lost silently. A sequence or a delegate can fail after the helper has returned it.
    private static string? GetReturnBlocker(IMethodSymbol method)
    {
        var returnType = method.ReturnType;
        var shape = method.ReturnsByRef ? "ref " + Display(returnType)
            : method.ReturnsByRefReadonly ? "ref readonly " + Display(returnType)
            : IsRefLike(returnType) ? Display(returnType)
            : method.IsIterator ? Display(returnType) + " iterator"
            : method.IsAsync && method.ReturnsVoid ? "async void"
            : IsAwaitable(returnType) && !IsType(returnType, "System.Threading.Tasks", "Task") ? Display(returnType)
            : IsDeferred(returnType) ? Display(returnType)
            : null;
        return shape is null ? null : "the userMessage helper cannot forward the '" + shape + "' result";
    }

    // Await finds an instance GetAwaiter through base types, interfaces, and the constraints of a type parameter.
    private static bool IsAwaitable(ITypeSymbol type)
    {
        if (type is ITypeParameterSymbol typeParameter)
        {
            return typeParameter.ConstraintTypes.Any(IsAwaitable);
        }

        for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (HasGetAwaiter(current))
            {
                return true;
            }
        }

        return type.AllInterfaces.Any(HasGetAwaiter);
    }

    private static bool HasGetAwaiter(ITypeSymbol type) =>
        type.GetMembers("GetAwaiter").Any(static member => member is IMethodSymbol { IsStatic: false, Parameters.IsEmpty: true });

    // The sequence types that an iterator can return, and delegates, run their checks when the caller uses them.
    private static bool IsDeferred(ITypeSymbol type) =>
        type.TypeKind == TypeKind.Delegate
        || IsType(type, "System.Collections", "IEnumerable")
        || IsType(type, "System.Collections", "IEnumerator")
        || IsType(type, "System.Collections.Generic", "IEnumerable")
        || IsType(type, "System.Collections.Generic", "IEnumerator")
        || IsType(type, "System.Collections.Generic", "IAsyncEnumerable")
        || IsType(type, "System.Collections.Generic", "IAsyncEnumerator");

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

    // The compiler cannot see that the forwarded call does not return, so it reports CS8763 on the overload.
    private static string? GetDoesNotReturnBlocker(IMethodSymbol method) =>
        method.GetAttributes().Any(static attribute => IsType(attribute.AttributeClass, "System.Diagnostics.CodeAnalysis", "DoesNotReturnAttribute"))
            ? "it has [DoesNotReturn], which an expression-bodied forwarder cannot keep (CS8763); write the overload by hand"
            : null;

    // A maybe-null argument for an [AllowNull] type parameter makes the forwarded call infer the nullable type argument,
    // which the overload's own type argument does not satisfy.
    private static string? GetAllowNullBlocker(ImmutableArray<IParameterSymbol> parameters)
    {
        foreach (var parameter in parameters)
        {
            if (parameter.Type is ITypeParameterSymbol { HasValueTypeConstraint: false }
                && parameter.GetAttributes().Any(static attribute => IsType(attribute.AttributeClass, "System.Diagnostics.CodeAnalysis", "AllowNullAttribute")))
            {
                return "parameter '" + parameter.Name + "' has [AllowNull], so the forwarded call would infer a nullable type argument; write the overload by hand";
            }
        }

        return null;
    }

    // A suppression covers only its own member, so a rule that inspects the copied signature would report the overload.
    private static string? GetSuppressionBlocker(IMethodSymbol method)
    {
        foreach (var attribute in method.GetAttributes())
        {
            if ((IsType(attribute.AttributeClass, "System.Diagnostics.CodeAnalysis", "SuppressMessageAttribute")
                    || IsType(attribute.AttributeClass, "System.Diagnostics.CodeAnalysis", "UnconditionalSuppressMessageAttribute"))
                && attribute.ConstructorArguments.Length > 1
                && attribute.ConstructorArguments[1].Value is string checkId)
            {
                var separator = checkId.IndexOf(':');
                var id = (separator < 0 ? checkId : checkId.Substring(0, separator)).Trim();
                if (!string.Equals(id, TestingDiagnosticIds.AssertionUserMessageOverload, StringComparison.Ordinal))
                {
                    return "its suppression of '" + id + "' would not carry over to a generated overload; write the overload by hand with its own justification";
                }
            }
        }

        return null;
    }

    private static string? GetComparisonBlocker(IMethodSymbol method)
    {
        foreach (var (containingNamespace, name, rule) in CultureParameters)
        {
            if (FindSibling(method, candidate => HasTrailingParameterShape(candidate, method.Parameters, containingNamespace, name)) is { } overload)
            {
                return "forwarding to it would omit the " + name + " that " + rule + " requires; forward to '" + Display(overload) + "' by hand";
            }
        }

        return null;
    }

    private static string? GetOptionalBlocker(ImmutableArray<IParameterSymbol> parameters, int messageIndex)
    {
        var hasOptionalToken = messageIndex < parameters.Length && parameters[messageIndex].IsOptional;
        return !hasOptionalToken && parameters.Take(messageIndex).Any(static parameter => parameter.IsOptional)
            ? "its optional parameters would become required in the overload, which RS0027 reports on the original"
            : null;
    }

    // RS0027 reports a public overload with optional parameters when another public overload of any arity has no optional
    // parameters and at least as many parameters, as the overload of an assertion without optional parameters would.
    private static string? GetOverloadBlocker(IMethodSymbol method)
    {
        if (method.Parameters.Any(static parameter => parameter.IsOptional))
        {
            return null;
        }

        foreach (var member in method.ContainingType.GetMembers(method.Name))
        {
            if (member is IMethodSymbol { DeclaredAccessibility: Accessibility.Public } sibling
                && sibling.Parameters.Length <= method.Parameters.Length + 1
                && sibling.Parameters.Any(static parameter => parameter.IsOptional))
            {
                return "'" + Display(sibling) + "' would no longer have the most parameters among its public overloads, which RS0027 reports";
            }
        }

        return null;
    }

    // A string argument binds better to the overload's string parameter than to a type parameter or through an implicit
    // conversion. Existing calls to a sibling that takes one there, or expanded calls to an object-like params array, would
    // move to the overload without a diagnostic.
    private static string? GetRebindBlocker(IMethodSymbol method, int messageIndex, Compilation compilation)
    {
        var parameters = method.Parameters;
        var stringType = compilation.GetSpecialType(SpecialType.System_String);
        if (messageIndex == parameters.Length
            && parameters[messageIndex - 1] is { IsParams: true, Type: IArrayTypeSymbol arrayType }
            && IsImplicitConversion(compilation, arrayType, arrayType.ElementType)
            && IsImplicitConversion(compilation, stringType, arrayType.ElementType))
        {
            return "existing calls that pass a string as the last of its 'params' values would bind to the overload";
        }

        foreach (var member in method.ContainingType.GetMembers(method.Name))
        {
            if (member is IMethodSymbol sibling
                && sibling.Arity >= method.Arity
                && sibling.Parameters.Length == parameters.Length + 1
                && TakesStringAt(ConstructWithLeadingTypeParameters(sibling, method).Parameters, parameters, messageIndex, compilation, stringType))
            {
                return "existing calls to '" + Display(sibling) + "' that pass a string would bind to the overload";
            }
        }

        return null;
    }

    // The sibling with the method's type parameters in place of its own leading ones and its remaining ones unchanged.
    private static IMethodSymbol ConstructWithLeadingTypeParameters(IMethodSymbol sibling, IMethodSymbol method) =>
        sibling.Arity == 0 ? sibling : sibling.Construct([.. method.TypeParameters, .. sibling.TypeParameters.Skip(method.Arity)]);

    // The candidate has the method's parameters with a parameter that accepts a string argument at messageIndex.
    private static bool TakesStringAt(
        ImmutableArray<IParameterSymbol> candidate,
        ImmutableArray<IParameterSymbol> parameters,
        int messageIndex,
        Compilation compilation,
        ITypeSymbol stringType)
    {
        for (var index = 0; index < candidate.Length; index++)
        {
            var matches = index == messageIndex
                ? candidate[index].RefKind == RefKind.None && AcceptsString(candidate[index], compilation, stringType)
                : HasSameType(candidate[index], parameters[index < messageIndex ? index : index - 1]);
            if (!matches)
            {
                return false;
            }
        }

        return true;
    }

    private static bool AcceptsString(IParameterSymbol parameter, Compilation compilation, ITypeSymbol stringType)
    {
        var type = parameter.IsParams && parameter.Type is IArrayTypeSymbol arrayType ? arrayType.ElementType : parameter.Type;
        return type is ITypeParameterSymbol typeParameter
            ? !typeParameter.HasValueTypeConstraint
            : IsImplicitConversion(compilation, stringType, type);
    }

    private static bool IsImplicitConversion(Compilation compilation, ITypeSymbol source, ITypeSymbol destination) =>
        compilation.ClassifyCommonConversion(source, destination) is { Exists: true, IsImplicit: true };

    private static bool IsType(ITypeSymbol? type, string containingNamespace, string name) =>
        type is INamedTypeSymbol { ContainingType: null } namedType
        && string.Equals(namedType.Name, name, StringComparison.Ordinal)
        && string.Equals(namedType.ContainingNamespace?.ToDisplayString(), containingNamespace, StringComparison.Ordinal);

    private static string Display(ISymbol symbol) => symbol.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
}
