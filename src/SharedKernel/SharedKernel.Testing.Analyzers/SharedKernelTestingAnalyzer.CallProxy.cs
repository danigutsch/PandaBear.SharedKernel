using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace PandaBear.Testing.Analyzers;

public sealed partial class SharedKernelTestingAnalyzer
{
    private const string CallProxyMetadataName = "PandaBear.Testing.CallProxy";

    private static readonly DiagnosticDescriptor CallProxyServiceTypeRule = new(
        TestingDiagnosticIds.CallProxyServiceType,
        "CallProxy can only wrap a public interface",
        "CallProxy can only wrap a public interface; '{0}' is {1}",
        TestingCategory, DiagnosticSeverity.Warning, true,
        description: "CallProxy.Create generates its proxy type at run time, which fails with an ArgumentException unless the service is a public interface.",
        helpLinkUri: "https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Testing.Analyzers/README.md#sktest019");

    private static void RegisterCallProxyServiceTypeAnalysis(CompilationStartAnalysisContext compilationContext)
    {
        var callProxy = compilationContext.Compilation.GetTypeByMetadataName(CallProxyMetadataName);
        if (callProxy is null)
        {
            return;
        }

        compilationContext.RegisterOperationAction(
            operationContext => AnalyzeCallProxyCreate(operationContext, callProxy),
            OperationKind.Invocation);
    }

    private static void AnalyzeCallProxyCreate(OperationAnalysisContext context, INamedTypeSymbol callProxy)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod;
        if (method.Name != "Create"
            || method.TypeArguments.Length != 1
            || !SymbolEqualityComparer.Default.Equals(method.ContainingType, callProxy)
            || method.TypeArguments[0] is not INamedTypeSymbol service)
        {
            return;
        }

        var reason = service.TypeKind != TypeKind.Interface
            ? "not an interface"
            : IsPubliclyAccessible(service) ? null : "not public";
        if (reason is null)
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                CallProxyServiceTypeRule,
                GetServiceTypeLocation(invocation),
                service.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                reason));
    }

    private static bool IsPubliclyAccessible(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility != Accessibility.Public)
            {
                return false;
            }
        }

        return true;
    }

    private static Location GetServiceTypeLocation(IInvocationOperation invocation)
    {
        if (invocation.Syntax is InvocationExpressionSyntax { Expression: var expression })
        {
            var name = expression switch
            {
                MemberAccessExpressionSyntax memberAccess => memberAccess.Name,
                SimpleNameSyntax simpleName => simpleName,
                _ => null,
            };

            if (name is GenericNameSyntax { TypeArgumentList.Arguments: { Count: 1 } arguments })
            {
                return arguments[0].GetLocation();
            }
        }

        return invocation.Syntax.GetLocation();
    }
}
