using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace SharedKernel.Testing.SourceGenerator;

/// <summary>Finds the places in the source that wrap or hook a service: the entry points and the typed hooks they configure.</summary>
internal static class CallProxySiteReader
{
    private const string CallProxyTypeName = "SharedKernel.Testing.CallProxy";
    private const string ServiceCollectionExtensionsTypeName = "SharedKernel.Testing.CallProxyServiceCollectionExtensions";
    private const string CallHooksMetadataName = "SharedKernel.Testing.CallHooks`1";
    private const string CallProxyName = "CallProxy";
    private const string CreateName = "Create";
    private const string WrapName = "WrapWithCallProxy";

    /// <summary>Tells whether a node may call <c>CallProxy.Create</c> or <c>WrapWithCallProxy</c>.</summary>
    /// <param name="node">The node.</param>
    /// <returns>
    /// <see langword="true"/> for a <c>WrapWithCallProxy</c> call, and for a <c>Create</c> call on something named <c>CallProxy</c> or
    /// without a receiver (<c>using static</c>), so other <c>Create</c> methods are not bound.
    /// </returns>
    public static bool IsEntryCandidate(SyntaxNode node) =>
        node is InvocationExpressionSyntax invocation
        && GetName(invocation.Expression)?.Identifier.ValueText switch
        {
            WrapName => true,
            CreateName => invocation.Expression is SimpleNameSyntax
                || (invocation.Expression is MemberAccessExpressionSyntax { Expression: var receiver } && GetRightmostName(receiver) == CallProxyName),
            _ => false,
        };

    /// <summary>Tells whether a node may add a typed hook, such as <c>hooks.Find.Before(...)</c>.</summary>
    /// <param name="node">The node.</param>
    /// <returns><see langword="true"/> for a <c>Before</c>, <c>After</c> or <c>Returns</c> call on a member of something.</returns>
    public static bool IsHookCandidate(SyntaxNode node) =>
        node is InvocationExpressionSyntax
        {
            Expression: MemberAccessExpressionSyntax
            {
                Name.Identifier.ValueText: "Before" or "After" or "Returns",
                Expression: MemberAccessExpressionSyntax,
            },
        };

    /// <summary>Reads the service of a <c>CallProxy.Create</c> or <c>WrapWithCallProxy</c> call.</summary>
    /// <param name="context">The syntax context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The site, or <see langword="null"/> when the call is not one of them or its service is a type parameter.</returns>
    public static ProxySite? ReadEntry(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation, cancellationToken);
        if ((symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault()) is not IMethodSymbol method
            || method.TypeArguments.Length != 1
            || !IsEntryPoint(method))
        {
            return null;
        }

        var typeArgument = GetName(invocation.Expression) is GenericNameSyntax { TypeArgumentList.Arguments: { Count: 1 } arguments }
            ? arguments[0]
            : null;
        var service = typeArgument is not null
            ? context.SemanticModel.GetTypeInfo(typeArgument, cancellationToken).Type
            : method.TypeArguments[0];
        if (service is ITypeParameterSymbol && typeArgument is null && method.Name == CreateName && invocation.ArgumentList.Arguments.Count > 0)
        {
            service = context.SemanticModel.GetTypeInfo(invocation.ArgumentList.Arguments[0].Expression, cancellationToken).Type;
        }

        return Read(context, service, typeArgument?.GetLocation() ?? invocation.GetLocation());
    }

    /// <summary>Reads the service of the <c>CallHooks&lt;T&gt;</c> that a typed hook is added to.</summary>
    /// <param name="context">The syntax context.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The site, or <see langword="null"/> when the receiver is not <c>CallHooks&lt;T&gt;</c> of a known type.</returns>
    public static ProxySite? ReadHook(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        var member = (MemberAccessExpressionSyntax)((MemberAccessExpressionSyntax)((InvocationExpressionSyntax)context.Node).Expression).Expression;
        if (context.SemanticModel.GetTypeInfo(member.Expression, cancellationToken).Type is not INamedTypeSymbol { TypeArguments.Length: 1 } hooks
            || !SymbolEqualityComparer.Default.Equals(hooks.OriginalDefinition, context.SemanticModel.Compilation.GetTypeByMetadataName(CallHooksMetadataName)))
        {
            return null;
        }

        return Read(context, hooks.TypeArguments[0], member.GetLocation());
    }

    private static ProxySite? Read(GeneratorSyntaxContext context, ITypeSymbol? service, Location location) =>
        service is null or ITypeParameterSymbol or IErrorTypeSymbol
            ? null
            : ProxyModelBuilder.Build(
                service,
                context.SemanticModel.Compilation,
                location,
                ((CSharpParseOptions)context.Node.SyntaxTree.Options).LanguageVersion);

    private static bool IsEntryPoint(IMethodSymbol method)
    {
        var containingType = method.ContainingType?.ToDisplayString();
        return (method.Name == CreateName && containingType == CallProxyTypeName)
            || (method.Name == WrapName && containingType == ServiceCollectionExtensionsTypeName);
    }

    private static string? GetRightmostName(ExpressionSyntax expression) => expression switch
    {
        MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
        AliasQualifiedNameSyntax aliasQualified => aliasQualified.Name.Identifier.ValueText,
        QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
        SimpleNameSyntax name => name.Identifier.ValueText,
        _ => null,
    };

    private static SimpleNameSyntax? GetName(ExpressionSyntax expression) => expression switch
    {
        MemberAccessExpressionSyntax memberAccess => memberAccess.Name,
        MemberBindingExpressionSyntax memberBinding => memberBinding.Name,
        SimpleNameSyntax name => name,
        _ => null,
    };
}
