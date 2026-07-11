using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using SharedKernel.Testing.Roslyn;

namespace SharedKernel.Testing.Analyzers.Tests;

internal static class AnalyzerTestHarness
{
    private const string DefaultUsings = """
        using System;
        using System.Collections.Generic;
        using System.IO;
        using System.Threading;
        using System.Threading.Tasks;
        using Xunit;

        """;

    public static CSharpCompilation CreateCompilation(
        string source,
        string assemblyName = "SharedKernel.Testing.Analyzers.Tests.Dynamic")
    {
        return Roslyn.AnalyzerTestHarness.CreateCompilation(
            source,
            DefaultUsings,
            [typeof(FactAttribute).Assembly],
            assemblyName);
    }

    public static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnostics(
        string source,
        string assemblyName = "SharedKernel.Testing.Analyzers.Tests.Dynamic",
        ImmutableDictionary<string, string>? analyzerOptions = null)
    {
        var compilation = CreateCompilation(source, assemblyName);
        var analyzer = new SharedKernelTestingAnalyzer();
        var optionsProvider = new TestAnalyzerConfigOptionsProvider(analyzerOptions ?? ImmutableDictionary<string, string>.Empty);
        var options = new AnalyzerOptions([]);
        var compilationOptions = new CompilationWithAnalyzersOptions(
            options,
            onAnalyzerException: null,
            concurrentAnalysis: true,
            logAnalyzerExecutionTime: false,
            reportSuppressedDiagnostics: false,
            analyzerExceptionFilter: null,
            _ => (AnalyzerConfigOptionsProvider)optionsProvider);
        var compilationWithAnalyzers = compilation.WithAnalyzers([analyzer], compilationOptions);

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
    }

}
