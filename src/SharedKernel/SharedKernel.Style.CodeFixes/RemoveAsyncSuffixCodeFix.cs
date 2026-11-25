using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
namespace SharedKernel.Style.CodeFixes;

/// <summary>
/// Registers the rename-based code fix for <c>SKSTYLE001</c> diagnostics.
/// </summary>
internal static class RemoveAsyncSuffixCodeFix
{
    private const string AsyncSuffix = "Async";

    /// <summary>
    /// Registers a rename code action when the diagnostic targets a method declaration.
    /// </summary>
    public static async Task Register(CodeFixContext context, Diagnostic diagnostic)
    {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        var semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);

        if (root is null || semanticModel is null)
        {
            return;
        }

        var methodDeclaration = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (methodDeclaration is null)
        {
            return;
        }

        if (semanticModel.GetDeclaredSymbol(methodDeclaration, context.CancellationToken) is not IMethodSymbol methodSymbol
            || !methodSymbol.Name.EndsWith(AsyncSuffix, StringComparison.Ordinal))
        {
            return;
        }

        if (methodSymbol.IsOverride
            || methodSymbol.ExplicitInterfaceImplementations.Length > 0
            || ImplementsInterfaceContract(methodSymbol))
        {
            return;
        }

        var updatedName = methodSymbol.Name.Substring(0, methodSymbol.Name.Length - AsyncSuffix.Length);
        if (string.IsNullOrWhiteSpace(updatedName)
            || HasRenameConflict(methodSymbol, updatedName))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                title: $"Rename to '{updatedName}'",
                createChangedSolution: cancellationToken => RenameMethod(context.Document, methodSymbol, updatedName, cancellationToken),
                equivalenceKey: $"RenameMethod:{updatedName}"),
            diagnostic);
    }

    private static async Task<Solution> RenameMethod(
        Document document,
        IMethodSymbol methodSymbol,
        string updatedName,
        CancellationToken cancellationToken)
    {
        var renamedSolution = await Renamer.RenameSymbolAsync(
            document.Project.Solution,
            methodSymbol,
            new SymbolRenameOptions(),
            updatedName,
            cancellationToken).ConfigureAwait(false);

        var renamedDocument = renamedSolution.GetDocument(document.Id);
        if (renamedDocument is null)
        {
            return renamedSolution;
        }

        var renamedRoot = await renamedDocument.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var renamedSemanticModel = await renamedDocument.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (renamedRoot is null || renamedSemanticModel is null)
        {
            return renamedSolution;
        }

        var renamedMethodDeclaration = renamedRoot.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(candidate =>
                renamedSemanticModel.GetDeclaredSymbol(candidate, cancellationToken) is IMethodSymbol candidateSymbol
                && IsRenamedMethodMatch(candidateSymbol, methodSymbol, updatedName));
        if (renamedMethodDeclaration is null)
        {
            return renamedSolution;
        }

        return await MethodOverloadGroupOrganizer.Organize(
            renamedSolution,
            document.Id,
            renamedMethodDeclaration,
            updatedName,
            cancellationToken).ConfigureAwait(false);
    }

    private static bool IsRenamedMethodMatch(IMethodSymbol candidateSymbol, ISymbol originalSymbol, string updatedName)
    {
        if (originalSymbol is not IMethodSymbol originalMethodSymbol)
        {
            return false;
        }

        if (!string.Equals(candidateSymbol.Name, updatedName, StringComparison.Ordinal)
            || !string.Equals(GetTypeIdentity(candidateSymbol.ContainingType), GetTypeIdentity(originalMethodSymbol.ContainingType), StringComparison.Ordinal)
            || candidateSymbol.Parameters.Length != originalMethodSymbol.Parameters.Length
            || candidateSymbol.Arity != originalMethodSymbol.Arity
            || candidateSymbol.MethodKind != originalMethodSymbol.MethodKind)
        {
            return false;
        }

        return ParametersMatch(candidateSymbol.Parameters, originalMethodSymbol.Parameters);
    }

    private static string GetTypeIdentity(INamedTypeSymbol containingType)
    {
        return containingType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    private static bool HasRenameConflict(IMethodSymbol methodSymbol, string updatedName)
    {
        for (INamedTypeSymbol? containingType = methodSymbol.ContainingType; containingType is not null; containingType = containingType.BaseType)
        {
            if (containingType
                .GetMembers(updatedName)
                .OfType<IMethodSymbol>()
                .Any(candidate =>
                    !SymbolEqualityComparer.Default.Equals(candidate, methodSymbol)
                    && candidate.MethodKind == methodSymbol.MethodKind
                    && candidate.Parameters.Length == methodSymbol.Parameters.Length
                    && candidate.TypeParameters.Length == methodSymbol.TypeParameters.Length
                    && candidate.Arity == methodSymbol.Arity
                    && ParametersMatch(candidate.Parameters, methodSymbol.Parameters)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ParametersMatch(ImmutableArray<IParameterSymbol> left, ImmutableArray<IParameterSymbol> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (var index = 0; index < left.Length; index++)
        {
            if (!SymbolEqualityComparer.Default.Equals(left[index].Type, right[index].Type)
                || left[index].RefKind != right[index].RefKind)
            {
                return false;
            }
        }

        return true;
    }

    private static bool ImplementsInterfaceContract(IMethodSymbol methodSymbol)
    {
        return methodSymbol.ContainingType.AllInterfaces
            .SelectMany(@interface => @interface.GetMembers(methodSymbol.Name).OfType<IMethodSymbol>())
            .Any(interfaceMethod => SymbolEqualityComparer.Default.Equals(
                methodSymbol.ContainingType.FindImplementationForInterfaceMember(interfaceMethod),
                methodSymbol));
    }
}
