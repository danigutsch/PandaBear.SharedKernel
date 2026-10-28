using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PandaBear.Testing.Analyzers;

public sealed partial class SharedKernelTestingAnalyzer
{
    private const string ScopeTraitName = "Scope";
    private const string TestScopeAttributeName = "global::PandaBear.Testing.TestScopeAttribute";
    private const string TraitAttributeName = "global::Xunit.TraitAttribute";

    private static readonly DiagnosticDescriptor XunitTestScopeRule = new(
        TestingDiagnosticIds.XunitTestScope,
        "xUnit test methods should declare exactly one valid test scope",
        "xUnit test method '{0}' {1}",
        TestingCategory, DiagnosticSeverity.Info, true,
        description: "Every xUnit test declares the seam it exercises through TestScope or Trait(\"Scope\", ...) on the method, an enclosing or base type, or the assembly. The nearest level that declares a scope wins. Raise the severity once a repository is fully tagged; narrow the allowed values with sharedkernel_testing_allowed_scopes.",
        helpLinkUri: "https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Testing.Analyzers/README.md#sktest018");

    private static void AnalyzeTestScope(
        SyntaxNodeAnalysisContext context,
        MethodDeclarationSyntax methodDeclaration,
        IMethodSymbol methodSymbol,
        TestingAnalyzerConfigOptions options)
    {
        foreach (var level in EnumerateScopeLevels(methodSymbol))
        {
            var scopes = ReadScopes(level);
            if (scopes.Count == 0)
            {
                continue;
            }

            if (scopes.Count > 1)
            {
                Report(context, methodDeclaration, methodSymbol, options, "conflicting", $"declares conflicting test scopes {Quote(scopes)} at the same level");
                return;
            }

            var scope = scopes[0];
            if (!options.AllowedScopes.Contains(scope, StringComparer.Ordinal))
            {
                Report(context, methodDeclaration, methodSymbol, options, "unknown", $"declares unknown test scope '{scope}'; allowed: {string.Join(", ", options.AllowedScopes)}");
            }

            return;
        }

        Report(context, methodDeclaration, methodSymbol, options, "missing", "has no test scope; declare one on the method, its class, a base class or the assembly");
    }

    private static void Report(
        SyntaxNodeAnalysisContext context,
        MethodDeclarationSyntax methodDeclaration,
        IMethodSymbol methodSymbol,
        TestingAnalyzerConfigOptions options,
        string reason,
        string message)
    {
        var typed = context.SemanticModel.Compilation.GetTypeByMetadataName("PandaBear.Testing.TestScopeAttribute") is not null;
        var properties = ImmutableDictionary<string, string?>.Empty
            .Add("Reason", reason)
            .Add("AllowedScopes", string.Join(";", options.AllowedScopes))
            .Add("TypedAttribute", typed ? "true" : "false");

        context.ReportDiagnostic(
            Diagnostic.Create(
                XunitTestScopeRule,
                methodDeclaration.Identifier.GetLocation(),
                properties,
                methodSymbol.Name,
                message));
    }

    private static string Quote(List<string> scopes) => string.Join(", ", scopes.Select(static scope => $"'{scope}'"));

    private static IEnumerable<ImmutableArray<AttributeData>> EnumerateScopeLevels(IMethodSymbol methodSymbol)
    {
        yield return methodSymbol.GetAttributes();

        for (var type = methodSymbol.ContainingType; type is not null; type = type.ContainingType)
        {
            for (var current = type; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
            {
                yield return current.GetAttributes();
            }
        }

        yield return methodSymbol.ContainingAssembly.GetAttributes();
    }

    /// <summary>Returns the distinct scope values declared in <paramref name="attributes"/>, in declaration order.</summary>
    private static List<string> ReadScopes(ImmutableArray<AttributeData> attributes)
    {
        var scopes = new List<string>();
        foreach (var attribute in attributes)
        {
            var scope = GetScope(attribute);
            if (scope is not null && !scopes.Contains(scope, StringComparer.Ordinal))
            {
                scopes.Add(scope);
            }
        }

        return scopes;
    }

    private static string? GetScope(AttributeData attribute)
    {
        var attributeName = attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var arguments = attribute.ConstructorArguments;
        if (attributeName == TestScopeAttributeName && arguments.Length == 1 && arguments[0].Value is int index)
        {
            return index >= 0 && index < TestScopeValues.All.Length ? TestScopeValues.All[index] : $"undefined({index})";
        }

        if (attributeName == TraitAttributeName
            && arguments.Length == 2
            && arguments[0].Value is string name
            && arguments[1].Value is string value
            && string.Equals(name, ScopeTraitName, StringComparison.Ordinal))
        {
            return value;
        }

        return null;
    }

    /// <summary>Reports a literal <c>Trait("Scope", value)</c> when <c>TestScope</c> is referenced, because the typed attribute is the canonical form.</summary>
    private static bool TryReportTypedScope(SyntaxNodeAnalysisContext context, AttributeSyntax attribute)
    {
        if (attribute.ArgumentList is not { Arguments.Count: 2 } argumentList
            || argumentList.Arguments[0].Expression is not LiteralExpressionSyntax { Token.Value: string name }
            || !string.Equals(name, ScopeTraitName, StringComparison.Ordinal)
            || argumentList.Arguments[1].Expression is not LiteralExpressionSyntax { Token.Value: string value }
            || !TestScopeValues.All.Contains(value, StringComparer.Ordinal)
            || context.Compilation.GetTypeByMetadataName("PandaBear.Testing.TestScopeAttribute") is null)
        {
            return false;
        }

        var member = TestScopeValues.ToMemberName(value);
        var properties = ImmutableDictionary<string, string?>.Empty
            .Add("Replacement", $"global::PandaBear.Testing.TestScope(global::PandaBear.Testing.TestScope.{member})")
            .Add("Kind", "TestScope");
        context.ReportDiagnostic(
            Diagnostic.Create(
                XunitTraitConstantUsageRule,
                attribute.GetLocation(),
                properties,
                $"Trait(\"{name}\", \"{value}\")",
                $"TestScope(TestScope.{member})"));

        return true;
    }
}
