using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SharedKernel.Mediator.SourceGenerator;

namespace SharedKernel.Mediator.CodeFixes;

/// <summary>
/// Repairs the explicit-interface-only handler shape by adding a public forwarding method.
/// </summary>
internal static class InvalidHandlerSignatureCodeFix
{
    /// <summary>
    /// Registers the invalid-signature fix when the diagnostic represents an explicit-interface-only handler.
    /// </summary>
    /// <param name="context">The code-fix registration context.</param>
    /// <param name="diagnostic">The invalid-signature diagnostic.</param>
    public static async Task RegisterAsync(CodeFixContext context, Diagnostic diagnostic)
    {
        var document = context.Document;
        var root = await document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var semanticModel = await document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

        if (root is null || semanticModel is null)
        {
            return;
        }

        if (!TryFindFixTarget(root, semanticModel, diagnostic, context.CancellationToken, out var handlerDeclaration, out var plan))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: "Add public Handle method",
                createChangedDocument: cancellationToken => AddForwardingHandleMethodAsync(document, handlerDeclaration, plan, cancellationToken),
                equivalenceKey: $"AddHandle:{handlerDeclaration.Identifier.ValueText}"),
            diagnostic);
    }

    private static async Task<Document> AddForwardingHandleMethodAsync(
        Document document,
        TypeDeclarationSyntax handlerDeclaration,
        ForwardingPlan plan,
        CancellationToken cancellationToken)
    {
        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);

        if (root is null)
        {
            return document;
        }

        var methodDeclaration = Microsoft.CodeAnalysis.CSharp.SyntaxFactory.ParseMemberDeclaration(plan.Render());

        if (methodDeclaration is null)
        {
            return document;
        }

        var updatedDeclaration = handlerDeclaration.AddMembers(methodDeclaration);
        var updatedRoot = root.ReplaceNode(handlerDeclaration, updatedDeclaration);

        return document.WithSyntaxRoot(updatedRoot);
    }

    private static bool TryFindFixTarget(
        SyntaxNode root,
        SemanticModel semanticModel,
        Diagnostic diagnostic,
        CancellationToken cancellationToken,
        out TypeDeclarationSyntax handlerDeclaration,
        out ForwardingPlan plan)
    {
        var declarationNode = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
        var diagnosticDeclaration = declarationNode.FirstAncestorOrSelf<TypeDeclarationSyntax>();

        if (diagnosticDeclaration is not null
            && semanticModel.GetDeclaredSymbol(diagnosticDeclaration, cancellationToken) is INamedTypeSymbol diagnosticHandlerSymbol
            && TryCreatePlan(diagnosticDeclaration, diagnosticHandlerSymbol, semanticModel, out plan))
        {
            handlerDeclaration = diagnosticDeclaration;
            return true;
        }

        foreach (var candidateDeclaration in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            if (semanticModel.GetDeclaredSymbol(candidateDeclaration, cancellationToken) is not INamedTypeSymbol candidateHandlerSymbol)
            {
                continue;
            }

            if (TryCreatePlan(candidateDeclaration, candidateHandlerSymbol, semanticModel, out plan))
            {
                handlerDeclaration = candidateDeclaration;
                return true;
            }
        }

        handlerDeclaration = null!;
        plan = default;
        return false;
    }

    private static bool TryCreatePlan(
        TypeDeclarationSyntax handlerDeclaration,
        INamedTypeSymbol handlerSymbol,
        SemanticModel semanticModel,
        out ForwardingPlan plan)
    {
        foreach (var methodDeclaration in handlerDeclaration.Members.OfType<MethodDeclarationSyntax>())
        {
            if (methodDeclaration.ExplicitInterfaceSpecifier is null)
            {
                continue;
            }

            if (semanticModel.GetDeclaredSymbol(methodDeclaration) is not IMethodSymbol method)
            {
                continue;
            }

            if (!string.Equals(methodDeclaration.Identifier.ValueText, "Handle", StringComparison.Ordinal))
            {
                continue;
            }

            if (semanticModel.GetTypeInfo(methodDeclaration.ExplicitInterfaceSpecifier.Name).Type is not INamedTypeSymbol interfaceType
                || !IsHandledMediatorInterface(interfaceType, semanticModel.Compilation))
            {
                continue;
            }

            if (HasCompatibleOrdinaryHandleMethod(handlerSymbol, method))
            {
                continue;
            }

            plan = new ForwardingPlan(
                method.ReturnType.ToDisplayString(SymbolDisplayFormats.FullyQualifiedWithNullability),
                method.Parameters[0].Type.ToDisplayString(SymbolDisplayFormats.FullyQualifiedWithNullability),
                interfaceType.ToDisplayString(SymbolDisplayFormats.FullyQualifiedWithNullability));
            return true;
        }

        plan = default;
        return false;
    }

    private static bool HasCompatibleOrdinaryHandleMethod(INamedTypeSymbol handlerSymbol, IMethodSymbol targetMethod)
    {
        return handlerSymbol.GetMembers("Handle")
            .OfType<IMethodSymbol>()
            .Any(
                method => method.MethodKind == MethodKind.Ordinary
                          && method.Parameters.Length == 2
                          && SymbolEqualityComparer.Default.Equals(method.ReturnType, targetMethod.ReturnType)
                          && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, targetMethod.Parameters[0].Type)
                          && SymbolEqualityComparer.Default.Equals(method.Parameters[1].Type, targetMethod.Parameters[1].Type));
    }

    private static bool IsHandledMediatorInterface(INamedTypeSymbol interfaceSymbol, Compilation compilation)
    {
        return Matches(interfaceSymbol, compilation, MetadataNames.RequestHandler)
               || Matches(interfaceSymbol, compilation, MetadataNames.QueryHandler)
               || Matches(interfaceSymbol, compilation, MetadataNames.CommandHandler)
               || Matches(interfaceSymbol, compilation, MetadataNames.CommandHandlerOfResponse);
    }

    private static bool Matches(INamedTypeSymbol interfaceSymbol, Compilation compilation, string metadataName)
    {
        var expected = compilation.GetTypeByMetadataName(metadataName);
        return expected is not null && SymbolEqualityComparer.Default.Equals(interfaceSymbol.OriginalDefinition, expected);
    }

    private readonly struct ForwardingPlan(string returnType, string requestType, string interfaceType)
    {
        public string ReturnType { get; } = returnType;

        public string RequestType { get; } = requestType;

        public string InterfaceType { get; } = interfaceType;

        public string Render()
        {
            return
                $"public {ReturnType} Handle({RequestType} request, global::System.Threading.CancellationToken ct){Environment.NewLine}" +
                $"{{{Environment.NewLine}" +
                $"    return (({InterfaceType})this).Handle(request, ct);{Environment.NewLine}" +
                $"}}";
        }
    }
}
