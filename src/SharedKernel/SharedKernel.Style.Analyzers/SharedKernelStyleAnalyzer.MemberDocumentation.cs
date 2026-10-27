using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PandaBear.Style.Analyzers;

public sealed partial class SharedKernelStyleAnalyzer
{
    private static readonly DiagnosticDescriptor MemberDocumentationRule = new(
        StyleDiagnosticIds.UndocumentedMember,
        title: "Internal types and members should have XML documentation",
        messageFormat: "{0} '{1}' should have XML documentation",
        category: "Style",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Types, methods, constructors, properties, fields, and events that are internal, or public inside an internal type, need a summary, because CS1591 covers only externally visible members. A property synthesized from a positional record parameter is documented by the matching param element on the record. A partial symbol is documented when any part is documented. Private members, overrides, explicit interface implementations, compiler-generated members, and generated code are not checked.",
        helpLinkUri: "https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Style.Analyzers/README.md#skstyle017");

    private static void AnalyzeMemberDocumentation(SymbolAnalysisContext context)
    {
        var symbol = context.Symbol;
        var sourceTree = symbol.Locations.FirstOrDefault(static location => location.IsInSource)?.SourceTree;
        var documentPublicMembers = StyleAnalyzerConfigOptions.Parse(context.Options.AnalyzerConfigOptionsProvider, sourceTree).DocumentPublicMembers;
        if (symbol.IsImplicitlyDeclared
            || symbol.IsOverride
            || !(documentPublicMembers ? IsNonPrivate(symbol) : IsInternalSurface(symbol))
            || IsTestMethod(symbol)
            || symbol is IMethodSymbol { MethodKind: not (MethodKind.Ordinary or MethodKind.Constructor or MethodKind.UserDefinedOperator or MethodKind.Conversion) }
            || symbol is IMethodSymbol { ExplicitInterfaceImplementations.Length: > 0 }
            || symbol is IPropertySymbol { IsIndexer: true }
            || symbol is IPropertySymbol { ExplicitInterfaceImplementations.Length: > 0 }
            || symbol is IEventSymbol { ExplicitInterfaceImplementations.Length: > 0 })
        {
            return;
        }

        foreach (var reference in symbol.DeclaringSyntaxReferences)
        {
            if (IsDocumented(reference.GetSyntax(context.CancellationToken)))
            {
                return;
            }
        }

        if (symbol.Locations.FirstOrDefault(static location => location.IsInSource) is { } location)
        {
            var name = symbol is IMethodSymbol { MethodKind: MethodKind.Constructor } ? symbol.ContainingType.Name : symbol.Name;
            context.ReportDiagnostic(Diagnostic.Create(MemberDocumentationRule, location, DescribeKind(symbol), name));
        }
    }

    private static string DescribeKind(ISymbol symbol)
    {
        return symbol switch
        {
            INamedTypeSymbol => "Type",
            IMethodSymbol { MethodKind: MethodKind.Constructor } => "Constructor",
            IMethodSymbol => "Method",
            IPropertySymbol => "Property",
            IFieldSymbol => "Field",
            _ => "Event",
        };
    }

    private static bool IsDocumented(SyntaxNode node)
    {
        return node switch
        {
            VariableDeclaratorSyntax { Parent.Parent: MemberDeclarationSyntax member } => HasDocumentation(member),
            ParameterSyntax { Parent.Parent: RecordDeclarationSyntax record } parameter => HasParamDocumentation(record, parameter.Identifier.ValueText),
            ParameterSyntax => true,
            MemberDeclarationSyntax member => HasDocumentation(member),
            _ => true,
        };
    }

    // xunit test methods describe themselves through their names and bodies and are not documented.
    private static bool IsTestMethod(ISymbol symbol)
    {
        return symbol is IMethodSymbol method
            && method.GetAttributes().Any(static attribute =>
                attribute.AttributeClass is { } attributeClass && InheritsFromFactAttribute(attributeClass));
    }

    private static bool InheritsFromFactAttribute(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current is { Name: "FactAttribute", ContainingNamespace: { Name: "Xunit", ContainingNamespace.IsGlobalNamespace: true } })
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsNonPrivate(ISymbol symbol)
    {
        for (var current = symbol; current is not null and not INamespaceSymbol; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility is Accessibility.Private or Accessibility.NotApplicable)
            {
                return false;
            }
        }

        return true;
    }

    // A symbol is in the internal surface when it is not private at any level but also not reachable from outside the assembly.
    private static bool IsInternalSurface(ISymbol symbol)
    {
        var visible = true;
        for (var current = symbol; current is not (null or INamespaceSymbol); current = current.ContainingType)
        {
            if (current.DeclaredAccessibility is Accessibility.Private or Accessibility.NotApplicable)
            {
                return false;
            }

            visible &= current.DeclaredAccessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal;
        }

        return !visible;
    }

    private static bool HasDocumentation(SyntaxNode member)
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
