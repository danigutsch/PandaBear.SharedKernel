using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SharedKernel.Testing.Analyzers;

/// <summary>
/// Reports diagnostics for repository-specific testing rules.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SharedKernelTestingAnalyzer : DiagnosticAnalyzer
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    private static readonly Regex XunitMethodNamingRegex = new(
        @"^[A-Z][A-Za-z0-9]*(?:_[A-Za-z0-9][A-Za-z0-9]*)+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);

    private static readonly DiagnosticDescriptor TestMethodWarningSuppressionRule = new(
        TestingDiagnosticIds.TestMethodWarningSuppression,
        title: "Test methods should not use pragma warning suppressions",
        messageFormat: "Test method '{0}' should not use '#pragma warning {1}' directives",
        category: "Testing",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Repository testing rules discourage local pragma warning suppressions inside xUnit test methods; prefer analyzer-compliant test code or broader justified suppression scopes when unavoidable.");

    private static readonly DiagnosticDescriptor XunitTestMethodNamingRule = new(
        TestingDiagnosticIds.XunitTestMethodNaming,
        title: "xUnit test methods should follow the underscore naming convention",
        messageFormat: "xUnit test method '{0}' should follow the underscore naming convention",
        category: "Testing",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Repository testing rules require xUnit test methods to use descriptive underscore-separated names such as 'Creates_a_tour_when_the_request_is_valid'.");

    private static readonly DiagnosticDescriptor XunitTestMethodRequiredTraitRule = new(
        TestingDiagnosticIds.XunitTestMethodRequiredTrait,
        title: "xUnit test methods should include required trait metadata",
        messageFormat: "xUnit test method '{0}' should include trait '{1}' with value '{2}'",
        category: "Testing",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Repository testing rules can require configured xUnit trait metadata so MTP trait filters remain reliable.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [TestMethodWarningSuppressionRule, XunitTestMethodNamingRule, XunitTestMethodRequiredTraitRule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            AnalyzePragmaDirective,
            SyntaxKind.PragmaWarningDirectiveTrivia);
        context.RegisterSyntaxNodeAction(
            AnalyzeMethodDeclaration,
            SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzePragmaDirective(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not PragmaWarningDirectiveTriviaSyntax pragmaDirective
            || pragmaDirective.ParentTrivia.Token.Parent?.FirstAncestorOrSelf<MethodDeclarationSyntax>() is not MethodDeclarationSyntax methodDeclaration
            || context.SemanticModel.GetDeclaredSymbol(methodDeclaration, context.CancellationToken) is not IMethodSymbol methodSymbol
            || !IsXunitTestMethod(methodSymbol))
        {
            return;
        }

        var action = pragmaDirective.DisableOrRestoreKeyword.IsKind(SyntaxKind.DisableKeyword)
            ? "disable"
            : "restore";

        context.ReportDiagnostic(
            Diagnostic.Create(
                TestMethodWarningSuppressionRule,
                pragmaDirective.GetLocation(),
                methodSymbol.Name,
                action));
    }

    private static bool IsXunitTestMethod(IMethodSymbol methodSymbol)
    {
        return methodSymbol.GetAttributes().Any(static attribute =>
            attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) is "global::Xunit.FactAttribute"
                or "global::Xunit.TheoryAttribute");
    }

    private static void AnalyzeMethodDeclaration(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not MethodDeclarationSyntax methodDeclaration
            || !IsPotentialXunitTestMethodDeclaration(methodDeclaration)
            || context.SemanticModel.GetDeclaredSymbol(methodDeclaration, context.CancellationToken) is not IMethodSymbol methodSymbol
            || !IsXunitTestMethod(methodSymbol))
        {
            return;
        }

        if (!XunitMethodNamingRegex.IsMatch(methodSymbol.Name))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    XunitTestMethodNamingRule,
                    methodDeclaration.Identifier.GetLocation(),
                    methodSymbol.Name));
        }

        var options = TestingAnalyzerConfigOptions.Parse(context.Options.AnalyzerConfigOptionsProvider, methodDeclaration.SyntaxTree);
        foreach (var requiredTrait in options.RequiredTraits)
        {
            if (HasTrait(methodSymbol, requiredTrait))
            {
                continue;
            }

            var properties = ImmutableDictionary<string, string?>.Empty
                .Add("TraitName", requiredTrait.Name)
                .Add("TraitValue", requiredTrait.Value);

            context.ReportDiagnostic(
                Diagnostic.Create(
                    XunitTestMethodRequiredTraitRule,
                    methodDeclaration.Identifier.GetLocation(),
                    properties,
                    methodSymbol.Name,
                    requiredTrait.Name,
                    requiredTrait.Value));
        }
    }

    private static bool HasTrait(IMethodSymbol methodSymbol, RequiredTrait requiredTrait)
    {
        return HasTrait(methodSymbol.GetAttributes(), requiredTrait)
            || HasTrait(methodSymbol.ContainingType.GetAttributes(), requiredTrait)
            || HasTrait(methodSymbol.ContainingAssembly.GetAttributes(), requiredTrait);
    }

    private static bool HasTrait(ImmutableArray<AttributeData> attributes, RequiredTrait requiredTrait)
    {
        return attributes.Any(attribute =>
            attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) is "global::Xunit.TraitAttribute"
            && attribute.ConstructorArguments.Length == 2
            && string.Equals(attribute.ConstructorArguments[0].Value as string, requiredTrait.Name, StringComparison.Ordinal)
            && string.Equals(attribute.ConstructorArguments[1].Value as string, requiredTrait.Value, StringComparison.Ordinal));
    }

    private static bool IsPotentialXunitTestMethodDeclaration(MethodDeclarationSyntax methodDeclaration)
    {
        if (methodDeclaration.AttributeLists.Count == 0)
        {
            return false;
        }

        return methodDeclaration.AttributeLists
            .SelectMany(static attributeList => attributeList.Attributes)
            .Select(static attribute => attribute.Name.ToString())
            .Any(static name =>
                name.EndsWith("Fact", StringComparison.Ordinal)
                || name.EndsWith("FactAttribute", StringComparison.Ordinal)
                || name.EndsWith("Theory", StringComparison.Ordinal)
                || name.EndsWith("TheoryAttribute", StringComparison.Ordinal));
    }

}
