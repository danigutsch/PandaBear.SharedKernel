using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace PandaBear.Style.Analyzers;

public sealed partial class SharedKernelStyleAnalyzer
{
    private static readonly DiagnosticDescriptor CurrentTimeRule = new(
        StyleDiagnosticIds.DirectCurrentTime,
        "Use TimeProvider for current time",
        "Inject or pass TimeProvider and use GetUtcNow() or GetLocalNow() instead of {0}.{1}",
        "Style", DiagnosticSeverity.Warning, true,
        helpLinkUri: "https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Style.Analyzers/README.md#skstyle014");

    private static void AnalyzeCurrentTime(OperationAnalysisContext context)
    {
        var property = ((IPropertyReferenceOperation)context.Operation).Property;
        if (!property.IsStatic || property.Name is not ("Now" or "UtcNow" or "Today"))
        {
            return;
        }

        if (property.ContainingType.ToDisplayString() is "System.DateTime" or "System.DateTimeOffset")
        {
            context.ReportDiagnostic(Diagnostic.Create(CurrentTimeRule, context.Operation.Syntax.GetLocation(), property.ContainingType.Name, property.Name));
        }
    }
}
