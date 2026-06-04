using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using SharedKernel.Testing.Roslyn;

namespace SharedKernel.Style.Analyzers.Tests;

internal static class AnalyzerTestHarness
{
    private const string DefaultUsings = """
        using System;
        using System.Collections.Generic;
        using System.IO;
        using System.Threading;
        using System.Threading.Tasks;

        """;

    public static CSharpCompilation CreateCompilation(
        string source,
        string assemblyName = "SharedKernel.Style.Analyzers.Tests.Dynamic")
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(DefaultUsings + source, new CSharpParseOptions(LanguageVersion.Preview));

        return CSharpCompilation.Create(
            assemblyName,
            [syntaxTree],
            GetMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    public static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnostics(
        string source,
        ImmutableDictionary<string, string>? globalOptions = null,
        string assemblyName = "SharedKernel.Style.Analyzers.Tests.Dynamic")
    {
        var compilation = CreateCompilation(source, assemblyName);
        var analyzer = new SharedKernelStyleAnalyzer();
        var optionsProvider = new TestAnalyzerConfigOptionsProvider(globalOptions);
        var analyzerOptions = new AnalyzerOptions([]);
        var compilationOptions = new CompilationWithAnalyzersOptions(
            analyzerOptions,
            onAnalyzerException: null,
            concurrentAnalysis: true,
            logAnalyzerExecutionTime: false,
            reportSuppressedDiagnostics: false,
            analyzerExceptionFilter: null,
            _ => optionsProvider);
        var compilationWithAnalyzers = compilation.WithAnalyzers([analyzer], compilationOptions);

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
    }

    private static IEnumerable<MetadataReference> GetMetadataReferences()
    {
        var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        Assert.False(string.IsNullOrWhiteSpace(trustedPlatformAssemblies));
        var trustedAssemblyPaths = Assert.IsType<string>(trustedPlatformAssemblies);

        foreach (var path in trustedAssemblyPaths.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            yield return MetadataReference.CreateFromFile(path);
        }
    }
}
