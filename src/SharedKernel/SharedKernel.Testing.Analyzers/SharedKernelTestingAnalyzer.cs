using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PandaBear.Testing.Analyzers;

/// <summary>
/// Reports diagnostics for repository-specific testing rules.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed partial class SharedKernelTestingAnalyzer : DiagnosticAnalyzer
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(1);

    private const string TestingCategory = "Testing";
    private const string ArrangeMarker = "Arrange";
    private const string ActMarker = "Act";
    private const string AssertMarker = "Assert";

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
        title: "xUnit tests should not hide helper declarations inside test classes or test methods",
        messageFormat: "xUnit test helper '{0}' should be kept visible in the test body or moved to a dedicated helper type",
        category: TestingCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Repository testing rules keep test behavior visible by requiring helper methods, nested helper types, and local helper functions to live outside xUnit test classes and methods.");

    private static readonly DiagnosticDescriptor XunitSerialCollectionJustificationRule = new(
        TestingDiagnosticIds.XunitSerialCollectionJustification,
        title: "Serial xUnit collections should include a justification",
        messageFormat: "Serial xUnit collection '{0}' should declare a non-empty SerialTestJustification attribute",
        category: TestingCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Repository testing rules require collection definitions with DisableParallelization = true to explain why serial execution is necessary.");

    private static readonly DiagnosticDescriptor XunitAssertionWrapperRule = new(
        TestingDiagnosticIds.XunitAssertionWrapper,
        title: "Test code should use repository assertion wrappers",
        messageFormat: "Use PandaBear.Testing.Assertions instead of direct xUnit assertion '{0}'",
        category: TestingCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Repository testing rules require assertion calls to go through PandaBear.Testing.Assertions so test diagnostics and nullability behavior stay consistent.");

    private static readonly DiagnosticDescriptor XunitArrangeActAssertMarkersRule = new(
        TestingDiagnosticIds.XunitArrangeActAssertMarkers,
        title: "Explicit Arrange/Act/Assert markers should stay ordered",
        messageFormat: "xUnit test method '{0}' has out-of-order Arrange/Act/Assert markers",
        category: TestingCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Repository testing rules allow tests without Arrange/Act/Assert comments, but a complete explicit marker set should appear as Arrange, Act, and Assert in that order when used.");

    private static readonly DiagnosticDescriptor XunitTryStatementRule = new(
        TestingDiagnosticIds.XunitTryFinallyCleanup,
        title: "xUnit test methods should not use try blocks",
        messageFormat: "xUnit test method '{0}' should move try/catch/finally control flow to a fixture, disposable helper, or dedicated helper type",
        category: TestingCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Repository testing rules discourage manual try/catch/finally control flow inside xUnit test methods because it can hide Arrange/Act/Assert behavior and cleanup plumbing.");

    private static readonly DiagnosticDescriptor XunitTraitConstantUsageRule = new(
        TestingDiagnosticIds.XunitTraitConstantUsage,
        title: "xUnit trait metadata should use canonical constants",
        messageFormat: "xUnit trait literal '{0}' should use '{1}'",
        category: TestingCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Repository testing rules keep trait filters stable by requiring canonical constants when they exist for a trait name or value.");

    private static readonly DiagnosticDescriptor AssertionDelegateInliningRule = new(
        TestingDiagnosticIds.AssertionDelegateInlining,
        title: "Inline one-use assertion delegates",
        messageFormat: "Inline the one-use lambda into the assertion call",
        category: TestingCategory,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Inline adjacent one-use lambda delegates only when the repository assertion overload remains unchanged.",
        helpLinkUri: "https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Testing.Analyzers/README.md#sktest011");

    private static readonly DiagnosticDescriptor SingleAssertionRule = new(
        TestingDiagnosticIds.LinqSingleAssertion,
        title: "Use a uniqueness assertion in tests",
        messageFormat: "Use ShouldHaveSingleItem instead of LINQ Single",
        category: TestingCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ParenthesizedAssertionRule = new(
        TestingDiagnosticIds.ParenthesizedAssertion,
        title: "Give assertion targets an explicit meaning",
        messageFormat: "Use a semantic assertion or name the parenthesized assertion target",
        category: TestingCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor ConditionalAssertionRule = new(
        TestingDiagnosticIds.ConditionalAssertion,
        title: "Conditional access must not skip assertions",
        messageFormat: "Assert the nullable receiver explicitly before calling {0}",
        category: TestingCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor PlaywrightAssertionRule = new(
        TestingDiagnosticIds.PlaywrightAssertion,
        title: "Use the Playwright assertion boundary",
        messageFormat: "Use ShouldHaveTitle instead of direct Playwright title assertions",
        category: TestingCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [ActivityListenerSourceIdentityRule, SutConstructionRule, TestMethodWarningSuppressionRule, XunitTestMethodNamingRule, XunitTestMethodRequiredTraitRule, XunitTestClassHelperMethodRule, XunitSerialCollectionJustificationRule, XunitAssertionWrapperRule, XunitArrangeActAssertMarkersRule, XunitTryStatementRule, XunitTraitConstantUsageRule, AssertionDelegateInliningRule, SingleAssertionRule, ParenthesizedAssertionRule, ConditionalAssertionRule, PlaywrightAssertionRule, AssertionUserMessageOverloadRule, XunitTestScopeRule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeListenerAssignment, OperationKind.SimpleAssignment);
        context.RegisterSyntaxNodeAction(
            AnalyzePragmaDirective,
            SyntaxKind.PragmaWarningDirectiveTrivia);
        context.RegisterSymbolAction(AnalyzeAssertionUserMessageOverload, SymbolKind.Method);
        context.RegisterCompilationStartAction(static compilationContext =>
        {
            var optionsByTree = new ConcurrentDictionary<SyntaxTree, TestingAnalyzerConfigOptions>();
            compilationContext.RegisterSyntaxNodeAction(
                context => AnalyzeMethodDeclaration(context, optionsByTree),
                SyntaxKind.MethodDeclaration);
            compilationContext.RegisterSyntaxNodeAction(
                AnalyzeTypeDeclaration,
                SyntaxKind.ClassDeclaration,
                SyntaxKind.RecordDeclaration);
            compilationContext.RegisterSyntaxNodeAction(
                AnalyzeLocalFunctionStatement,
                SyntaxKind.LocalFunctionStatement);
            compilationContext.RegisterSyntaxNodeAction(
                AnalyzeInvocationExpression,
                SyntaxKind.InvocationExpression);
            compilationContext.RegisterSyntaxNodeAction(
                AnalyzeTryStatement,
                SyntaxKind.TryStatement);
            compilationContext.RegisterSyntaxNodeAction(
                AnalyzeAttribute,
                SyntaxKind.Attribute);
        });
    }

    private static void AnalyzeAttribute(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not AttributeSyntax { ArgumentList.Arguments.Count: >= 2 } attribute
            || !HasTraitAttributeName(attribute)
            || !IsXunitTraitAttribute(context, attribute))
        {
            return;
        }

        AnalyzeTraitArgument(context, attribute, attribute.ArgumentList.Arguments[0], includeTraitValues: false);
        AnalyzeTraitArgument(context, attribute, attribute.ArgumentList.Arguments[1], includeTraitValues: true);
    }

    private static bool HasTraitAttributeName(AttributeSyntax attribute)
    {
        return GetRightmostAttributeIdentifier(attribute.Name) is "Trait" or "TraitAttribute";
    }

    private static string? GetRightmostAttributeIdentifier(NameSyntax name)
    {
        return name switch
        {
            IdentifierNameSyntax identifierName => identifierName.Identifier.ValueText,
            QualifiedNameSyntax qualifiedName => qualifiedName.Right.Identifier.ValueText,
            AliasQualifiedNameSyntax aliasQualifiedName => aliasQualifiedName.Name.Identifier.ValueText,
            _ => null,
        };
    }

    private static bool IsXunitTraitAttribute(SyntaxNodeAnalysisContext context, AttributeSyntax attribute)
    {
        return context.SemanticModel.GetSymbolInfo(attribute, context.CancellationToken).Symbol is IMethodSymbol constructor
            && constructor.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) is "global::Xunit.TraitAttribute";
    }

    private static void AnalyzeTraitArgument(
        SyntaxNodeAnalysisContext context,
        AttributeSyntax attribute,
        AttributeArgumentSyntax argument,
        bool includeTraitValues)
    {
        if (argument.Expression is not LiteralExpressionSyntax literalExpression
            || !literalExpression.IsKind(SyntaxKind.StringLiteralExpression)
            || literalExpression.Token.ValueText is not { Length: > 0 } literalValue
            || TryFindTraitConstantReplacement(context, attribute, literalValue, includeTraitValues) is not { Length: > 0 } replacement)
        {
            return;
        }

        var properties = ImmutableDictionary<string, string?>.Empty
            .Add("Replacement", replacement);

        context.ReportDiagnostic(
            Diagnostic.Create(
                XunitTraitConstantUsageRule,
                literalExpression.GetLocation(),
                properties,
                literalValue,
                replacement));
    }

    private static string? TryFindTraitConstantReplacement(
        SyntaxNodeAnalysisContext context,
        AttributeSyntax attribute,
        string literalValue,
        bool includeTraitValues)
    {
        var localReplacement = includeTraitValues
            ? TryFindLocalTestTraitsReplacement(context, attribute, literalValue)
            : null;
        if (localReplacement is not null)
        {
            return localReplacement;
        }

        var replacements = ImmutableArray.CreateBuilder<string>();
        AddConstantsFromType(context.Compilation.GetTypeByMetadataName("PandaBear.Testing.TestTraitNames"), literalValue, replacements);
        AddConstantsFromType(context.Compilation.GetTypeByMetadataName("PandaBear.Testing.SharedKernelTestTraitNames"), literalValue, replacements);

        if (includeTraitValues)
        {
            AddConstantsFromType(context.Compilation.GetTypeByMetadataName("PandaBear.Testing.TestTraitValues"), literalValue, replacements);
        }

        return TryGetSingleReplacement(replacements);
    }

    private static string? TryFindLocalTestTraitsReplacement(
        SyntaxNodeAnalysisContext context,
        AttributeSyntax attribute,
        string literalValue)
    {
        if (attribute.FirstAncestorOrSelf<BaseTypeDeclarationSyntax>() is not BaseTypeDeclarationSyntax containingTypeDeclaration
            || context.SemanticModel.GetDeclaredSymbol(containingTypeDeclaration, context.CancellationToken) is not INamedTypeSymbol containingType)
        {
            return null;
        }

        var replacements = ImmutableArray.CreateBuilder<string>();
        for (var @namespace = containingType.ContainingNamespace; @namespace is not null && !@namespace.IsGlobalNamespace; @namespace = @namespace.ContainingNamespace)
        {
            foreach (var testTraitsType in @namespace.GetTypeMembers("TestTraits"))
            {
                AddConstantsFromType(testTraitsType, literalValue, replacements);
            }
        }

        foreach (var testTraitsType in context.Compilation.GlobalNamespace.GetTypeMembers("TestTraits"))
        {
            AddConstantsFromType(testTraitsType, literalValue, replacements);
        }

        return TryGetSingleReplacement(replacements);
    }

    private static void AddConstantsFromType(INamedTypeSymbol? type, string literalValue, ImmutableArray<string>.Builder replacements)
    {
        if (type is null)
        {
            return;
        }

        foreach (var member in type.GetMembers().OfType<IFieldSymbol>())
        {
            if (!member.IsConst
                || member.Type.SpecialType != SpecialType.System_String
                || member.ConstantValue is not string constantValue
                || !string.Equals(constantValue, literalValue, StringComparison.Ordinal))
            {
                continue;
            }

            replacements.Add(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) + "." + member.Name);
        }
    }

    private static string? TryGetSingleReplacement(ImmutableArray<string>.Builder replacements)
    {
        var distinct = replacements.Distinct(StringComparer.Ordinal).ToArray();
        return distinct.Length == 1 ? distinct[0] : null;
    }

    private static void AnalyzeInvocationExpression(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocationExpression)
        {
            return;
        }

        AnalyzeAssertionInvocation(context, invocationExpression);
        AnalyzeUnsafeAssertion(context, invocationExpression);
        if (AssertionDelegateInlining.TryRewrite(context.SemanticModel, invocationExpression, context.CancellationToken, out _, out _))
        {
            context.ReportDiagnostic(Diagnostic.Create(AssertionDelegateInliningRule, invocationExpression.GetLocation()));
        }
    }

    private static void AnalyzeAssertionInvocation(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocationExpression)
    {
        if (context.SemanticModel.GetSymbolInfo(invocationExpression, context.CancellationToken).Symbol is not IMethodSymbol methodSymbol
            || methodSymbol.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) is not "global::Xunit.Assert")
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                XunitAssertionWrapperRule,
                invocationExpression.GetLocation(),
                methodSymbol.Name));
    }

    private static void AnalyzeUnsafeAssertion(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        var name = invocation.Expression switch
        {
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax binding => binding.Name.Identifier.ValueText,
            SimpleNameSyntax simple => simple.Identifier.ValueText,
            _ => string.Empty,
        };
        if (name != "Single" && name != "ToHaveTitleAsync" && !name.StartsWith("Should", StringComparison.Ordinal))
        {
            return;
        }

        if (invocation.FirstAncestorOrSelf<MethodDeclarationSyntax>() is not { } declaration
            || context.SemanticModel.GetDeclaredSymbol(declaration, context.CancellationToken) is not IMethodSymbol test
            || !IsXunitTestMethod(test)
            || context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is not IMethodSymbol method)
        {
            return;
        }

        var owner = method.ContainingType.ToDisplayString();
        if (name == "Single" && owner is "System.Linq.Enumerable" or "System.Linq.Queryable"
            && !IsDeliberateExceptionAssertion(context, invocation, declaration))
        {
            context.ReportDiagnostic(Diagnostic.Create(SingleAssertionRule, invocation.GetLocation()));
        }

        if (name.StartsWith("Should", StringComparison.Ordinal)
            && method.ContainingNamespace.ToDisplayString().StartsWith("PandaBear.Testing.", StringComparison.Ordinal))
        {
            if (invocation.Expression is MemberAccessExpressionSyntax { Expression: ParenthesizedExpressionSyntax })
            {
                context.ReportDiagnostic(Diagnostic.Create(ParenthesizedAssertionRule, invocation.GetLocation()));
            }

            if (name.StartsWith("ShouldBe", StringComparison.Ordinal)
                && invocation.Ancestors().OfType<ConditionalAccessExpressionSyntax>().Any(conditional => conditional.WhenNotNull.Span.Contains(invocation.Span)))
            {
                context.ReportDiagnostic(Diagnostic.Create(ConditionalAssertionRule, invocation.GetLocation(), name));
            }
        }

        if (name == "ToHaveTitleAsync" && owner == "Microsoft.Playwright.IPageAssertions"
            && invocation.ArgumentList.Arguments.Count == 1
            && method.Parameters[0].Type.SpecialType == SpecialType.System_String
            && context.Compilation.AssemblyName != "SharedKernel.Testing.Playwright")
        {
            context.ReportDiagnostic(Diagnostic.Create(PlaywrightAssertionRule, invocation.GetLocation()));
        }
    }

    private static bool IsDeliberateExceptionAssertion(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        MethodDeclarationSyntax declaration)
    {
        if (invocation.Ancestors().OfType<LambdaExpressionSyntax>().FirstOrDefault() is not { } lambda)
        {
            return false;
        }

        var local = lambda.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax variable }
            ? context.SemanticModel.GetDeclaredSymbol(variable, context.CancellationToken)
            : null;
        foreach (var candidate in declaration.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (context.SemanticModel.GetSymbolInfo(candidate, context.CancellationToken).Symbol is not IMethodSymbol method)
            {
                continue;
            }

            var isExceptionAssertion = method.ContainingType.ToDisplayString() == "Xunit.Assert"
                    && method.Name.StartsWith("Throws", StringComparison.Ordinal)
                || method.ContainingType.ToDisplayString() == "PandaBear.Testing.Assertions.ShouldAssertionExtensions"
                    && method.Name.StartsWith("ShouldThrow", StringComparison.Ordinal);
            if (isExceptionAssertion && (candidate.Span.Contains(lambda.Span)
                || local is not null && candidate.DescendantNodes().OfType<IdentifierNameSyntax>().Any(identifier =>
                    SymbolEqualityComparer.Default.Equals(context.SemanticModel.GetSymbolInfo(identifier, context.CancellationToken).Symbol, local))))
            {
                return true;
            }
        }

        return false;
    }

    private static void AnalyzeTryStatement(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not TryStatementSyntax tryStatement
            || tryStatement.FirstAncestorOrSelf<MethodDeclarationSyntax>() is not MethodDeclarationSyntax methodDeclaration
            || !IsPotentialXunitTestMethodDeclaration(methodDeclaration)
            || context.SemanticModel.GetDeclaredSymbol(methodDeclaration, context.CancellationToken) is not IMethodSymbol methodSymbol
            || !IsXunitTestMethod(methodSymbol))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                XunitTryStatementRule,
                tryStatement.TryKeyword.GetLocation(),
                methodSymbol.Name));
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
        ConcurrentDictionary<SyntaxTree, TestingAnalyzerConfigOptions> optionsByTree)
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
            AnalyzeTestClassHelperMethod(context, methodDeclaration, methodSymbol);
            return;
        }

        var options = optionsByTree.GetOrAdd(
            methodDeclaration.SyntaxTree,
            syntaxTree => TestingAnalyzerConfigOptions.Parse(context.Options.AnalyzerConfigOptionsProvider, syntaxTree));

        if (!HasValidXunitMethodName(methodSymbol.Name, options.StrictTestMethodCasing))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    XunitTestMethodNamingRule,
                    methodDeclaration.Identifier.GetLocation(),
                    methodSymbol.Name));
        }

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

        AnalyzeTestScope(context, methodDeclaration, methodSymbol, options);
        AnalyzeArrangeActAssertMarkers(context, methodDeclaration, methodSymbol);
        AnalyzeSutConstruction(context, methodDeclaration);
    }

    private static void AnalyzeArrangeActAssertMarkers(
        SyntaxNodeAnalysisContext context,
        MethodDeclarationSyntax methodDeclaration,
        IMethodSymbol methodSymbol)
    {
        var markers = GetArrangeActAssertMarkers(methodDeclaration);
        if (!HasSingleCompleteArrangeActAssertMarkerSet(markers) || HasOrderedArrangeActAssertMarkers(markers))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                XunitArrangeActAssertMarkersRule,
                methodDeclaration.Identifier.GetLocation(),
                methodSymbol.Name));
    }

    private static ImmutableArray<string> GetArrangeActAssertMarkers(MethodDeclarationSyntax methodDeclaration)
    {
        var markers = ImmutableArray.CreateBuilder<string>();

        foreach (var trivia in methodDeclaration.DescendantTrivia(descendIntoTrivia: false))
        {
            if (!trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
            {
                continue;
            }

            var comment = trivia.ToString().Substring(2).Trim();
            if (comment is ArrangeMarker or ActMarker or AssertMarker)
            {
                markers.Add(comment);
            }
        }

        return markers.ToImmutable();
    }

    private static bool HasOrderedArrangeActAssertMarkers(ImmutableArray<string> markers)
    {
        var currentOrder = 0;
        foreach (var marker in markers)
        {
            var markerOrder = marker switch
            {
                ArrangeMarker => 1,
                ActMarker => 2,
                AssertMarker => 3,
                _ => 0,
            };

            if (markerOrder < currentOrder)
            {
                return false;
            }

            currentOrder = markerOrder;
        }

        return true;
    }

    private static bool HasSingleCompleteArrangeActAssertMarkerSet(ImmutableArray<string> markers)
    {
        return markers.Length == 3
            && markers.Contains(ArrangeMarker, StringComparer.Ordinal)
            && markers.Contains(ActMarker, StringComparer.Ordinal)
            && markers.Contains(AssertMarker, StringComparer.Ordinal);
    }

    private static void AnalyzeTestClassHelperMethod(
        SyntaxNodeAnalysisContext context,
        MethodDeclarationSyntax methodDeclaration,
        IMethodSymbol methodSymbol)
    {
        if (IsXunitLifecycleMethod(methodSymbol)
            || !ContainsXunitTestMethod(methodSymbol.ContainingType))
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
            || typeSymbol.TypeKind != TypeKind.Class)
        {
            return;
        }

        AnalyzeSerialCollectionJustification(context, typeDeclaration, typeSymbol);

        if (typeSymbol.ContainingType is null
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

    private static void AnalyzeSerialCollectionJustification(
        SyntaxNodeAnalysisContext context,
        TypeDeclarationSyntax typeDeclaration,
        INamedTypeSymbol typeSymbol)
    {
        if (!HasSerialCollectionDefinition(typeSymbol) || HasSerialTestJustification(typeSymbol))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                XunitSerialCollectionJustificationRule,
                typeDeclaration.Identifier.GetLocation(),
                typeSymbol.Name));
    }

    private static bool HasSerialCollectionDefinition(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.GetAttributes().Any(static attribute =>
            attribute.AttributeClass?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) is "global::Xunit.CollectionDefinitionAttribute"
            && attribute.NamedArguments.Any(static argument =>
                string.Equals(argument.Key, "DisableParallelization", StringComparison.Ordinal)
                && argument.Value.Value is true));
    }

    private static bool HasSerialTestJustification(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.GetAttributes().Any(static attribute =>
            attribute.AttributeClass?.Name is "SerialTestJustificationAttribute"
            && attribute.ConstructorArguments.Length > 0
            && attribute.ConstructorArguments[0].Value is string reason
            && !string.IsNullOrWhiteSpace(reason));
    }

    private static void AnalyzeLocalFunctionStatement(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not LocalFunctionStatementSyntax localFunction
            || localFunction.FirstAncestorOrSelf<MethodDeclarationSyntax>() is not MethodDeclarationSyntax methodDeclaration
            || !IsPotentialXunitTestMethodDeclaration(methodDeclaration)
            || context.SemanticModel.GetDeclaredSymbol(methodDeclaration, context.CancellationToken) is not IMethodSymbol methodSymbol
            || !IsXunitTestMethod(methodSymbol))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                XunitTestClassHelperMethodRule,
                localFunction.Identifier.GetLocation(),
                localFunction.Identifier.ValueText));
    }

    private static bool ContainsXunitTestMethod(INamedTypeSymbol typeSymbol)
    {
        return typeSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Any(static method => method.MethodKind == MethodKind.Ordinary && IsXunitTestMethod(method));
    }

    private static bool IsPotentialXunitHelperMethodDeclaration(MethodDeclarationSyntax methodDeclaration)
    {
        return methodDeclaration.Parent is TypeDeclarationSyntax;
    }

    private static bool HasValidXunitMethodName(string methodName, bool strictTestMethodCasing)
    {
        return strictTestMethodCasing
            ? HasStrictSentenceStyleXunitMethodName(methodName)
            : XunitMethodNamingRegex.IsMatch(methodName);
    }

    private static bool HasStrictSentenceStyleXunitMethodName(string methodName)
    {
        var segments = methodName.Split('_');

        return segments.Length >= 2
            && segments.All(static segment => segment.Length != 0)
            && char.IsUpper(segments[0][0])
            && segments[0].All(char.IsLetterOrDigit)
            && (TestMethodNameSegments.Canonical(segments[0]) is not { } firstCanonical || firstCanonical == segments[0])
            && segments.Skip(1).All(IsStrictSentenceStyleSegment);
    }

    private static bool IsStrictSentenceStyleSegment(string segment)
    {
        var canonical = TestMethodNameSegments.Canonical(segment);
        return segment.All(char.IsLetterOrDigit)
            && (canonical is not null
                ? canonical == segment
                : segment.All(static character => char.IsLower(character) || char.IsDigit(character)));
    }

    private static bool IsXunitLifecycleMethod(IMethodSymbol methodSymbol)
    {
        return methodSymbol.Parameters.Length == 0
            && (methodSymbol.ExplicitInterfaceImplementations.Any(static implementation =>
                (implementation.Name is nameof(IDisposable.Dispose) or "DisposeAsync" or "InitializeAsync")
                && implementation.ContainingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) is "global::System.IDisposable" or "global::System.IAsyncDisposable" or "global::Xunit.IAsyncLifetime")
                || (methodSymbol.DeclaredAccessibility == Accessibility.Public
                    && methodSymbol.Name switch
                    {
                        nameof(IDisposable.Dispose) => ImplementsInterface(methodSymbol.ContainingType, "global::System.IDisposable"),
                        "DisposeAsync" => ImplementsInterface(methodSymbol.ContainingType, "global::System.IAsyncDisposable"),
                        "InitializeAsync" => ImplementsInterface(methodSymbol.ContainingType, "global::Xunit.IAsyncLifetime"),
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
