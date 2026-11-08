using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PandaBear.Results.Analyzers;

/// <summary>
/// Reports misuse of SharedKernel Result values.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SharedKernelResultsAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor RawResultEndpointRule = new(
        ResultsDiagnosticIds.RawResultEndpoint,
        title: "Convert a Result before returning it from an endpoint",
        messageFormat: "Endpoint returns '{0}' directly; the client would receive the raw status and error details with HTTP 200, so return result.ToHttpResult() instead",
        category: "Security",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "The framework serializes a returned Result struct as JSON: error details leak and the status is always 200. Convert it with ToHttpResult from PandaBear.Results.AspNet.");

    private static readonly DiagnosticDescriptor DiscardedResultRule = new(
        ResultsDiagnosticIds.DiscardedResult,
        title: "Do not ignore a returned Result",
        messageFormat: "The Result returned by '{0}' is ignored; check it, return it, or discard it explicitly with '_ ='",
        category: "Reliability",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A failure Result that nobody inspects looks like success, for example a failed save. Inspect the Result, pass it on, or discard it explicitly with '_ ='.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
    {
        get;
    } =
        ImmutableArray.Create(RawResultEndpointRule, DiscardedResultRule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        if (context is null)
        {
            throw new System.ArgumentNullException(nameof(context));
        }

        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static compilationContext =>
        {
            var types = ResultTypes.Create(compilationContext.Compilation);
            if (types is null)
            {
                return;
            }

            var endpointExtensions = compilationContext.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Builder.EndpointRouteBuilderExtensions");
            if (endpointExtensions is not null)
            {
                compilationContext.RegisterOperationAction(
                    operationContext => AnalyzeInvocation(operationContext, types, endpointExtensions),
                    OperationKind.Invocation);
            }

            compilationContext.RegisterOperationAction(
                operationContext => AnalyzeExpressionStatement(operationContext, types),
                OperationKind.ExpressionStatement);
        });
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context, ResultTypes types, INamedTypeSymbol endpointExtensions)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        if (!SymbolEqualityComparer.Default.Equals(method.ContainingType, endpointExtensions) || !MapMethodNames.Contains(method.Name))
        {
            return;
        }

        foreach (var argument in invocation.Arguments)
        {
            var value = argument.Value;
            while (value is IConversionOperation conversion)
            {
                value = conversion.Operand;
            }

            if (value is IDelegateCreationOperation creation)
            {
                value = creation.Target;
            }

            switch (value)
            {
                case IAnonymousFunctionOperation lambda when types.IsResultOrWrapped(lambda.Symbol.ReturnType):
                    Report(context, GetLambdaLocation(lambda.Syntax), lambda.Symbol.ReturnType);
                    break;
                case IMethodReferenceOperation reference when types.IsResultOrWrapped(reference.Method.ReturnType):
                    Report(context, reference.Syntax.GetLocation(), reference.Method.ReturnType);
                    break;
            }
        }

        static void Report(OperationAnalysisContext context, Location location, ITypeSymbol returnType) =>
            context.ReportDiagnostic(Diagnostic.Create(RawResultEndpointRule, location, returnType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
    }

    private static Location GetLambdaLocation(SyntaxNode syntax) => syntax switch
    {
        SimpleLambdaExpressionSyntax simple => simple.ArrowToken.GetLocation(),
        ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.ArrowToken.GetLocation(),
        _ => syntax.GetLocation(),
    };

    private static void AnalyzeExpressionStatement(OperationAnalysisContext context, ResultTypes types)
    {
        var statement = (IExpressionStatementOperation)context.Operation;
        var expression = statement.Operation;
        ITypeSymbol? resultType;
        if (expression is IAwaitOperation await)
        {
            resultType = await.Type;
            expression = await.Operation;
        }
        else
        {
            resultType = expression.Type;
        }

        if (!types.IsResultOrWrapped(resultType))
        {
            return;
        }

        var invocation = FindInvocation(expression);
        if (invocation is null)
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(DiscardedResultRule, invocation.Syntax.GetLocation(), invocation.TargetMethod.Name));
    }

    private static IInvocationOperation? FindInvocation(IOperation operation)
    {
        // Skip ConfigureAwait(...) so the diagnostic names the method that produced the result.
        while (operation is IInvocationOperation { TargetMethod.Name: "ConfigureAwait", Instance: { } instance })
        {
            operation = instance;
        }

        return operation as IInvocationOperation;
    }

    private static readonly ImmutableHashSet<string> MapMethodNames = ImmutableHashSet.Create(
        "MapGet", "MapPost", "MapPut", "MapDelete", "MapPatch", "Map", "MapMethods");

    private sealed class ResultTypes
    {
        private readonly INamedTypeSymbol result;
        private readonly INamedTypeSymbol resultOfT;
        private readonly INamedTypeSymbol? task;
        private readonly INamedTypeSymbol? valueTask;

        private ResultTypes(INamedTypeSymbol result, INamedTypeSymbol resultOfT, INamedTypeSymbol? task, INamedTypeSymbol? valueTask)
        {
            this.result = result;
            this.resultOfT = resultOfT;
            this.task = task;
            this.valueTask = valueTask;
        }

        public static ResultTypes? Create(Compilation compilation)
        {
            var result = compilation.GetTypeByMetadataName("PandaBear.Results.Result");
            var resultOfT = compilation.GetTypeByMetadataName("PandaBear.Results.Result`1");
            return result is null || resultOfT is null
                ? null
                : new ResultTypes(
                    result,
                    resultOfT,
                    compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1"),
                    compilation.GetTypeByMetadataName("System.Threading.Tasks.ValueTask`1"));
        }

        /// <summary>True for Result, Result&lt;T&gt;, or a Task or ValueTask of either.</summary>
        public bool IsResultOrWrapped(ITypeSymbol? type)
        {
            if (type is not INamedTypeSymbol named)
            {
                return false;
            }

            if (IsResult(named))
            {
                return true;
            }

            var definition = named.OriginalDefinition;
            return (SymbolEqualityComparer.Default.Equals(definition, task) || SymbolEqualityComparer.Default.Equals(definition, valueTask))
                && named.TypeArguments[0] is INamedTypeSymbol inner
                && IsResult(inner);
        }

        private bool IsResult(INamedTypeSymbol type) =>
            SymbolEqualityComparer.Default.Equals(type, result)
            || SymbolEqualityComparer.Default.Equals(type.OriginalDefinition, resultOfT);
    }
}
