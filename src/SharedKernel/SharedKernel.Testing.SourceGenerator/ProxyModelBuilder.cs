using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SharedKernel.Testing.SourceGenerator;

/// <summary>Turns a wrapped service type into the proxy to generate, or into the diagnostics that explain why it cannot be proxied.</summary>
internal static class ProxyModelBuilder
{
    private const string NullableAttributesNamespace = "System.Diagnostics.CodeAnalysis";
    private const string TasksNamespace = "System.Threading.Tasks";

    private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>Builds what one wrapping or hooking place in the source asks for.</summary>
    /// <param name="service">The wrapped service type.</param>
    /// <param name="compilation">The compilation the proxy is generated into.</param>
    /// <param name="location">Where to report diagnostics.</param>
    /// <param name="languageVersion">The C# version of the source.</param>
    /// <returns>The proxy to generate or the diagnostics.</returns>
    public static ProxySite Build(ITypeSymbol service, Compilation compilation, Location location, LanguageVersion languageVersion)
    {
        var where = LocationInfo.From(location);
        var display = service.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);
        if (languageVersion < LanguageVersion.CSharp14)
        {
            return Fail(CallProxyDiagnosticIds.LanguageVersion, where, languageVersion.ToDisplayString());
        }

        if (service is not INamedTypeSymbol { TypeKind: TypeKind.Interface } named)
        {
            return Fail(CallProxyDiagnosticIds.NotAnInterface, where, display);
        }

        if (!compilation.IsSymbolAccessibleWithin(named, compilation.Assembly))
        {
            return Fail(CallProxyDiagnosticIds.InaccessibleInterface, where, display);
        }

        var methods = new List<IMethodSymbol>();
        var diagnostics = new List<DiagnosticInfo>();
        foreach (var member in new[] { named }.Concat(named.AllInterfaces).SelectMany(static type => type.GetMembers()))
        {
            if (member is INamedTypeSymbol || member.IsStatic || !(member.IsAbstract || member.IsVirtual) || IsExplicitImplementation(member))
            {
                continue;
            }

            var reason = member switch
            {
                IMethodSymbol { MethodKind: MethodKind.Ordinary } method => GetUnsupportedReason(method),
                IMethodSymbol => null,
                IPropertySymbol { IsIndexer: true } => "an indexer",
                IPropertySymbol => "a property",
                IEventSymbol => "an event",
                _ => null,
            };

            if (reason is not null)
            {
                diagnostics.Add(Diagnostic(
                    CallProxyDiagnosticIds.UnsupportedMember,
                    where,
                    display,
                    member.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat),
                    reason));
            }
            else if (member is IMethodSymbol { MethodKind: MethodKind.Ordinary } method)
            {
                methods.Add(method);
            }
        }

        return diagnostics.Count > 0
            ? new ProxySite(null, new EquatableArray<DiagnosticInfo>([.. diagnostics]))
            : new ProxySite(
                new ProxyModel(
                    named.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                    named.ToDisplayString(TypeFormat),
                    display,
                    new EquatableArray<ProxyMethod>(BuildMethods(named, methods))),
                new EquatableArray<DiagnosticInfo>([]));
    }

    private static ProxyMethod[] BuildMethods(INamedTypeSymbol service, List<IMethodSymbol> methods)
    {
        var overloaded = new HashSet<string>(
            methods.GroupBy(static method => method.Name, StringComparer.Ordinal).Where(static group => group.Count() > 1).Select(static group => group.Key),
            StringComparer.Ordinal);
        var usedHookNames = new HashSet<string>(StringComparer.Ordinal);
        var result = new ProxyMethod[methods.Count];
        for (var index = 0; index < methods.Count; index++)
        {
            var method = methods[index];
            var preferred = overloaded.Contains(method.Name) ? method.Name + "_" + OverloadSuffix(method) : method.Name;
            if (Array.IndexOf(ProxyNaming.ReservedHookNames, preferred) >= 0)
            {
                preferred += "Method";
            }

            var hookName = ProxyNaming.Unique(preferred, usedHookNames);
            result[index] = BuildMethod(service, method, hookName);
        }

        return result;
    }

    private static ProxyMethod BuildMethod(INamedTypeSymbol service, IMethodSymbol method, string hookName)
    {
        var (kind, resultType) = GetReturnKind(method);
        var callTypeName = hookName + "Call";
        var usedPropertyNames = new HashSet<string>(ProxyNaming.ReservedPropertyNames, StringComparer.Ordinal) { callTypeName };
        if (resultType is not null)
        {
            usedPropertyNames.Add("Result");
        }

        var parameters = method.Parameters
            .Select(parameter =>
            {
                var preferred = ProxyNaming.Pascal(parameter.Name);
                if (usedPropertyNames.Contains(preferred))
                {
                    preferred += "Argument";
                }

                return new ProxyParameter(
                    ProxyNaming.Escape(parameter.Name),
                    parameter.Name,
                    ProxyNaming.Unique(preferred, usedPropertyNames),
                    parameter.Type.ToDisplayString(TypeFormat),
                    FormatAttributes(parameter.GetAttributes(), string.Empty) + (parameter.IsParams ? "params " : string.Empty));
            })
            .ToArray();

        return new ProxyMethod(
            ProxyNaming.Escape(hookName),
            callTypeName,
            ProxyNaming.Escape(method.Name),
            method.ContainingType.ToDisplayString(TypeFormat),
            !SymbolEqualityComparer.Default.Equals(method.ContainingType, service),
            kind,
            method.ReturnType.ToDisplayString(TypeFormat),
            resultType?.ToDisplayString(TypeFormat),
            FormatAttributes(method.GetReturnTypeAttributes(), "return: "),
            method.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat),
            new EquatableArray<ProxyParameter>(parameters));
    }

    private static (ProxyReturnKind Kind, ITypeSymbol? ResultType) GetReturnKind(IMethodSymbol method)
    {
        if (method.ReturnsVoid)
        {
            return (ProxyReturnKind.Void, null);
        }

        if (method.ReturnType is INamedTypeSymbol named && named.ContainingNamespace?.ToDisplayString() == TasksNamespace)
        {
            switch (named.MetadataName)
            {
                case "Task":
                    return (ProxyReturnKind.Task, null);
                case "ValueTask":
                    return (ProxyReturnKind.ValueTask, null);
                case "Task`1":
                    return (ProxyReturnKind.TaskOfResult, named.TypeArguments[0]);
                case "ValueTask`1":
                    return (ProxyReturnKind.ValueTaskOfResult, named.TypeArguments[0]);
            }
        }

        return (ProxyReturnKind.Value, method.ReturnType);
    }

    private static string? GetUnsupportedReason(IMethodSymbol method)
    {
        if (method.IsGenericMethod)
        {
            return "a generic method";
        }

        if (method.Parameters.Any(static parameter => parameter.RefKind != RefKind.None))
        {
            return "a method with a ref, out or in parameter";
        }

        if (method.ReturnsByRef || method.ReturnsByRefReadonly)
        {
            return "a method that returns by reference";
        }

        if (method.IsVararg)
        {
            return "a method with a variable argument list";
        }

        return IsRefLikeOrPointer(method.ReturnType) || method.Parameters.Any(static parameter => IsRefLikeOrPointer(parameter.Type))
            ? "a method with a ref struct or pointer parameter or result"
            : null;
    }

    private static bool IsRefLikeOrPointer(ITypeSymbol type) =>
        type.IsRefLikeType || type.TypeKind is TypeKind.Pointer or TypeKind.FunctionPointer;

    private static bool IsExplicitImplementation(ISymbol member) => member switch
    {
        IMethodSymbol method => method.ExplicitInterfaceImplementations.Length > 0,
        IPropertySymbol property => property.ExplicitInterfaceImplementations.Length > 0,
        IEventSymbol @event => @event.ExplicitInterfaceImplementations.Length > 0,
        _ => false,
    };

    private static string OverloadSuffix(IMethodSymbol method) =>
        method.Parameters.Length == 0
            ? "NoParameters"
            : string.Join("_", method.Parameters.Select(static parameter => ProxyNaming.TypeToken(parameter.Type)));

    private static string FormatAttributes(IEnumerable<AttributeData> attributes, string target) =>
        string.Concat(attributes
            .Where(static attribute => attribute.AttributeClass?.ContainingNamespace?.ToDisplayString() == NullableAttributesNamespace)
            .Select(attribute =>
            {
                var arguments = attribute.ConstructorArguments.Select(static argument => argument.ToCSharpString())
                    .Concat(attribute.NamedArguments.Select(static argument => argument.Key + " = " + argument.Value.ToCSharpString()))
                    .ToArray();
                var name = attribute.AttributeClass!.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                return "[" + target + name + (arguments.Length > 0 ? "(" + string.Join(", ", arguments) + ")" : string.Empty) + "] ";
            }));

    private static ProxySite Fail(string id, LocationInfo? where, string argument) =>
        new(null, new EquatableArray<DiagnosticInfo>([Diagnostic(id, where, argument)]));

    private static DiagnosticInfo Diagnostic(string id, LocationInfo? where, params string[] arguments) =>
        new(id, where, new EquatableArray<string>(arguments));
}
