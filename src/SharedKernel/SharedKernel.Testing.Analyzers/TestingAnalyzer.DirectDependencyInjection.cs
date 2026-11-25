using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace SharedKernel.Testing.Analyzers;

public sealed partial class TestingAnalyzer
{
    private const string DependencyInjectionNamespace = "Microsoft.Extensions.DependencyInjection.";

    private static readonly DiagnosticDescriptor DirectDependencyInjectionRule = new(
        TestingDiagnosticIds.DirectDependencyInjection,
        title: "Use a typed test fixture for dependency injection",
        messageFormat: "Use a typed fixture instead of direct dependency injection operation '{0}'",
        category: TestingCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: false,
        description: "Keep service collection, provider, scope, and service resolution operations in fixture setup. Enable this rule for suites that use typed fixtures, and scope exceptions to tests that exercise dependency injection itself.",
        helpLinkUri: "https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Testing.Analyzers/README.md#sktest023");

    private static void RegisterDirectDependencyInjectionAnalysis(CompilationStartAnalysisContext context)
    {
        var compilation = context.Compilation;
        if (compilation.GetTypeByMetadataName("Xunit.FactAttribute") is null
            && compilation.GetTypeByMetadataName("Xunit.TheoryAttribute") is null)
        {
            return;
        }

        context.RegisterOperationBlockStartAction(static blockContext =>
        {
            if (blockContext.OwningSymbol is IMethodSymbol { MethodKind: MethodKind.Ordinary } method && IsXunitTestMethod(method))
            {
                blockContext.RegisterOperationAction(AnalyzeDirectDependencyInjection, OperationKind.ObjectCreation, OperationKind.Invocation);
            }
        });
    }

    private static void AnalyzeDirectDependencyInjection(OperationAnalysisContext context)
    {
        if (context.Operation.IsImplicit)
        {
            return;
        }

        var operationName = context.Operation switch
        {
            IObjectCreationOperation creation when IsMetadataType(creation.Type, context.Compilation, DependencyInjectionNamespace + "ServiceCollection") => "ServiceCollection",
            IInvocationOperation invocation when IsDirectDependencyInjectionInvocation(invocation.TargetMethod, context.Compilation) => invocation.TargetMethod.Name,
            _ => null
        };
        if (operationName is not null)
        {
            context.ReportDiagnostic(Diagnostic.Create(DirectDependencyInjectionRule, context.Operation.Syntax.GetLocation(), operationName));
        }
    }

    private static bool IsDirectDependencyInjectionInvocation(IMethodSymbol target, Compilation compilation)
    {
        var method = target.ReducedFrom ?? target;
        var owner = method.ContainingType;
        return method.Name switch
        {
            "BuildServiceProvider" => IsMetadataType(owner, compilation, DependencyInjectionNamespace + "ServiceCollectionContainerBuilderExtensions"),
            "CreateScope" or "CreateAsyncScope" => IsMetadataType(owner, compilation, DependencyInjectionNamespace + "ServiceProviderServiceExtensions")
                || ImplementsInterfaceMember(method, compilation, DependencyInjectionNamespace + "IServiceScopeFactory"),
            "GetService" or "GetRequiredService" or "GetServices" => IsMetadataType(owner, compilation, DependencyInjectionNamespace + "ServiceProviderServiceExtensions")
                || ImplementsInterfaceMember(method, compilation, "System.IServiceProvider")
                || ImplementsInterfaceMember(method, compilation, DependencyInjectionNamespace + "ISupportRequiredService"),
            "GetKeyedService" or "GetRequiredKeyedService" or "GetKeyedServices" => IsMetadataType(owner, compilation, DependencyInjectionNamespace + "ServiceProviderKeyedServiceExtensions")
                || ImplementsInterfaceMember(method, compilation, DependencyInjectionNamespace + "IKeyedServiceProvider"),
            _ => false
        };
    }

    private static bool IsMetadataType(ITypeSymbol? type, Compilation compilation, string metadataName) =>
        compilation.GetTypeByMetadataName(metadataName) is { } known && SymbolEqualityComparer.Default.Equals(type, known);

    private static bool ImplementsInterfaceMember(IMethodSymbol method, Compilation compilation, string metadataName)
    {
        if (compilation.GetTypeByMetadataName(metadataName) is not { } contract)
        {
            return false;
        }

        var owner = method.ContainingType;
        return SymbolEqualityComparer.Default.Equals(owner, contract)
            || contract.GetMembers(method.Name).OfType<IMethodSymbol>().Any(member => SymbolEqualityComparer.Default.Equals(
                owner.FindImplementationForInterfaceMember(member)?.OriginalDefinition, method.OriginalDefinition));
    }
}
