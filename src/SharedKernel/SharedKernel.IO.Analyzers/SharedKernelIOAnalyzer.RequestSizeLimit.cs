using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace PandaBear.IO.Analyzers;

public sealed partial class SharedKernelIOAnalyzer
{
    private static readonly DiagnosticDescriptor MissingRequestSizeLimitRule = new(
        IODiagnosticIds.MissingRequestSizeLimit,
        title: "Upload and raw-body endpoints should set a request size limit",
        messageFormat: "Endpoint '{0}' reads an upload or the raw request body without an endpoint-level request size limit; add RequestSizeLimit, RequestFormLimits, or IHttpMaxRequestBodySizeFeature",
        category: "IO",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: false,
        description: "Kestrel's default request body limit is 30,000,000 bytes and is rarely the right budget for an upload endpoint. Enable this rule where the host does not already enforce a per-endpoint size policy. A limit set outside the endpoint (Kestrel options, endpoint groups, filters) is not visible; suppress the diagnostic where one applies.");

    /// <summary>Reports a public controller action that takes an upload or reads Request.Body with no size-limit attribute or feature.</summary>
    private static void AnalyzeControllerAction(OperationBlockAnalysisContext context, KnownTypes types)
    {
        if (context.OwningSymbol is not IMethodSymbol { MethodKind: MethodKind.Ordinary, DeclaredAccessibility: Accessibility.Public, IsStatic: false } method
            || !Inherits(method.ContainingType, types.ControllerBase)
            || method.GetAttributes().Any(static attribute => attribute.AttributeClass is { Name: "NonActionAttribute" })
            || method.Locations.FirstOrDefault(static location => location.IsInSource) is not { } location)
        {
            return;
        }

        var operations = context.OperationBlocks.SelectMany(static block => block.DescendantsAndSelf()).ToList();
        if (!HasIntake(types, method, operations)
            || HasLimitAttribute(types, method)
            || HasLimitAttribute(types, method.ContainingType)
            || MentionsLimitFeature(types, operations))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(MissingRequestSizeLimitRule, location, method.Name));
    }

    /// <summary>Reports a minimal-API MapPost, MapPut, MapPatch, MapMethods, or Map call whose handler takes an upload or reads Request.Body with no size limit on the handler or the call chain.</summary>
    private static void AnalyzeMappedEndpoint(OperationAnalysisContext context, KnownTypes types)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (invocation.TargetMethod.Name is not ("MapPost" or "MapPut" or "MapPatch" or "MapMethods" or "Map")
            || invocation.Arguments.FirstOrDefault(static argument => argument.Parameter is { Name: "handler", Type.Name: "Delegate" }) is not { } handlerArgument)
        {
            return;
        }

        IMethodSymbol handler;
        var operations = new List<IOperation>();
        switch (UnwrapHandler(handlerArgument.Value))
        {
            case IAnonymousFunctionOperation lambda:
                handler = lambda.Symbol;
                operations.AddRange(lambda.Body.DescendantsAndSelf());
                break;
            case IMethodReferenceOperation reference:
                handler = reference.Method;
                break;
            default:
                return;
        }

        if (!HasIntake(types, handler, operations)
            || HasLimitAttribute(types, handler)
            || MentionsLimitFeature(types, operations)
            || ChainSetsLimit(types, invocation))
        {
            return;
        }

        var location = invocation.Syntax is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax member }
            ? member.Name.GetLocation()
            : invocation.Syntax.GetLocation();
        context.ReportDiagnostic(Diagnostic.Create(MissingRequestSizeLimitRule, location, handler.Name.Length == 0 || handler.MethodKind == MethodKind.AnonymousFunction ? invocation.TargetMethod.Name : handler.Name));
    }

    private static IOperation UnwrapHandler(IOperation operation)
    {
        operation = Unwrap(operation);
        return operation is IDelegateCreationOperation creation ? Unwrap(creation.Target) : operation;
    }

    private static bool HasIntake(KnownTypes types, IMethodSymbol handler, IEnumerable<IOperation> operations)
    {
        return handler.Parameters.Any(parameter => IsIntakeType(types, parameter.Type))
            || operations.Any(static operation => operation is IPropertyReferenceOperation { Property: { Name: "Body", ContainingType.Name: "HttpRequest" } });
    }

    private static bool IsIntakeType(KnownTypes types, ITypeSymbol type)
    {
        return Inherits(type, types.FormFile) || Inherits(type, types.FormFileCollection) || Inherits(type, types.PipeReader) || Inherits(type, types.Stream);
    }

    private static bool HasLimitAttribute(KnownTypes types, ISymbol symbol)
    {
        return symbol.GetAttributes().Any(attribute => types.LimitAttributes.Any(limit => Inherits(attribute.AttributeClass, limit))
            && (attribute.AttributeClass is not { Name: "RequestFormLimitsAttribute" } || attribute.NamedArguments.Any(static named => named.Key == "MultipartBodyLengthLimit")));
    }

    private static bool MentionsLimitFeature(KnownTypes types, IEnumerable<IOperation> operations)
    {
        var feature = types.MaxRequestBodySizeFeature;
        return feature is not null
            && operations.Any(operation => SymbolEqualityComparer.Default.Equals(operation.Type, feature)
                || operation is IInvocationOperation invocation && invocation.TargetMethod.TypeArguments.Any(argument => SymbolEqualityComparer.Default.Equals(argument, feature))
                || operation is ITypeOfOperation typeOf && SymbolEqualityComparer.Default.Equals(typeOf.TypeOperand, feature));
    }

    /// <summary>A later call in the fluent chain such as .WithMetadata(new RequestSizeLimitAttribute(n)) sets the limit for the mapped endpoint.</summary>
    private static bool ChainSetsLimit(KnownTypes types, IInvocationOperation mapCall)
    {
        IOperation current = mapCall;
        while (current.Parent is IArgumentOperation { Parent: IInvocationOperation next } argument
            && ReferenceEquals(next.Arguments.FirstOrDefault(), argument))
        {
            if (next.Arguments.Skip(1).SelectMany(static other => other.DescendantsAndSelf()).OfType<IObjectCreationOperation>().Any(creation =>
                    types.LimitAttributes.Any(limit => Inherits(creation.Type, limit)) || Inherits(creation.Type, types.RequestSizeLimitMetadata)))
            {
                return true;
            }

            current = next;
        }

        return false;
    }
}
