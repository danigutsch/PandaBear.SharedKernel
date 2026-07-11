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
        using Xunit;

        """;

    public static CSharpCompilation CreateCompilation(
        string source,
        string assemblyName = "SharedKernel.Style.Analyzers.Tests.Dynamic",
        string path = "TestSource.cs")
    {
        return Testing.Roslyn.AnalyzerTestHarness.CreateCompilation(
            source,
            DefaultUsings,
            [typeof(FactAttribute).Assembly],
            assemblyName,
            path);
    }

    public static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnostics(
        string source,
        ImmutableDictionary<string, string>? globalOptions = null,
        string assemblyName = "SharedKernel.Style.Analyzers.Tests.Dynamic",
        string path = "TestSource.cs")
    {
        var compilation = CreateCompilation(source, assemblyName, path);
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

}
