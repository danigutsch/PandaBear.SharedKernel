using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace PandaBear.Testing.Analyzers;

public sealed partial class SharedKernelTestingAnalyzer
{
    private static readonly DiagnosticDescriptor ActivityListenerSourceIdentityRule = new(
        TestingDiagnosticIds.ActivityListenerSourceIdentity,
        "Test activity listeners should filter by source identity",
        "ActivityListener.ShouldListenTo should compare the expected ActivitySource instance by reference instead of matching ActivitySource.Name",
        TestingCategory, DiagnosticSeverity.Warning, true,
        helpLinkUri: "https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Testing.Analyzers/README.md#sktest010");

    private static readonly DiagnosticDescriptor SutConstructionRule = new(
        TestingDiagnosticIds.SutConstructionInAct,
        "Construct the system under test in Arrange",
        "Construct '{0}' in Arrange before observing its behavior in Act",
        TestingCategory, DiagnosticSeverity.Warning, true,
        helpLinkUri: "https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Testing.Analyzers/README.md#sktest016");

    private static void AnalyzeListenerAssignment(OperationAnalysisContext context)
    {
        var assignment = (ISimpleAssignmentOperation)context.Operation;
        if (assignment.Target is not IPropertyReferenceOperation { Property.Name: "ShouldListenTo" } target
            || target.Property.ContainingType.ToDisplayString() != "System.Diagnostics.ActivityListener")
        {
            return;
        }

        var value = assignment.Value;
        while (value is IConversionOperation conversion)
        {
            value = conversion.Operand;
        }

        if (value is IDelegateCreationOperation creation)
        {
            value = creation.Target;
        }

        if (value is not IAnonymousFunctionOperation lambda || lambda.Symbol.Parameters.Length != 1)
        {
            return;
        }

        foreach (var node in lambda.Body.Descendants())
        {
            if (node is IPropertyReferenceOperation { Property.Name: "Name", Instance: IParameterReferenceOperation parameter } property
                && property.Property.ContainingType.ToDisplayString() == "System.Diagnostics.ActivitySource"
                && SymbolEqualityComparer.Default.Equals(parameter.Parameter, lambda.Symbol.Parameters[0]))
            {
                context.ReportDiagnostic(Diagnostic.Create(ActivityListenerSourceIdentityRule, property.Syntax.GetLocation()));
                return;
            }
        }
    }

    private static void AnalyzeSutConstruction(SyntaxNodeAnalysisContext context, MethodDeclarationSyntax method)
    {
        var markers = GetArrangeActAssertMarkers(method);
        if (!HasSingleCompleteArrangeActAssertMarkerSet(markers) || !HasOrderedArrangeActAssertMarkers(markers))
        {
            return;
        }

        var comments = method.DescendantTrivia().Where(static trivia => trivia.IsKind(SyntaxKind.SingleLineCommentTrivia)).ToArray();
        var start = comments.First(static trivia => trivia.ToString().Substring(2).Trim() == ActMarker).Span.End;
        var end = comments.First(static trivia => trivia.ToString().Substring(2).Trim() == AssertMarker).SpanStart;
        foreach (var node in method.DescendantNodes(static node => node is not AnonymousFunctionExpressionSyntax and not LocalFunctionStatementSyntax))
        {
            if (node is not BaseObjectCreationExpressionSyntax creation || node.SpanStart < start || node.Span.End > end)
            {
                continue;
            }

            var name = creation.Parent switch
            {
                EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax variable } => variable.Identifier.ValueText,
                AssignmentExpressionSyntax { Left: IdentifierNameSyntax identifier } => identifier.Identifier.ValueText,
                _ => null,
            };
            if (name is "sut" or "systemUnderTest")
            {
                context.ReportDiagnostic(Diagnostic.Create(SutConstructionRule, creation.GetLocation(), name));
            }
        }
    }
}
