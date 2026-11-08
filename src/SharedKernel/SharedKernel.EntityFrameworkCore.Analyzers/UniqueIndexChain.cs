using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PandaBear.EntityFrameworkCore.Analyzers;

/// <summary>Syntax and symbol helpers that decide whether a unique index declaration lacks a <c>HasUniqueRule</c> call.</summary>
internal static class UniqueIndexChain
{
    private const string RuleMethodName = "HasUniqueRule";
    private const string AttributeSuffix = " (the [Index] attribute cannot carry a rule; configure the index with the fluent API instead)";

    /// <summary>Reports an <c>IsUnique</c> call on an index builder whose chain, or whose variable, never calls <c>HasUniqueRule</c>.</summary>
    internal static void Analyze(
        SyntaxNodeAnalysisContext context,
        DiagnosticDescriptor descriptor,
        INamedTypeSymbol ruleExtensions,
        INamedTypeSymbol? indexBuilder,
        INamedTypeSymbol? indexBuilderOfT)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (invocation.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: "IsUnique" })
        {
            return;
        }

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method
            || !IsIndexBuilder(method.ContainingType, indexBuilder, indexBuilderOfT)
            || !IsUniqueRequested(context, invocation))
        {
            return;
        }

        var top = TopOfChain(invocation);
        var chain = new List<InvocationExpressionSyntax>();
        CollectChain(top, chain);
        if (chain.Any(call => IsRuleCall(context.SemanticModel, call, ruleExtensions, context.CancellationToken)))
        {
            return;
        }

        var locals = new List<ILocalSymbol>();
        AddLocal(context.SemanticModel, RootOf(top), locals, context.CancellationToken);
        AddDeclaredTarget(context.SemanticModel, top, locals, context.CancellationToken);
        if (locals.Count > 0 && LocalGetsRule(context, locals, ruleExtensions))
        {
            return;
        }

        var properties = DescribeIndex(context.SemanticModel, chain, context.CancellationToken);
        context.ReportDiagnostic(Diagnostic.Create(descriptor, invocation.GetLocation(), properties, string.Empty));
    }

    /// <summary>Reports <c>[Index(..., IsUnique = true)]</c>, which cannot carry a rule.</summary>
    internal static void AnalyzeAttribute(SyntaxNodeAnalysisContext context, DiagnosticDescriptor descriptor, INamedTypeSymbol indexAttribute)
    {
        var attribute = (AttributeSyntax)context.Node;
        if (attribute.ArgumentList is null
            || context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is not IMethodSymbol constructor
            || !SymbolEqualityComparer.Default.Equals(constructor.ContainingType, indexAttribute))
        {
            return;
        }

        var isUnique = false;
        var names = new List<string>();
        foreach (var argument in attribute.ArgumentList.Arguments)
        {
            if (argument.NameEquals is { Name.Identifier.ValueText: "IsUnique" })
            {
                var value = context.SemanticModel.GetConstantValue(argument.Expression, context.CancellationToken);
                isUnique = value.HasValue && value.Value is true;
            }
            else if (argument.NameEquals is null)
            {
                var value = context.SemanticModel.GetConstantValue(argument.Expression, context.CancellationToken);
                if (value.HasValue && value.Value is string name)
                {
                    names.Add(name);
                }
            }
        }

        if (isUnique)
        {
            context.ReportDiagnostic(Diagnostic.Create(descriptor, attribute.GetLocation(), Describe(names), AttributeSuffix));
        }
    }

    private static bool IsIndexBuilder(INamedTypeSymbol? type, INamedTypeSymbol? indexBuilder, INamedTypeSymbol? indexBuilderOfT)
    {
        if (type is null)
        {
            return false;
        }

        var definition = type.OriginalDefinition;
        return SymbolEqualityComparer.Default.Equals(definition, indexBuilder)
            || SymbolEqualityComparer.Default.Equals(definition, indexBuilderOfT);
    }

    private static bool IsUniqueRequested(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        if (invocation.ArgumentList.Arguments.Count == 0)
        {
            return true;
        }

        var value = context.SemanticModel.GetConstantValue(invocation.ArgumentList.Arguments[0].Expression, context.CancellationToken);
        return value.HasValue && value.Value is true;
    }

    private static bool IsRuleCall(SemanticModel model, InvocationExpressionSyntax call, INamedTypeSymbol ruleExtensions, System.Threading.CancellationToken token)
    {
        if (call.Expression is not MemberAccessExpressionSyntax { Name.Identifier.ValueText: RuleMethodName })
        {
            return false;
        }

        return model.GetSymbolInfo(call, token).Symbol is IMethodSymbol method
            && SymbolEqualityComparer.Default.Equals(method.ContainingType, ruleExtensions);
    }

    private static InvocationExpressionSyntax TopOfChain(InvocationExpressionSyntax invocation)
    {
        var current = invocation;
        while (current.Parent is MemberAccessExpressionSyntax access
            && access.Expression == current
            && access.Parent is InvocationExpressionSyntax parent
            && parent.Expression == access)
        {
            current = parent;
        }

        return current;
    }

    private static void CollectChain(ExpressionSyntax? node, List<InvocationExpressionSyntax> chain)
    {
        while (true)
        {
            switch (node)
            {
                case InvocationExpressionSyntax call:
                    chain.Add(call);
                    node = call.Expression;
                    break;
                case MemberAccessExpressionSyntax access:
                    node = access.Expression;
                    break;
                case ParenthesizedExpressionSyntax parenthesized:
                    node = parenthesized.Expression;
                    break;
                default:
                    return;
            }
        }
    }

    private static ExpressionSyntax? RootOf(ExpressionSyntax top)
    {
        var node = (ExpressionSyntax?)top;
        while (true)
        {
            switch (node)
            {
                case InvocationExpressionSyntax call:
                    node = call.Expression;
                    break;
                case MemberAccessExpressionSyntax access:
                    node = access.Expression;
                    break;
                case ParenthesizedExpressionSyntax parenthesized:
                    node = parenthesized.Expression;
                    break;
                default:
                    return node;
            }
        }
    }

    private static void AddDeclaredTarget(SemanticModel model, InvocationExpressionSyntax top, List<ILocalSymbol> locals, System.Threading.CancellationToken token)
    {
        if (top.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax declarator })
        {
            if (model.GetDeclaredSymbol(declarator, token) is ILocalSymbol declared)
            {
                locals.Add(declared);
            }
        }
        else if (top.Parent is AssignmentExpressionSyntax assignment && assignment.Right == top)
        {
            AddLocal(model, assignment.Left, locals, token);
        }
    }

    private static void AddLocal(SemanticModel model, ExpressionSyntax? expression, List<ILocalSymbol> locals, System.Threading.CancellationToken token)
    {
        if (expression is not null && model.GetSymbolInfo(expression, token).Symbol is ILocalSymbol local)
        {
            locals.Add(local);
        }
    }

    private static bool LocalGetsRule(SyntaxNodeAnalysisContext context, List<ILocalSymbol> locals, INamedTypeSymbol ruleExtensions)
    {
        foreach (var local in locals)
        {
            var reference = local.DeclaringSyntaxReferences.FirstOrDefault();
            var scope = reference?.GetSyntax(context.CancellationToken).FirstAncestorOrSelf<BlockSyntax>();
            if (scope is null)
            {
                continue;
            }

            foreach (var call in scope.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (!IsRuleCall(context.SemanticModel, call, ruleExtensions, context.CancellationToken))
                {
                    continue;
                }

                var root = RootOf(call);
                if (root is not null
                    && context.SemanticModel.GetSymbolInfo(root, context.CancellationToken).Symbol is ILocalSymbol rootLocal
                    && SymbolEqualityComparer.Default.Equals(rootLocal, local))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string DescribeIndex(SemanticModel model, List<InvocationExpressionSyntax> chain, System.Threading.CancellationToken token)
    {
        var names = new List<string>();
        var hasIndex = chain.FirstOrDefault(static call => call.Expression is MemberAccessExpressionSyntax { Name.Identifier.ValueText: "HasIndex" });
        if (hasIndex is not null)
        {
            foreach (var argument in hasIndex.ArgumentList.Arguments)
            {
                switch (argument.Expression)
                {
                    case LambdaExpressionSyntax { Body: MemberAccessExpressionSyntax access }:
                        names.Add(access.Name.Identifier.ValueText);
                        break;
                    case LambdaExpressionSyntax { Body: AnonymousObjectCreationExpressionSyntax creation }:
                        foreach (var initializer in creation.Initializers)
                        {
                            if (initializer.Expression is MemberAccessExpressionSyntax member)
                            {
                                names.Add(member.Name.Identifier.ValueText);
                            }
                        }

                        break;
                    default:
                        var value = model.GetConstantValue(argument.Expression, token);
                        if (value.HasValue && value.Value is string name)
                        {
                            names.Add(name);
                        }

                        break;
                }
            }
        }

        return Describe(names);
    }

    private static string Describe(List<string> names) =>
        names.Count == 0 ? string.Empty : " on '" + string.Join(", ", names) + "'";
}
