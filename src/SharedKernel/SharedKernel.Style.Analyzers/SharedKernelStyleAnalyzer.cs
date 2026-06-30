using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SharedKernel.Style.Analyzers;

/// <summary>
/// Reports diagnostics for repository-specific style rules.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SharedKernelStyleAnalyzer : DiagnosticAnalyzer
{
    private const string AsyncSuffix = "Async";
    private const string CancellationTokenParameterName = "ct";
    private const string OperationCanceledExceptionTypeName = "OperationCanceledException";
    private const string ShouldHandleAsFailureMethodName = "ShouldHandleAsFailure";
    private const string LoggerExtensionsTypeName = "Microsoft.Extensions.Logging.LoggerExtensions";
    private const string LoggerInterfaceTypeName = "Microsoft.Extensions.Logging.ILogger";
    private static readonly ImmutableHashSet<string> DirectLoggerExtensionMethodNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        "LogTrace",
        "LogDebug",
        "LogInformation",
        "LogWarning",
        "LogError",
        "LogCritical");
    private static readonly DiagnosticDescriptor AsyncSuffixRule = new(
        StyleDiagnosticIds.AsyncSuffix,
        title: "Method name should not end with Async",
        messageFormat: "Method '{0}' should be named '{1}' to follow repository async naming conventions",
        category: "Style",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Repository coding rules prohibit the Async suffix on method names unless an inherited contract requires that name.");
    private static readonly DiagnosticDescriptor CancellationTokenParameterNameRule = new(
        StyleDiagnosticIds.CancellationTokenParameterName,
        title: "CancellationToken parameters should be named ct",
        messageFormat: "CancellationToken parameter '{0}' should be named 'ct'",
        category: "Style",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Repository coding rules standardize CancellationToken parameter names on the short form 'ct'.");
    private static readonly DiagnosticDescriptor CancellationTokenDefaultValueRule = new(
        StyleDiagnosticIds.CancellationTokenDefaultValue,
        title: "CancellationToken parameters should not declare default values",
        messageFormat: "CancellationToken parameter '{0}' should not declare a default value",
        category: "Style",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Repository coding rules require callers to pass cancellation tokens explicitly instead of relying on optional default token parameters.");
    private static readonly DiagnosticDescriptor MultipleTopLevelTypesPerFileRule = new(
        StyleDiagnosticIds.MultipleTopLevelTypesPerFile,
        title: "Source files should not declare more than one top-level type",
        messageFormat: "Source file '{0}' declares multiple top-level types; move '{1}' into its own file",
        category: "Style",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Repository coding rules prefer one top-level type per file for new or significantly refactored C# code.");
    private static readonly DiagnosticDescriptor BroadOperationCanceledExceptionFilterRule = new(
        StyleDiagnosticIds.BroadOperationCanceledExceptionFilter,
        title: "Catch filters should preserve unexpected OperationCanceledException telemetry",
        messageFormat: "Catch filter for '{0}' should use ShouldHandleAsFailure(ct) so only cooperative cancellation is excluded",
        category: "Style",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Catch filters must not exclude every OperationCanceledException from error handling; only cancellation tied to the operation token is cooperative.");
    private static readonly DiagnosticDescriptor NonSourceGeneratedLoggingRule = new(
        StyleDiagnosticIds.NonSourceGeneratedLogging,
        title: "Production logging should use source-generated LoggerMessage methods",
        messageFormat: "Logger call '{0}' should be replaced with a source-generated LoggerMessage method or documented exception",
        category: "Style",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Repository observability rules require stable source-generated logging contracts for production logging.");
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(
            AsyncSuffixRule,
            CancellationTokenParameterNameRule,
            CancellationTokenDefaultValueRule,
            MultipleTopLevelTypesPerFileRule,
            BroadOperationCanceledExceptionFilterRule,
            NonSourceGeneratedLoggingRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(InitializeCompilation);
    }

    private static void InitializeCompilation(CompilationStartAnalysisContext context)
    {
        var cancellationTokenType = context.Compilation.GetTypeByMetadataName("System.Threading.CancellationToken");

        context.RegisterSymbolAction(
            symbolContext =>
            {
                var sourceLocation = symbolContext.Symbol.Locations.FirstOrDefault(static location => location.IsInSource);
                AnalyzeMethod(
                    symbolContext,
                    StyleAnalyzerConfigOptions.Parse(
                        symbolContext.Options.AnalyzerConfigOptionsProvider,
                        sourceLocation?.SourceTree));
            },
            SymbolKind.Method);

        context.RegisterSyntaxTreeAction(AnalyzeSyntaxTree);

        context.RegisterSyntaxNodeAction(
            AnalyzeCatchFilter,
            SyntaxKind.CatchFilterClause);

        context.RegisterSyntaxNodeAction(
            AnalyzeLoggerInvocation,
            SyntaxKind.InvocationExpression);

        if (cancellationTokenType is null)
        {
            return;
        }

        context.RegisterSyntaxNodeAction(
            syntaxContext => AnalyzeParameter(syntaxContext, cancellationTokenType),
            SyntaxKind.Parameter);
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context, StyleAnalyzerConfigOptions options)
    {
        if (context.Symbol is not IMethodSymbol method
            || method.MethodKind != MethodKind.Ordinary
            || method.AssociatedSymbol is not null
            || method.Name.Length <= AsyncSuffix.Length
            || !method.Name.EndsWith(AsyncSuffix, StringComparison.Ordinal))
        {
            return;
        }

        if (options.AllowAsyncSuffixOverrides && (method.IsOverride || OverridesAsyncContract(method)))
        {
            return;
        }

        if (options.AllowAsyncSuffixInterfaceImplementations && ImplementsInterfaceContract(method))
        {
            return;
        }

        var suggestedName = method.Name.Substring(0, method.Name.Length - AsyncSuffix.Length);
        if (string.IsNullOrWhiteSpace(suggestedName))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                AsyncSuffixRule,
                method.Locations.FirstOrDefault(),
                method.Name,
                suggestedName));
    }

    private static bool ImplementsInterfaceContract(IMethodSymbol method)
    {
        if (method.ExplicitInterfaceImplementations.Length > 0)
        {
            return true;
        }

        return method.ContainingType.AllInterfaces
            .SelectMany(@interface => @interface.GetMembers(method.Name).OfType<IMethodSymbol>())
            .Any(interfaceMethod => SymbolEqualityComparer.Default.Equals(
                method.ContainingType.FindImplementationForInterfaceMember(interfaceMethod),
                method));
    }

    private static bool OverridesAsyncContract(IMethodSymbol method)
    {
        var overriddenMethod = method.OverriddenMethod;
        while (overriddenMethod is not null)
        {
            if (overriddenMethod.Name.EndsWith(AsyncSuffix, StringComparison.Ordinal))
            {
                return true;
            }

            overriddenMethod = overriddenMethod.OverriddenMethod;
        }

        return false;
    }

    private static void AnalyzeParameter(SyntaxNodeAnalysisContext context, INamedTypeSymbol cancellationTokenType)
    {
        if (context.Node is not ParameterSyntax parameterSyntax
            || context.SemanticModel.GetDeclaredSymbol(parameterSyntax, context.CancellationToken) is not IParameterSymbol parameter
            || !parameter.Locations.Any(static location => location.IsInSource)
            || !SymbolEqualityComparer.Default.Equals(parameter.Type, cancellationTokenType))
        {
            return;
        }

        if (!string.Equals(parameter.Name, CancellationTokenParameterName, StringComparison.Ordinal))
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    CancellationTokenParameterNameRule,
                    parameter.Locations.First(),
                    parameter.Name));
        }

        if (parameter.HasExplicitDefaultValue)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    CancellationTokenDefaultValueRule,
                    parameter.Locations.First(),
                    parameter.Name));
        }
    }

    private static void AnalyzeSyntaxTree(SyntaxTreeAnalysisContext context)
    {
        if (context.Tree.GetRoot(context.CancellationToken) is not CompilationUnitSyntax root)
        {
            return;
        }

        var topLevelTypes = GetTopLevelTypes(root).ToArray();
        if (topLevelTypes.Length <= 1 || topLevelTypes.All(IsPartialType))
        {
            return;
        }

        var offendingType = topLevelTypes.Skip(1).FirstOrDefault(static type => !IsPartialType(type)) ?? topLevelTypes[1];
        var typeName = GetTypeName(offendingType);
        context.ReportDiagnostic(
            Diagnostic.Create(
                MultipleTopLevelTypesPerFileRule,
                offendingType.GetLocation(),
                Path.GetFileName(context.Tree.FilePath),
                typeName));
    }

    private static void AnalyzeCatchFilter(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not CatchFilterClauseSyntax filter
            || filter.Parent is not CatchClauseSyntax { Declaration.Identifier.ValueText: { Length: > 0 } exceptionName })
        {
            return;
        }

        if (ContainsShouldHandleAsFailure(filter.FilterExpression)
            || ContainsCooperativeCancellationGuard(filter.FilterExpression)
            || !ContainsBroadOperationCanceledExceptionExclusion(filter.FilterExpression))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            BroadOperationCanceledExceptionFilterRule,
            filter.FilterExpression.GetLocation(),
            exceptionName));
    }

    private static bool ContainsShouldHandleAsFailure(SyntaxNode node)
    {
        return node.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any(static invocation =>
            string.Equals(GetInvocationName(invocation), ShouldHandleAsFailureMethodName, StringComparison.Ordinal));
    }

    private static bool ContainsCooperativeCancellationGuard(SyntaxNode node)
    {
        return node.DescendantNodesAndSelf().OfType<PrefixUnaryExpressionSyntax>().Any(static prefixUnary =>
            prefixUnary.IsKind(SyntaxKind.LogicalNotExpression)
            && prefixUnary.Operand is MemberAccessExpressionSyntax memberAccess
            && memberAccess.Expression is IdentifierNameSyntax { Identifier.ValueText: "ct" }
            && string.Equals(memberAccess.Name.Identifier.ValueText, nameof(CancellationToken.IsCancellationRequested), StringComparison.Ordinal));
    }

    private static bool ContainsBroadOperationCanceledExceptionExclusion(SyntaxNode node)
    {
        var expressionText = node.ToString();
        return expressionText.Contains(OperationCanceledExceptionTypeName, StringComparison.Ordinal)
            && (expressionText.Contains("is not", StringComparison.Ordinal)
                || expressionText.Contains("!", StringComparison.Ordinal));
    }

    private static void AnalyzeLoggerInvocation(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation
            || GetInvocationName(invocation) is not { Length: > 0 } invocationName
            || !IsDirectLoggerCall(context, invocation, invocationName))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            NonSourceGeneratedLoggingRule,
            invocation.GetLocation(),
            invocationName));
    }

    private static bool IsDirectLoggerCall(
        SyntaxNodeAnalysisContext context,
        InvocationExpressionSyntax invocation,
        string invocationName)
    {
        return DirectLoggerExtensionMethodNames.Contains(invocationName)
            ? IsLoggerExtensionsCall(context, invocation)
            : string.Equals(invocationName, "Log", StringComparison.Ordinal)
                && IsLoggerInterfaceCall(context, invocation);
    }

    private static bool IsLoggerExtensionsCall(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        return context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol method
            && string.Equals(method.ContainingType.ToDisplayString(), LoggerExtensionsTypeName, StringComparison.Ordinal);
    }

    private static bool IsLoggerInterfaceCall(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation)
    {
        return context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol is IMethodSymbol method
            && string.Equals(method.ContainingType.ToDisplayString(), LoggerInterfaceTypeName, StringComparison.Ordinal);
    }

    private static IEnumerable<MemberDeclarationSyntax> GetTopLevelTypes(CompilationUnitSyntax compilationUnit)
    {
        foreach (var member in GetTopLevelTypes(compilationUnit.Members))
        {
            yield return member;
        }
    }

    private static IEnumerable<MemberDeclarationSyntax> GetTopLevelTypes(BaseNamespaceDeclarationSyntax @namespace)
    {
        foreach (var member in GetTopLevelTypes(@namespace.Members))
        {
            yield return member;
        }
    }

    private static IEnumerable<MemberDeclarationSyntax> GetTopLevelTypes(SyntaxList<MemberDeclarationSyntax> members)
    {
        foreach (var member in members)
        {
            if (member is BaseNamespaceDeclarationSyntax @namespace)
            {
                foreach (var namespacedMember in GetTopLevelTypes(@namespace))
                {
                    yield return namespacedMember;
                }

                continue;
            }

            if (IsTopLevelTypeDeclaration(member))
            {
                yield return member;
            }
        }
    }

    private static bool IsTopLevelTypeDeclaration(MemberDeclarationSyntax member)
    {
        return member is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax;
    }

    private static bool IsPartialType(MemberDeclarationSyntax member)
    {
        return member is TypeDeclarationSyntax typeDeclaration
            && typeDeclaration.Modifiers.Any(static modifier => modifier.IsKind(SyntaxKind.PartialKeyword));
    }

    private static string GetTypeName(MemberDeclarationSyntax member)
    {
        return member switch
        {
            BaseTypeDeclarationSyntax baseType => baseType.Identifier.ValueText,
            DelegateDeclarationSyntax @delegate => @delegate.Identifier.ValueText,
            _ => "type"
        };
    }

    private static string? GetInvocationName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            MemberBindingExpressionSyntax memberBinding => memberBinding.Name.Identifier.ValueText,
            IdentifierNameSyntax identifierName => identifierName.Identifier.ValueText,
            _ => null
        };
    }
}
