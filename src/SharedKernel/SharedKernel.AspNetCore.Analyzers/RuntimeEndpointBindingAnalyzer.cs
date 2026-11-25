using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SharedKernel.AspNetCore.Analyzers;

/// <summary>Checks that intentional Minimal API runtime binding has explicit AOT and trimming annotations.</summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class RuntimeEndpointBindingAnalyzer : DiagnosticAnalyzer
{
    private const string EndpointExtensions = "Microsoft.AspNetCore.Builder.EndpointRouteBuilderExtensions";

    private static readonly DiagnosticDescriptor Rule = new(
        RuntimeEndpointDiagnostics.Id,
        "Annotate intentional runtime endpoint binding",
        "Endpoint mapping method '{0}' uses runtime binding and requires {1}",
        "AOT",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Prefer source-generated request delegates and annotate reviewed runtime binding fallbacks.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
        {
            var extensions = start.Compilation.GetTypeByMetadataName(EndpointExtensions);
            var dynamicCode = start.Compilation.GetTypeByMetadataName(RuntimeEndpointDiagnostics.DynamicCode);
            var unreferencedCode = start.Compilation.GetTypeByMetadataName(RuntimeEndpointDiagnostics.UnreferencedCode);
            if (extensions is not null && dynamicCode is not null && unreferencedCode is not null)
            {
                start.RegisterSyntaxNodeAction(action => AnalyzeMethod(action, extensions, dynamicCode, unreferencedCode),
                    SyntaxKind.MethodDeclaration, SyntaxKind.LocalFunctionStatement);
            }
        });
    }

    private static void AnalyzeMethod(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol extensions,
        INamedTypeSymbol dynamicCode,
        INamedTypeSymbol unreferencedCode)
    {
        var node = context.Node;
        var semanticModel = context.SemanticModel;
        var ct = context.CancellationToken;
        if (semanticModel.GetDeclaredSymbol(node, ct) is not IMethodSymbol method)
        {
            return;
        }

        var hasDynamicCode = HasAttribute(method, dynamicCode);
        var hasUnreferencedCode = HasAttribute(method, unreferencedCode);
        if (hasDynamicCode && hasUnreferencedCode)
        {
            return;
        }

        var missingDynamicCode = false;
        var missingUnreferencedCode = false;
        var invocations = node.ChildNodes()
            .Where(static child => child is BlockSyntax or ArrowExpressionClauseSyntax)
            .SelectMany(static body => body.DescendantNodes(static child =>
                child is not (LocalFunctionStatementSyntax or AnonymousFunctionExpressionSyntax)))
            .OfType<InvocationExpressionSyntax>();
        foreach (var invocation in invocations)
        {
            if (semanticModel.GetSymbolInfo(invocation, ct).Symbol is not IMethodSymbol original
                || !SymbolEqualityComparer.Default.Equals(original.ContainingType, extensions))
            {
                continue;
            }

            var target = semanticModel.GetInterceptorMethod(invocation, ct) ?? original;
            missingDynamicCode |= !hasDynamicCode && HasAttribute(target, dynamicCode);
            missingUnreferencedCode |= !hasUnreferencedCode && HasAttribute(target, unreferencedCode);
        }

        if (!missingDynamicCode && !missingUnreferencedCode)
        {
            return;
        }

        var annotations = ImmutableDictionary.CreateBuilder<string, string?>();
        var names = new List<string>();
        if (missingDynamicCode)
        {
            annotations.Add(RuntimeEndpointDiagnostics.DynamicCode, bool.TrueString);
            names.Add(RuntimeEndpointDiagnostics.DynamicCodeName);
        }

        if (missingUnreferencedCode)
        {
            annotations.Add(RuntimeEndpointDiagnostics.UnreferencedCode, bool.TrueString);
            names.Add(RuntimeEndpointDiagnostics.UnreferencedCodeName);
        }

        var location = method.Locations.First();
        context.ReportDiagnostic(Diagnostic.Create(Rule, location, annotations.ToImmutable(), method.Name, string.Join(" and ", names)));
    }

    private static bool HasAttribute(IMethodSymbol method, INamedTypeSymbol annotation) =>
        method.GetAttributes().Any(attribute => SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, annotation));
}
