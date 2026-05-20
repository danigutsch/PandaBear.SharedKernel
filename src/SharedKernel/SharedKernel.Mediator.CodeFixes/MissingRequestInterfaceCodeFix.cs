using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using SharedKernel.Mediator.SourceGenerator;

namespace SharedKernel.Mediator.CodeFixes;

/// <summary>
/// Adds a missing mediator request interface to a request type used in a send call.
/// </summary>
internal static class MissingRequestInterfaceCodeFix
{
    private const string InvalidRequestArgumentDiagnosticId = "CS1503";
    /// <summary>
    /// Registers the missing-request-interface code fix when a send call uses a request type without the required mediator interface.
    /// </summary>
    /// <param name="context">The code-fix registration context.</param>
    /// <param name="diagnostic">The compiler diagnostic to fix.</param>
    public static async Task RegisterAsync(CodeFixContext context, Diagnostic diagnostic)
    {
        if (!string.Equals(diagnostic.Id, InvalidRequestArgumentDiagnosticId, StringComparison.Ordinal))
        {
            return;
        }

        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null || semanticModel is null)
        {
            return;
        }

        if (!TryCreatePlan(root, semanticModel, diagnostic, context.CancellationToken, out var requestDeclaration, out var plan))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Add {plan.InterfaceDisplayName}",
                createChangedSolution: cancellationToken => AddRequestInterfaceAsync(context.Document.Project.Solution, requestDeclaration, plan, cancellationToken),
                equivalenceKey: $"AddMediatorRequestInterface:{plan.InterfaceDisplayName}"),
            diagnostic);
    }

    private static async Task<Solution> AddRequestInterfaceAsync(
        Solution solution,
        TypeDeclarationSyntax requestDeclaration,
        InterfacePlan plan,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var document = solution.GetDocument(requestDeclaration.SyntaxTree);
        if (document is null)
        {
            return solution;
        }

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return solution;
        }

        var currentDeclaration = root.FindNode(requestDeclaration.Span, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<TypeDeclarationSyntax>() ?? requestDeclaration;
        var baseTypeSyntax = Microsoft.CodeAnalysis.CSharp.SyntaxFactory.SimpleBaseType(
            Microsoft.CodeAnalysis.CSharp.SyntaxFactory.ParseTypeName(plan.InterfaceTypeName));
        var updatedDeclaration = currentDeclaration.BaseList is null
            ? currentDeclaration.WithBaseList(
                Microsoft.CodeAnalysis.CSharp.SyntaxFactory.BaseList(
                    Microsoft.CodeAnalysis.CSharp.SyntaxFactory.SeparatedList<BaseTypeSyntax>([baseTypeSyntax])))
            : currentDeclaration.WithBaseList(currentDeclaration.BaseList.AddTypes(baseTypeSyntax));
        updatedDeclaration = updatedDeclaration.WithAdditionalAnnotations(Formatter.Annotation);

        var updatedRoot = root.ReplaceNode(currentDeclaration, updatedDeclaration);
        return solution.WithDocumentSyntaxRoot(document.Id, updatedRoot);
    }

    private static bool TryCreatePlan(
        SyntaxNode root,
        SemanticModel semanticModel,
        Diagnostic diagnostic,
        CancellationToken cancellationToken,
        out TypeDeclarationSyntax requestDeclaration,
        out InterfacePlan plan)
    {
        var node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        var argumentExpression = node.FirstAncestorOrSelf<ArgumentSyntax>()?.Expression ?? node as ExpressionSyntax;
        if (argumentExpression is null)
        {
            requestDeclaration = null!;
            plan = default;
            return false;
        }

        if (semanticModel.GetTypeInfo(argumentExpression, cancellationToken).Type is not INamedTypeSymbol requestType
            || requestType.DeclaringSyntaxReferences.Length == 0)
        {
            requestDeclaration = null!;
            plan = default;
            return false;
        }

        var compilation = semanticModel.Compilation;
        if (requestType.AllInterfaces.Any(candidate =>
                SymbolMatches(compilation, candidate, MetadataNames.Request)
                || SymbolMatches(compilation, candidate, MetadataNames.Command)
                || SymbolMatches(compilation, candidate, MetadataNames.CommandOfResponse)
                || SymbolMatches(compilation, candidate, MetadataNames.Query)))
        {
            requestDeclaration = null!;
            plan = default;
            return false;
        }

        var invocation = argumentExpression.FirstAncestorOrSelf<InvocationExpressionSyntax>();
        var targetMethod = invocation is null ? null : GetCandidateTargetMethod(semanticModel, invocation, cancellationToken);
        if (targetMethod is null || !IsSenderSendMethod(compilation, targetMethod))
        {
            requestDeclaration = null!;
            plan = default;
            return false;
        }

        requestDeclaration = requestType.DeclaringSyntaxReferences[0].GetSyntax(cancellationToken) as TypeDeclarationSyntax ?? null!;
        if (requestDeclaration is null)
        {
            plan = default;
            return false;
        }

        plan = CreateInterfacePlan(compilation, requestType, targetMethod);
        return true;
    }

    private static InterfacePlan CreateInterfacePlan(Compilation compilation, INamedTypeSymbol requestType, IMethodSymbol targetMethod)
    {
        foreach (var candidateType in EnumerateTypes(compilation.Assembly.GlobalNamespace))
        {
            foreach (var candidateInterface in candidateType.AllInterfaces)
            {
                if (SymbolMatches(compilation, candidateInterface, MetadataNames.QueryHandler)
                    && SymbolEqualityComparer.Default.Equals(candidateInterface.TypeArguments[0], requestType))
                {
                    return new InterfacePlan(
                        $"global::SharedKernel.Mediator.IQuery<{candidateInterface.TypeArguments[1].ToDisplayString(SymbolDisplayFormats.FullyQualifiedWithNullability)}>",
                        $"IQuery<{candidateInterface.TypeArguments[1].ToDisplayString(SymbolDisplayFormats.MinimallyQualifiedWithNullability)}>");
                }

                if (SymbolMatches(compilation, candidateInterface, MetadataNames.CommandHandlerOfResponse)
                    && SymbolEqualityComparer.Default.Equals(candidateInterface.TypeArguments[0], requestType))
                {
                    return new InterfacePlan(
                        $"global::SharedKernel.Mediator.ICommand<{candidateInterface.TypeArguments[1].ToDisplayString(SymbolDisplayFormats.FullyQualifiedWithNullability)}>",
                        $"ICommand<{candidateInterface.TypeArguments[1].ToDisplayString(SymbolDisplayFormats.MinimallyQualifiedWithNullability)}>");
                }

                if (SymbolMatches(compilation, candidateInterface, MetadataNames.CommandHandler)
                    && SymbolEqualityComparer.Default.Equals(candidateInterface.TypeArguments[0], requestType))
                {
                    return new InterfacePlan("global::SharedKernel.Mediator.ICommand", "ICommand");
                }

                if (SymbolMatches(compilation, candidateInterface, MetadataNames.RequestHandler)
                    && SymbolEqualityComparer.Default.Equals(candidateInterface.TypeArguments[0], requestType))
                {
                    return new InterfacePlan(
                        $"global::SharedKernel.Mediator.IRequest<{candidateInterface.TypeArguments[1].ToDisplayString(SymbolDisplayFormats.FullyQualifiedWithNullability)}>",
                        $"IRequest<{candidateInterface.TypeArguments[1].ToDisplayString(SymbolDisplayFormats.MinimallyQualifiedWithNullability)}>");
                }
            }
        }

        var requestParameter = targetMethod.Parameters.First();
        var requestContract = requestParameter.Type.ToDisplayString(SymbolDisplayFormats.FullyQualifiedWithNullability);
        var displayName = requestParameter.Type.ToDisplayString(SymbolDisplayFormats.MinimallyQualifiedWithNullability);
        return new InterfacePlan(requestContract, displayName);
    }

    private static IMethodSymbol? GetCandidateTargetMethod(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        CancellationToken cancellationToken)
    {
        var symbolInfo = semanticModel.GetSymbolInfo(invocation, cancellationToken);
        if (symbolInfo.Symbol is IMethodSymbol method)
        {
            return method;
        }

        return symbolInfo.CandidateSymbols.OfType<IMethodSymbol>().FirstOrDefault();
    }

    private static bool IsSenderSendMethod(Compilation compilation, IMethodSymbol method)
    {
        var senderType = compilation.GetTypeByMetadataName(MetadataNames.Sender);
        return senderType is not null
               && string.Equals(method.Name, "Send", StringComparison.Ordinal)
               && (SymbolEqualityComparer.Default.Equals(method.ContainingType, senderType)
                   || SymbolEqualityComparer.Default.Equals(method.ContainingType.OriginalDefinition, senderType)
                   || method.ContainingType.AllInterfaces.Any(
                       candidate => SymbolEqualityComparer.Default.Equals(candidate, senderType)
                                    || SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, senderType)));
    }

    private static bool SymbolMatches(Compilation compilation, INamedTypeSymbol candidate, string metadataName)
    {
        var expected = compilation.GetTypeByMetadataName(metadataName);
        return expected is not null && SymbolEqualityComparer.Default.Equals(candidate.OriginalDefinition, expected);
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateTypes(INamespaceSymbol @namespace)
    {
        foreach (var nestedNamespace in @namespace.GetNamespaceMembers())
        {
            foreach (var nestedType in EnumerateTypes(nestedNamespace))
            {
                yield return nestedType;
            }
        }

        foreach (var type in @namespace.GetTypeMembers())
        {
            foreach (var nestedType in EnumerateTypes(type))
            {
                yield return nestedType;
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> EnumerateTypes(INamedTypeSymbol type)
    {
        yield return type;

        foreach (var nestedType in type.GetTypeMembers())
        {
            foreach (var childType in EnumerateTypes(nestedType))
            {
                yield return childType;
            }
        }
    }

    private readonly struct InterfacePlan(string interfaceTypeName, string interfaceDisplayName)
    {
        public string InterfaceTypeName { get; } = interfaceTypeName;

        public string InterfaceDisplayName { get; } = interfaceDisplayName;
    }
}
