using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace SharedKernel.Style.CodeFixes;

/// <summary>
/// Regroups overloaded methods so same-name declarations stay adjacent after a rename.
/// </summary>
internal static class MethodOverloadGroupOrganizer
{
    /// <summary>
    /// Reorders the renamed method and any same-name overloads into a contiguous, deterministic block.
    /// </summary>
    public static async Task<Solution> Organize(
        Solution solution,
        DocumentId documentId,
        int diagnosticSpanStart,
        string updatedName,
        CancellationToken cancellationToken)
    {
        var document = solution.GetDocument(documentId);
        if (document is null)
        {
            return solution;
        }

        var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return solution;
        }

        var targetMethod = root.FindNode(new TextSpan(diagnosticSpanStart, updatedName.Length), getInnermostNodeForTie: true)
            .FirstAncestorOrSelf<MethodDeclarationSyntax>();
        if (targetMethod?.Parent is not TypeDeclarationSyntax containingType)
        {
            return solution;
        }

        var members = containingType.Members;
        List<int> overloadIndices = [];

        for (var index = 0; index < members.Count; index++)
        {
            if (members[index] is MethodDeclarationSyntax candidateMethod
                && string.Equals(candidateMethod.Identifier.ValueText, updatedName, StringComparison.Ordinal))
            {
                overloadIndices.Add(index);
            }
        }

        if (overloadIndices.Count <= 1)
        {
            return solution;
        }

        var firstIndex = overloadIndices.Min();
        var orderedOverloads = overloadIndices
            .Select(index => (MethodDeclarationSyntax)members[index])
            .ToList();
        orderedOverloads.Sort(MethodOverloadDeclarationComparer.Instance);

        var reorderedMembers = new List<MemberDeclarationSyntax>(members.Count);

        for (var index = 0; index < members.Count; index++)
        {
            if (index == firstIndex)
            {
                reorderedMembers.AddRange(orderedOverloads);
            }

            if (!overloadIndices.Contains(index))
            {
                reorderedMembers.Add(members[index]);
            }
        }

        var updatedType = containingType.WithMembers(SyntaxFactory.List(reorderedMembers));
        var updatedRoot = root.ReplaceNode(containingType, updatedType);
        return document.WithSyntaxRoot(updatedRoot).Project.Solution;
    }

    private sealed class MethodOverloadDeclarationComparer : IComparer<MethodDeclarationSyntax>
    {
        public static MethodOverloadDeclarationComparer Instance { get; } = new();

        public int Compare(MethodDeclarationSyntax? left, MethodDeclarationSyntax? right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left is null)
            {
                return -1;
            }

            if (right is null)
            {
                return 1;
            }

            var parameterCountComparison = left.ParameterList.Parameters.Count.CompareTo(right.ParameterList.Parameters.Count);
            if (parameterCountComparison != 0)
            {
                return parameterCountComparison;
            }

            var typeParameterCountComparison = left.TypeParameterList?.Parameters.Count.CompareTo(right.TypeParameterList?.Parameters.Count ?? 0)
                ?? (right.TypeParameterList is null ? 0 : -1);
            if (typeParameterCountComparison != 0)
            {
                return typeParameterCountComparison;
            }

            for (var index = 0; index < left.ParameterList.Parameters.Count; index++)
            {
                var parameterComparison = CompareParameter(left.ParameterList.Parameters[index], right.ParameterList.Parameters[index]);
                if (parameterComparison != 0)
                {
                    return parameterComparison;
                }
            }

            return string.Compare(left.Identifier.ValueText, right.Identifier.ValueText, StringComparison.Ordinal);
        }

        private static int CompareParameter(ParameterSyntax left, ParameterSyntax right)
        {
            var paramsComparison = HasParamsModifier(left).CompareTo(HasParamsModifier(right));
            if (paramsComparison != 0)
            {
                return paramsComparison;
            }

            var refKindComparison = GetRefKindOrder(left).CompareTo(GetRefKindOrder(right));
            if (refKindComparison != 0)
            {
                return refKindComparison;
            }

            var typeComparison = string.Compare(left.Type?.ToString(), right.Type?.ToString(), StringComparison.Ordinal);
            if (typeComparison != 0)
            {
                return typeComparison;
            }

            return string.Compare(left.Identifier.ValueText, right.Identifier.ValueText, StringComparison.Ordinal);
        }

        private static bool HasParamsModifier(ParameterSyntax parameter)
        {
            return parameter.Modifiers.Any(SyntaxKind.ParamsKeyword);
        }

        private static int GetRefKindOrder(ParameterSyntax parameter)
        {
            if (parameter.Modifiers.Any(SyntaxKind.RefKeyword))
            {
                return 1;
            }

            if (parameter.Modifiers.Any(SyntaxKind.OutKeyword))
            {
                return 2;
            }

            if (parameter.Modifiers.Any(SyntaxKind.InKeyword))
            {
                return 3;
            }

            return 0;
        }
    }
}
