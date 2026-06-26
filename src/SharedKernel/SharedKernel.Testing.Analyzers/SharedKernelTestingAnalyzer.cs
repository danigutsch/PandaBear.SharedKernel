using System.Collections.Concurrent;
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

    private const string TestingCategory = "Testing";

    private static readonly Regex XunitMethodNamingRegex = new(
        @"^[A-Z][A-Za-z0-9]*(?:_[A-Za-z0-9][A-Za-z0-9]*)+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        RegexTimeout);

    private static readonly DiagnosticDescriptor TestMethodWarningSuppressionRule = new(
        TestingDiagnosticIds.TestMethodWarningSuppression,
        title: "Test methods should not use pragma warning suppressions",
        messageFormat: "Test method '{0}' should not use '#pragma warning {1}' directives",
        category: TestingCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Repository testing rules discourage local pragma warning suppressions inside xUnit test methods; prefer analyzer-compliant test code or broader justified suppression scopes when unavoidable.");

    private static readonly DiagnosticDescriptor XunitTestMethodNamingRule = new(
        TestingDiagnosticIds.XunitTestMethodNaming,
        title: "xUnit test methods should follow the underscore naming convention",
        messageFormat: "xUnit test method '{0}' should follow the underscore naming convention",
        category: TestingCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Repository testing rules require xUnit test methods to use descriptive underscore-separated names such as 'Creates_a_tour_when_the_request_is_valid'.");

    private static readonly DiagnosticDescriptor XunitTestMethodRequiredTraitRule = new(
        TestingDiagnosticIds.XunitTestMethodRequiredTrait,
        title: "xUnit test methods should include required trait metadata",
        messageFormat: "xUnit test method '{0}' should include trait '{1}' with value '{2}' configured by sharedkernel_testing_required_traits",
        category: TestingCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Repository testing rules can require xUnit trait metadata through the sharedkernel_testing_required_traits .editorconfig key so MTP trait filters remain reliable.");

    private static readonly DiagnosticDescriptor XunitTestClassHelperMethodRule = new(
        TestingDiagnosticIds.XunitTestClassHelperMethod,
        title: "xUnit test classes should not declare private or reused internal static helpers directly",
        messageFormat: "xUnit test class helper member '{0}' should be moved to a dedicated helper type or kept in the test body when local",
        category: TestingCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Repository testing rules keep test behavior visible by requiring private helpers and reused internal static helpers to live in dedicated helper types or local functions instead of directly on xUnit test classes.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [TestMethodWarningSuppressionRule, XunitTestMethodNamingRule, XunitTestMethodRequiredTraitRule, XunitTestClassHelperMethodRule];

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
        context.RegisterCompilationStartAction(static compilationContext =>
        {
            var requiredTraitsByTree = new ConcurrentDictionary<SyntaxTree, ImmutableArray<RequiredTrait>>();
            var helperUsageCountsByType = new ConcurrentDictionary<INamedTypeSymbol, ImmutableDictionary<IMethodSymbol, int>>(SymbolEqualityComparer.Default);
            compilationContext.RegisterSyntaxNodeAction(
                context => AnalyzeMethodDeclaration(context, requiredTraitsByTree, helperUsageCountsByType),
                SyntaxKind.MethodDeclaration);
            compilationContext.RegisterSyntaxNodeAction(
                AnalyzeTypeDeclaration,
                SyntaxKind.ClassDeclaration,
                SyntaxKind.RecordDeclaration);
        });
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

    private static void AnalyzeMethodDeclaration(
        SyntaxNodeAnalysisContext context,
        ConcurrentDictionary<SyntaxTree, ImmutableArray<RequiredTrait>> requiredTraitsByTree,
        ConcurrentDictionary<INamedTypeSymbol, ImmutableDictionary<IMethodSymbol, int>> helperUsageCountsByType)
    {
        if (context.Node is not MethodDeclarationSyntax methodDeclaration)
        {
            return;
        }

        var isPotentialXunitTestMethod = IsPotentialXunitTestMethodDeclaration(methodDeclaration);
        var isPotentialHelperMethod = IsPotentialXunitHelperMethodDeclaration(methodDeclaration);
        if (!isPotentialXunitTestMethod && !isPotentialHelperMethod)
        {
            return;
        }

        if (context.SemanticModel.GetDeclaredSymbol(methodDeclaration, context.CancellationToken) is not IMethodSymbol methodSymbol)
        {
            return;
        }

        if (!isPotentialXunitTestMethod || !IsXunitTestMethod(methodSymbol))
        {
            AnalyzeTestClassHelperMethod(context, methodDeclaration, methodSymbol, helperUsageCountsByType);
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

        var requiredTraits = requiredTraitsByTree.GetOrAdd(
            methodDeclaration.SyntaxTree,
            syntaxTree => TestingAnalyzerConfigOptions.Parse(context.Options.AnalyzerConfigOptionsProvider, syntaxTree).RequiredTraits);
        foreach (var requiredTrait in requiredTraits)
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

    private static void AnalyzeTestClassHelperMethod(
        SyntaxNodeAnalysisContext context,
        MethodDeclarationSyntax methodDeclaration,
        IMethodSymbol methodSymbol,
        ConcurrentDictionary<INamedTypeSymbol, ImmutableDictionary<IMethodSymbol, int>> helperUsageCountsByType)
    {
        if (IsDisposeLifecycleMethod(methodSymbol)
            || !ContainsXunitTestMethod(methodSymbol.ContainingType))
        {
            return;
        }

        var reportsPrivateHelper = methodSymbol.DeclaredAccessibility == Accessibility.Private;
        var reportsReusedStaticHelper = methodSymbol.IsStatic
            && IsInvokedByMultipleXunitTestMethods(context.SemanticModel, methodDeclaration, methodSymbol, helperUsageCountsByType, context.CancellationToken);

        if (!reportsPrivateHelper && !reportsReusedStaticHelper)
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                XunitTestClassHelperMethodRule,
                methodDeclaration.Identifier.GetLocation(),
                methodSymbol.Name));
    }

    private static void AnalyzeTypeDeclaration(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not TypeDeclarationSyntax typeDeclaration
            || context.SemanticModel.GetDeclaredSymbol(typeDeclaration, context.CancellationToken) is not INamedTypeSymbol typeSymbol
            || typeSymbol.DeclaredAccessibility == Accessibility.Public
            || typeSymbol.TypeKind != TypeKind.Class
            || typeSymbol.ContainingType is null
            || !ContainsXunitTestMethod(typeSymbol.ContainingType))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                XunitTestClassHelperMethodRule,
                typeDeclaration.Identifier.GetLocation(),
                typeSymbol.Name));
    }

    private static bool IsInvokedByMultipleXunitTestMethods(
        SemanticModel semanticModel,
        MethodDeclarationSyntax methodDeclaration,
        IMethodSymbol methodSymbol,
        ConcurrentDictionary<INamedTypeSymbol, ImmutableDictionary<IMethodSymbol, int>> helperUsageCountsByType,
        CancellationToken ct)
    {
        if (methodDeclaration.Parent is not TypeDeclarationSyntax typeDeclaration)
        {
            return false;
        }

        var usageCounts = helperUsageCountsByType.GetOrAdd(
            methodSymbol.ContainingType,
            _ => GetXunitHelperUsageCounts(semanticModel, typeDeclaration, ct));

        return usageCounts.TryGetValue(methodSymbol.OriginalDefinition, out var usageCount) && usageCount > 1;
    }

    private static ImmutableDictionary<IMethodSymbol, int> GetXunitHelperUsageCounts(
        SemanticModel semanticModel,
        TypeDeclarationSyntax typeDeclaration,
        CancellationToken ct)
    {
        var callersByHelper = new Dictionary<IMethodSymbol, HashSet<IMethodSymbol>>(SymbolEqualityComparer.Default);
        foreach (var invocation in typeDeclaration.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            ct.ThrowIfCancellationRequested();

            if (semanticModel.GetSymbolInfo(invocation, ct).Symbol is not IMethodSymbol invokedMethod
                || invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>() is not MethodDeclarationSyntax callerDeclaration
                || semanticModel.GetDeclaredSymbol(callerDeclaration, ct) is not IMethodSymbol callerSymbol
                || !IsXunitTestMethod(callerSymbol))
            {
                continue;
            }

            var helperSymbol = invokedMethod.OriginalDefinition;
            if (!callersByHelper.TryGetValue(helperSymbol, out var callers))
            {
                callers = new(SymbolEqualityComparer.Default);
                callersByHelper.Add(helperSymbol, callers);
            }

            callers.Add(callerSymbol);
        }

        var usageCounts = ImmutableDictionary.CreateBuilder<IMethodSymbol, int>(SymbolEqualityComparer.Default);
        foreach (var pair in callersByHelper)
        {
            usageCounts.Add(pair.Key, pair.Value.Count);
        }

        return usageCounts.ToImmutable();
    }

    private static bool ContainsXunitTestMethod(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Any(static method => method.MethodKind == MethodKind.Ordinary && IsXunitTestMethod(method));
    }

    private static bool IsPotentialXunitHelperMethodDeclaration(MethodDeclarationSyntax methodDeclaration)
    {
        return methodDeclaration.Parent is TypeDeclarationSyntax
            && (!methodDeclaration.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.PublicKeyword) || modifier.IsKind(SyntaxKind.InternalKeyword) || modifier.IsKind(SyntaxKind.ProtectedKeyword))
                || (methodDeclaration.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.StaticKeyword))
                    && methodDeclaration.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.InternalKeyword))));
    }

    private static bool IsDisposeLifecycleMethod(IMethodSymbol methodSymbol)
    {
        return methodSymbol.Parameters.Length == 0
            && (methodSymbol.ExplicitInterfaceImplementations.Any(static implementation =>
                implementation.Name is nameof(IDisposable.Dispose) or "DisposeAsync"
                && implementation.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) is "global::System.IDisposable" or "global::System.IAsyncDisposable")
                || (methodSymbol.DeclaredAccessibility == Accessibility.Public
                    && methodSymbol.Name switch
                    {
                        nameof(IDisposable.Dispose) => ImplementsInterface(methodSymbol.ContainingType, "global::System.IDisposable"),
                        "DisposeAsync" => ImplementsInterface(methodSymbol.ContainingType, "global::System.IAsyncDisposable"),
                        _ => false,
                    }));
    }

    private static bool ImplementsInterface(INamedTypeSymbol typeSymbol, string interfaceName)
    {
        return typeSymbol.AllInterfaces.Any(candidate =>
            string.Equals(candidate.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), interfaceName, StringComparison.Ordinal));
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
