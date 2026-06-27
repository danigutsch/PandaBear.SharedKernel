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

    private static readonly string[] AllowedStrictNameSegments = [
        "API",
        "CSV",
        "DTO",
        "DataAnnotationsValidator",
        "DateOnly",
        "DateTime",
        "EF",
        "EditContext",
        "GUID",
        "Guid",
        "HTTP",
        "HttpClient",
        "HttpContext",
        "HttpRequest",
        "HttpResponse",
        "HttpStatusCode",
        "ID",
        "IDs",
        "JSON",
        "Json",
        "OpenApi",
        "ProblemDetails",
        "QuickGrid",
        "SKTEST",
        "Task",
        "TimeOnly",
        "TimeSpan",
        "URI",
        "URL",
        "UTC",
        "ValidationMessage",
        "ValueTask",
        "xUnit",
    ];

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

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [TestMethodWarningSuppressionRule, XunitTestMethodNamingRule, XunitTestMethodRequiredTraitRule, XunitTestClassHelperMethodRule, XunitSerialCollectionJustificationRule];

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

        if (typeSymbol.ContainingType is null || !ContainsXunitTestMethod(typeSymbol.ContainingType))
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
        if (segments.Length < 2
            || segments.Any(static segment => segment.Length == 0)
            || !char.IsUpper(segments[0][0])
            || !segments[0].All(char.IsLetterOrDigit))
        {
            return false;
        }

        return segments.Skip(1).All(IsStrictSentenceStyleSegment);
    }

    private static bool IsStrictSentenceStyleSegment(string segment)
    {
        return segment.All(char.IsLetterOrDigit)
            && (segment.All(static character => char.IsLower(character) || char.IsDigit(character))
                || IsAllowedStrictNameSegment(segment));
    }

    private static bool IsAllowedStrictNameSegment(string segment)
    {
        return Array.Exists(AllowedStrictNameSegments, allowedSegment => string.Equals(segment, allowedSegment, StringComparison.Ordinal))
            || (segment.StartsWith("SKTEST", StringComparison.Ordinal)
                && segment.Length > "SKTEST".Length
                && segment.Substring("SKTEST".Length).All(char.IsDigit));
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
