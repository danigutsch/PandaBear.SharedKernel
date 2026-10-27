using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PandaBear.Style.Analyzers;

public sealed partial class SharedKernelStyleAnalyzer
{
    private static readonly DiagnosticDescriptor PropertyDocumentationRule = new(
        StyleDiagnosticIds.UndocumentedProperty,
        title: "Properties should have XML documentation",
        messageFormat: "Property '{0}' should have XML documentation",
        category: "Style",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Class and record properties that CS1591 does not cover, because they are not externally visible, need a summary. A property synthesized from a positional record parameter is documented by the matching param element on the record. Overrides, explicit interface implementations, and generated code are not checked.",
        helpLinkUri: "https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Style.Analyzers/README.md#skstyle017");

    private static void AnalyzePropertyDocumentation(SymbolAnalysisContext context)
    {
        var property = (IPropertySymbol)context.Symbol;
        if (property.IsOverride
            || property.IsIndexer
            || property.ExplicitInterfaceImplementations.Length > 0
            || property.ContainingType is not { TypeKind: TypeKind.Class } and not { IsRecord: true }
            || IsExternallyVisible(property))
        {
            return;
        }

        foreach (var reference in property.DeclaringSyntaxReferences)
        {
            var node = reference.GetSyntax(context.CancellationToken);
            if (node is PropertyDeclarationSyntax declaration
                ? HasDocumentation(declaration)
                : node is ParameterSyntax { Parent.Parent: RecordDeclarationSyntax record } parameter
                    && HasParamDocumentation(record, parameter.Identifier.ValueText))
            {
                return;
            }
        }

        if (property.Locations.FirstOrDefault(static location => location.IsInSource) is { } location)
        {
            context.ReportDiagnostic(Diagnostic.Create(PropertyDocumentationRule, location, property.Name));
        }
    }

    private static bool IsExternallyVisible(ISymbol symbol)
    {
        for (var current = symbol; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasDocumentation(MemberDeclarationSyntax member)
    {
        return member.GetLeadingTrivia().Any(static trivia => trivia.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia)
            || trivia.IsKind(SyntaxKind.MultiLineDocumentationCommentTrivia));
    }

    private static bool HasParamDocumentation(RecordDeclarationSyntax record, string parameterName)
    {
        return record.GetLeadingTrivia()
            .Select(static trivia => trivia.GetStructure())
            .OfType<DocumentationCommentTriviaSyntax>()
            .SelectMany(static documentation => documentation.DescendantNodes().OfType<XmlElementSyntax>())
            .Any(element => element.StartTag.Name.ToString() == "param"
                && element.StartTag.Attributes.OfType<XmlNameAttributeSyntax>().Any(attribute => attribute.Identifier.Identifier.ValueText == parameterName));
    }
}
