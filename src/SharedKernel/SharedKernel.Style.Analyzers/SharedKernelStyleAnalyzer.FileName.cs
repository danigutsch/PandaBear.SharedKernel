using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace PandaBear.Style.Analyzers;

public sealed partial class SharedKernelStyleAnalyzer
{
    private static readonly DiagnosticDescriptor FileNameRule = new(
        StyleDiagnosticIds.FileNameMismatch,
        title: "File name should match its top-level type",
        messageFormat: "File '{0}' declares the single top-level type '{1}'; rename the file to '{1}.cs' ('{1}{{T}}.cs' for a generic type) or rename the type",
        category: "Style",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A file that declares exactly one top-level type should be named after that type. A generic type may add its type parameters in braces, such as Result{T}.cs. Partial types, which are split across files by design, generated files, files with several top-level types, and nested types are not checked.",
        helpLinkUri: "https://github.com/danigutsch/PandaBear.SharedKernel/blob/main/src/SharedKernel/SharedKernel.Style.Analyzers/README.md#skstyle016");

    private static void AnalyzeFileName(SyntaxTreeAnalysisContext context)
    {
        if (string.IsNullOrEmpty(context.Tree.FilePath)
            || context.Tree.GetRoot(context.CancellationToken) is not CompilationUnitSyntax root)
        {
            return;
        }

        var topLevelTypes = GetTopLevelTypes(root).ToArray();
        if (topLevelTypes.Length != 1 || IsPartialType(topLevelTypes[0]))
        {
            return;
        }

        var typeName = GetTypeName(topLevelTypes[0]);
        var fileName = Path.GetFileNameWithoutExtension(context.Tree.FilePath);
        if (string.Equals(fileName, typeName, StringComparison.Ordinal) || IsGenericFileName(fileName, typeName, topLevelTypes[0]))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            FileNameRule,
            topLevelTypes[0].GetLocation(),
            Path.GetFileName(context.Tree.FilePath),
            typeName));
    }

    private static bool IsGenericFileName(string fileName, string typeName, MemberDeclarationSyntax type)
    {
        var typeParameters = type switch
        {
            TypeDeclarationSyntax declaration => declaration.TypeParameterList,
            DelegateDeclarationSyntax @delegate => @delegate.TypeParameterList,
            _ => null,
        };

        return typeParameters is not null
            && string.Equals(fileName, typeName + "{" + string.Join(",", typeParameters.Parameters.Select(static parameter => parameter.Identifier.ValueText)) + "}", StringComparison.Ordinal);
    }
}
