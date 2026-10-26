using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PandaBear.Style.Analyzers;

public sealed partial class SharedKernelStyleAnalyzer
{
    private const int JsonSerializerDefaultsWeb = 1;

    private static readonly DiagnosticDescriptor CaseInsensitiveJsonContextRule = new(
        StyleDiagnosticIds.CaseInsensitiveJsonContext,
        title: "Require case-insensitive property names on API JSON contexts",
        messageFormat: "Set PropertyNameCaseInsensitive = true in the JsonSourceGenerationOptions of '{0}'",
        category: "Style",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: false,
        description: "Contexts that read external API payloads should match property names case-insensitively so casing drift does not break deserialization. Enable this rule for API payload scopes through .editorconfig; local case-sensitive contracts stay disabled or use a normal diagnostic suppression.",
        helpLinkUri: "https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Style.Analyzers/README.md#skstyle015");

    private static void AnalyzeJsonContext(
        SymbolAnalysisContext context,
        INamedTypeSymbol serializerContextType,
        INamedTypeSymbol optionsAttributeType)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (!InheritsFrom(type, serializerContextType))
        {
            return;
        }

        var location = type.Locations.FirstOrDefault(static candidate => candidate.IsInSource);
        if (location is null)
        {
            return;
        }

        foreach (var attribute in type.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, optionsAttributeType)
                && IsCaseInsensitive(attribute))
            {
                return;
            }
        }

        context.ReportDiagnostic(Diagnostic.Create(CaseInsensitiveJsonContextRule, location, type.Name));
    }

    private static bool IsCaseInsensitive(AttributeData attribute)
    {
        // JsonSerializerDefaults.Web implies PropertyNameCaseInsensitive = true.
        if (attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is JsonSerializerDefaultsWeb)
        {
            return true;
        }

        return attribute.NamedArguments.Any(static argument =>
            argument.Key == "PropertyNameCaseInsensitive" && argument.Value.Value is true);
    }

    private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }
        }

        return false;
    }
}
