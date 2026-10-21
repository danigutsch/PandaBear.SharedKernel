using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PandaBear.Style.Analyzers;

internal static class GovernedLiteralAnalysis
{
    internal static readonly DiagnosticDescriptor LiteralRule = new(
        StyleDiagnosticIds.GovernedLiteral, "Use the governed constant",
        "Replace literal '{0}' with '{1}'", "Style", DiagnosticSeverity.Warning, isEnabledByDefault: true);
    internal static readonly DiagnosticDescriptor CatalogRule = new(
        StyleDiagnosticIds.InvalidGovernedLiteralCatalog, "Invalid governed-literal catalog",
        "Invalid GovernedLiterals.txt: {0}", "Style", DiagnosticSeverity.Warning, isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.CompilationEnd]);

    public static void Initialize(CompilationStartAnalysisContext context)
    {
        var files = context.Options.AdditionalFiles.Where(static file =>
            string.Equals(Path.GetFileName(file.Path), "GovernedLiterals.txt", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (files.Length == 0)
        {
            return;
        }

        ImmutableArray<GovernedLiteral> catalog;
        try
        {
            var texts = files.Select(file => file.GetText(context.CancellationToken)?.ToString()
                ?? throw new FormatException("Catalog could not be read."));
            catalog = GovernedLiteralCatalog.Parse(string.Join("\n", texts));
        }
        catch (FormatException exception)
        {
            context.RegisterCompilationEndAction(end => end.ReportDiagnostic(Diagnostic.Create(CatalogRule, Location.None, exception.Message)));
            return;
        }

        var owners = catalog.Where(static entry => entry.Justification is null)
            .ToImmutableDictionary(static entry => entry.Value, StringComparer.Ordinal);
        context.RegisterSyntaxNodeAction(syntaxContext =>
        {
            var literal = (LiteralExpressionSyntax)syntaxContext.Node;
            if (!owners.TryGetValue(literal.Token.ValueText, out var owner))
            {
                return;
            }

            var variable = literal.FirstAncestorOrSelf<VariableDeclaratorSyntax>();
            if (variable?.Parent is VariableDeclarationSyntax { Parent: FieldDeclarationSyntax }
                && syntaxContext.SemanticModel.GetDeclaredSymbol(variable, syntaxContext.CancellationToken) is IFieldSymbol { IsConst: true } field
                && field.ConstantValue is string value && value == owner.Value
                && GovernedLiteralCatalog.Matches(owner, field))
            {
                return;
            }

            syntaxContext.ReportDiagnostic(Diagnostic.Create(LiteralRule, literal.GetLocation(), owner.Value, owner.Symbol));
        }, SyntaxKind.StringLiteralExpression);
    }
}
