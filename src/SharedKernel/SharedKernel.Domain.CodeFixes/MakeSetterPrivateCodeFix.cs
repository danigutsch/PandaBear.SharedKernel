using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;

namespace PandaBear.Domain.CodeFixes;

/// <summary>
/// Registers the <c>SKDOM001</c> fix that makes a domain-model setter private.
/// </summary>
/// <remarks>
/// The fix is offered only when no code outside the declaring type, or its nested types, writes the
/// property, so applying it cannot break a caller in the solution.
/// </remarks>
internal static class MakeSetterPrivateCodeFix
{
    private const string Title = "Make setter private";
    private const string EquivalenceKey = "MakeDomainSetterPrivate";

    /// <summary>
    /// Registers the fix for one <c>SKDOM001</c> diagnostic when it is safe.
    /// </summary>
    public static async Task Register(CodeFixContext context, Diagnostic diagnostic)
    {
        var cancellationToken = context.CancellationToken;
        var root = await context.Document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        var setter = root?.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<AccessorDeclarationSyntax>();
        if (root is null
            || setter is null
            || !setter.IsKind(SyntaxKind.SetAccessorDeclaration)
            || setter.Parent?.Parent is not BasePropertyDeclarationSyntax propertyDeclaration)
        {
            return;
        }

        var semanticModel = await context.Document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
        if (semanticModel?.GetDeclaredSymbol(propertyDeclaration, cancellationToken) is not IPropertySymbol property
            || !CanBecomePrivate(property)
            || await IsWrittenOutsideDeclaringType(context.Document.Project.Solution, property, cancellationToken).ConfigureAwait(false))
        {
            return;
        }

        context.RegisterCodeFix(
            CodeAction.Create(
                Title,
                _ => Task.FromResult(context.Document.WithSyntaxRoot(root.ReplaceNode(setter, MakePrivate(setter)))),
                EquivalenceKey),
            diagnostic);
    }

    private static bool CanBecomePrivate(IPropertySymbol property)
    {
        var containingType = property.ContainingType;
        if (containingType.TypeKind is not (TypeKind.Class or TypeKind.Struct)
            || property.IsAbstract
            || property.IsVirtual
            || property.IsOverride
            || !property.ExplicitInterfaceImplementations.IsEmpty)
        {
            return false;
        }

        return !containingType.AllInterfaces
            .SelectMany(static candidate => candidate.GetMembers().OfType<IPropertySymbol>())
            .Any(interfaceProperty => interfaceProperty.SetMethod is not null
                && SymbolEqualityComparer.Default.Equals(containingType.FindImplementationForInterfaceMember(interfaceProperty), property));
    }

    private static async Task<bool> IsWrittenOutsideDeclaringType(
        Solution solution,
        IPropertySymbol property,
        CancellationToken cancellationToken)
    {
        var declaringType = property.ContainingType.OriginalDefinition;
        var referencedSymbols = await SymbolFinder.FindReferencesAsync(property, solution, cancellationToken).ConfigureAwait(false);
        foreach (var location in referencedSymbols.SelectMany(static referencedSymbol => referencedSymbol.Locations))
        {
            if (location.IsImplicit || !location.Location.IsInSource)
            {
                return true;
            }

            var root = await location.Document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var reference = root?.FindNode(location.Location.SourceSpan, getInnermostNodeForTie: true);
            if (reference is null)
            {
                return true;
            }

            if (!IsWrite(reference))
            {
                continue;
            }

            var semanticModel = await location.Document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var enclosingSymbol = semanticModel?.GetEnclosingSymbol(reference.SpanStart, cancellationToken);
            if (!IsInsideType(enclosingSymbol, declaringType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsInsideType(ISymbol? symbol, INamedTypeSymbol declaringType)
    {
        for (var current = symbol as INamedTypeSymbol ?? symbol?.ContainingType; current is not null; current = current.ContainingType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, declaringType))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsWrite(SyntaxNode reference)
    {
        var expression = reference.Parent switch
        {
            MemberAccessExpressionSyntax memberAccess when memberAccess.Name == reference => memberAccess,
            MemberBindingExpressionSyntax memberBinding when memberBinding.Name == reference => memberBinding,
            _ => reference
        };

        while (expression.Parent is ParenthesizedExpressionSyntax parenthesized)
        {
            expression = parenthesized;
        }

        return expression.Parent switch
        {
            AssignmentExpressionSyntax assignment => assignment.Left == expression,
            PrefixUnaryExpressionSyntax prefix => prefix.IsKind(SyntaxKind.PreIncrementExpression) || prefix.IsKind(SyntaxKind.PreDecrementExpression),
            PostfixUnaryExpressionSyntax postfix => postfix.IsKind(SyntaxKind.PostIncrementExpression) || postfix.IsKind(SyntaxKind.PostDecrementExpression),
            ArgumentSyntax { Parent: TupleExpressionSyntax tuple } => IsDeconstructionTarget(tuple),
            _ => false
        };
    }

    private static bool IsDeconstructionTarget(TupleExpressionSyntax tuple)
    {
        return tuple.Parent switch
        {
            AssignmentExpressionSyntax assignment => assignment.Left == tuple,
            ArgumentSyntax { Parent: TupleExpressionSyntax outerTuple } => IsDeconstructionTarget(outerTuple),
            _ => false
        };
    }

    private static AccessorDeclarationSyntax MakePrivate(AccessorDeclarationSyntax setter)
    {
        var leadingTrivia = setter.Modifiers.Count > 0 ? setter.Modifiers[0].LeadingTrivia : setter.Keyword.LeadingTrivia;
        var privateModifier = SyntaxFactory.Token(leadingTrivia, SyntaxKind.PrivateKeyword, SyntaxFactory.TriviaList(SyntaxFactory.Space));

        return setter
            .WithModifiers(SyntaxFactory.TokenList(privateModifier))
            .WithKeyword(setter.Keyword.WithLeadingTrivia(SyntaxFactory.TriviaList()));
    }
}
