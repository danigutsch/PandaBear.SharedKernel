using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace PandaBear.Blazor.Analyzers;

/// <summary>
/// Reports Blazor component state and service-scope mistakes: a busy flag that an exception can leave set, and services injected
/// into a component that go stale in a connection-long service scope.
/// </summary>
/// <remarks>
/// Only types that implement <c>IComponent</c> are analyzed. Code generated from <c>.razor</c> files is analyzed too, so
/// diagnostics in <c>@code</c> blocks point at the <c>.razor</c> file; the IDE cannot apply code fixes there, but code-behind files get them.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SharedKernelBlazorAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor UnguardedBusyFlagRule = new(
        BlazorDiagnosticIds.UnguardedBusyFlag,
        title: "Reset a busy flag in a finally block",
        messageFormat: "Flag '{0}' is set to true before an await and reset without try/finally; reset it in a finally block so an exception cannot leave it set",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A component that disables its UI while it saves must clear the flag even when the save throws. Otherwise the button stays disabled until the page is reloaded.");

    private static readonly DiagnosticDescriptor LongLivedScopeServiceRule = new(
        BlazorDiagnosticIds.LongLivedScopeService,
        title: "Do not inject a scope-bound service into a component",
        messageFormat: "Component injects '{0}', which goes stale in the single service scope of a Blazor Server circuit; {1}",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Components on Blazor Server live as long as the connection and share one service scope. A DbContext or a sender resolved from it keeps tracked entities and cached state between unrelated operations. Create one scope or one context per operation.");

    private static readonly DiagnosticDescriptor MissingKeyRule = new(
        BlazorDiagnosticIds.MissingKey,
        title: "Give looped rows, items and components a key",
        messageFormat: "A loop renders {0} without a key; add @key (SetKey) so Blazor keeps each item's state when the list changes",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Without a key Blazor matches items by position, so after an insert, a removal or a reorder the state of one row (a pending confirmation, a focused input) can attach to a different row. Reported for table rows, list items and components; options and other elements are not reported.");

    private static readonly DiagnosticDescriptor StaleParameterRule = new(
        BlazorDiagnosticIds.StaleParameter,
        title: "Load parameter-dependent state in OnParametersSet",
        messageFormat: "Parameter '{0}' is read in {1}, which runs once, but the route can change it while the component instance is reused; load state in OnParametersSetAsync and reload when the value changes (see ParameterTracker<T>)",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Blazor keeps one component instance when the route changes between URLs that map to the same component, so OnInitialized does not run again and state loaded from a parameter goes stale. Reported for routable components that have a route parameter, several routes or a query parameter.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
    {
        get;
    } = ImmutableArray.Create(UnguardedBusyFlagRule, LongLivedScopeServiceRule, MissingKeyRule, StaleParameterRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static compilationContext =>
        {
            var component = compilationContext.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.IComponent");
            if (component is null)
            {
                return;
            }

            var inject = compilationContext.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.InjectAttribute");
            var sender = compilationContext.Compilation.GetTypeByMetadataName("PandaBear.Mediator.ISender");
            var dbContext = compilationContext.Compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbContext");

            compilationContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeBlock(nodeContext, component),
                SyntaxKind.Block);
            compilationContext.RegisterSyntaxNodeAction(
                nodeContext => AnalyzeLoop(nodeContext, component),
                SyntaxKind.ForEachStatement,
                SyntaxKind.ForStatement);

            var parameter = compilationContext.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.ParameterAttribute");
            var query = compilationContext.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.SupplyParameterFromQueryAttribute");
            var route = compilationContext.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Components.RouteAttribute");
            if (parameter is not null && route is not null)
            {
                compilationContext.RegisterOperationAction(
                    operationContext => AnalyzeParameterRead(operationContext, component, parameter, query, route),
                    OperationKind.PropertyReference);
            }

            if (inject is not null && (sender is not null || dbContext is not null))
            {
                compilationContext.RegisterSymbolAction(
                    symbolContext => AnalyzeProperty(symbolContext, component, inject, sender, dbContext),
                    SymbolKind.Property);
            }
        });
    }

    private static void AnalyzeParameterRead(
        OperationAnalysisContext context,
        INamedTypeSymbol component,
        INamedTypeSymbol parameter,
        INamedTypeSymbol? query,
        INamedTypeSymbol route)
    {
        var reference = (IPropertyReferenceOperation)context.Operation;
        if (context.ContainingSymbol is not IMethodSymbol { IsOverride: true, Name: "OnInitialized" or "OnInitializedAsync" } method
            || reference.Instance is not IInstanceReferenceOperation
            || !IsComponent(method.ContainingType, component)
            || !HasAttribute(reference.Property, parameter, query)
            || IsNameOfOrWrite(reference)
            || !CanChangeParameters(method.ContainingType, route, IsQueryParameter(reference.Property, query)))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(StaleParameterRule, reference.Syntax.GetLocation(), reference.Property.Name, method.Name));
    }

    private static bool HasAttribute(IPropertySymbol property, INamedTypeSymbol parameter, INamedTypeSymbol? query)
    {
        return property.GetAttributes().Any(attribute =>
            SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, parameter)
            || (query is not null && SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, query)));
    }

    private static bool IsQueryParameter(IPropertySymbol property, INamedTypeSymbol? query)
    {
        return query is not null
            && property.GetAttributes().Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, query));
    }

    private static bool IsNameOfOrWrite(IPropertyReferenceOperation reference)
    {
        for (var current = reference.Parent; current is not null; current = current.Parent)
        {
            if (current is INameOfOperation)
            {
                return true;
            }
        }

        return reference.Parent is IAssignmentOperation { Target: var target } && target == reference;
    }

    private static bool CanChangeParameters(INamedTypeSymbol type, INamedTypeSymbol route, bool isQueryParameter)
    {
        var routes = type.GetAttributes()
            .Where(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, route))
            .ToArray();
        return routes.Length > 0
            && (isQueryParameter
                || routes.Length > 1
                || routes.Any(attribute => attribute.ConstructorArguments.Length > 0
                    && attribute.ConstructorArguments[0].Value is string template
                    && template.IndexOf('{') >= 0));
    }

    private static void AnalyzeBlock(SyntaxNodeAnalysisContext context, INamedTypeSymbol component)
    {
        if (!IsComponent(context.ContainingSymbol?.ContainingType, component))
        {
            return;
        }

        var block = (BlockSyntax)context.Node;
        for (var index = 0; index < block.Statements.Count; index++)
        {
            var assignment = BusyFlagPattern.AsAssignment(block.Statements[index]);
            if (assignment is null || !BusyFlagPattern.TryFindReset(context.SemanticModel, block, index, out _))
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(UnguardedBusyFlagRule, assignment.GetLocation(), assignment.Left.ToString()));
        }
    }

    private static void AnalyzeLoop(SyntaxNodeAnalysisContext context, INamedTypeSymbol component)
    {
        if (!IsComponent(context.ContainingSymbol?.ContainingType, component))
        {
            return;
        }

        var body = context.Node is ForEachStatementSyntax forEach ? forEach.Statement : ((ForStatementSyntax)context.Node).Statement;
        InvocationExpressionSyntax? firstKeyed = null;
        var hasKey = false;
        foreach (var invocation in OwnInvocations(body))
        {
            var name = InvocationName(invocation);
            if (name == "SetKey")
            {
                hasKey = true;
            }
            else if (firstKeyed is null && IsKeyedKind(invocation, name))
            {
                firstKeyed = invocation;
            }
        }

        if (firstKeyed is null || hasKey)
        {
            return;
        }

        var rendered = InvocationName(firstKeyed) == "OpenComponent" ? "components" : "rows or list items";
        context.ReportDiagnostic(Diagnostic.Create(MissingKeyRule, firstKeyed.GetLocation(), rendered));
    }

    private static IEnumerable<InvocationExpressionSyntax> OwnInvocations(SyntaxNode body)
    {
        return body
            .DescendantNodesAndSelf(node => node == body || node is not (ForEachStatementSyntax or ForStatementSyntax or WhileStatementSyntax))
            .OfType<InvocationExpressionSyntax>();
    }

    private static string? InvocationName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax { Name: GenericNameSyntax generic } => generic.Identifier.ValueText,
            MemberAccessExpressionSyntax { Name: var name } => name.Identifier.ValueText,
            _ => null,
        };
    }

    private static bool IsKeyedKind(InvocationExpressionSyntax invocation, string? name)
    {
        if (name == "OpenComponent")
        {
            return true;
        }

        return name == "OpenElement"
            && invocation.ArgumentList.Arguments.Count >= 2
            && invocation.ArgumentList.Arguments[1].Expression is LiteralExpressionSyntax literal
            && literal.Token.ValueText is "tr" or "li";
    }

    private static void AnalyzeProperty(
        SymbolAnalysisContext context,
        INamedTypeSymbol component,
        INamedTypeSymbol inject,
        INamedTypeSymbol? sender,
        INamedTypeSymbol? dbContext)
    {
        if (context.Symbol is not IPropertySymbol property
            || !IsComponent(property.ContainingType, component)
            || !property.GetAttributes().Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, inject)))
        {
            return;
        }

        string advice;
        if (sender is not null && SymbolEqualityComparer.Default.Equals(property.Type, sender))
        {
            advice = "inject ScopedSender instead, which sends each request in its own scope";
        }
        else if (dbContext is not null && DerivesFrom(property.Type, dbContext))
        {
            advice = "inject IDbContextFactory<T> instead and create a context per operation";
        }
        else
        {
            return;
        }

        var location = property.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(context.CancellationToken) is PropertyDeclarationSyntax declaration
            ? declaration.Type.GetLocation()
            : property.Locations[0];
        context.ReportDiagnostic(Diagnostic.Create(LongLivedScopeServiceRule, location, property.Type.Name, advice));
    }

    private static bool IsComponent(INamedTypeSymbol? type, INamedTypeSymbol component)
    {
        return type is not null && type.AllInterfaces.Any(candidate => SymbolEqualityComparer.Default.Equals(candidate, component));
    }

    private static bool DerivesFrom(ITypeSymbol type, INamedTypeSymbol baseType)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }
        }

        return false;
    }
}
