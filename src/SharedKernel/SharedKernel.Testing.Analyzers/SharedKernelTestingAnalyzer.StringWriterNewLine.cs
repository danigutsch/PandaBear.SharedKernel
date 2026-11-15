using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace PandaBear.Testing.Analyzers;

public sealed partial class SharedKernelTestingAnalyzer
{
    private static readonly DiagnosticDescriptor StringWriterNewLineRule = new(
        TestingDiagnosticIds.StringWriterNewLine,
        "Capture text with InvariantStringWriter",
        "Use PandaBear.Testing.InvariantStringWriter instead of StringWriter, or set NewLine in the initializer; the captured text ends lines with the platform line ending",
        TestingCategory, DiagnosticSeverity.Warning, true,
        description: "A StringWriter ends each line with Environment.NewLine, which is \"\\r\\n\" on Windows and \"\\n\" elsewhere, so a test that compares the captured text with a \"\\n\" expectation fails only on Windows. InvariantStringWriter ends lines with \"\\n\" and formats with the invariant culture on every operating system. A test that needs a specific line ending sets NewLine in the object initializer.",
        helpLinkUri: "https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Testing.Analyzers/README.md#sktest022");

    // Only test projects are checked: a production StringWriter usually feeds a platform-native file or console on purpose.
    private static void RegisterStringWriterNewLineAnalysis(CompilationStartAnalysisContext compilationContext)
    {
        var compilation = compilationContext.Compilation;
        var stringWriter = compilation.GetTypeByMetadataName("System.IO.StringWriter");
        if (stringWriter is null || compilation.GetTypeByMetadataName("Xunit.FactAttribute") is null)
        {
            return;
        }

        compilationContext.RegisterOperationAction(
            operationContext => AnalyzeStringWriterCreation(operationContext, stringWriter),
            OperationKind.ObjectCreation);
    }

    private static void AnalyzeStringWriterCreation(OperationAnalysisContext context, INamedTypeSymbol stringWriter)
    {
        var creation = (IObjectCreationOperation)context.Operation;
        if (!SymbolEqualityComparer.Default.Equals(creation.Type, stringWriter) || SetsNewLine(creation.Initializer))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(StringWriterNewLineRule, creation.Syntax.GetLocation()));
    }

    private static bool SetsNewLine(IObjectOrCollectionInitializerOperation? initializer)
    {
        return initializer is not null
            && initializer.Initializers.Any(static member => member is ISimpleAssignmentOperation
            {
                Target: IPropertyReferenceOperation { Property.Name: "NewLine" },
            });
    }
}
