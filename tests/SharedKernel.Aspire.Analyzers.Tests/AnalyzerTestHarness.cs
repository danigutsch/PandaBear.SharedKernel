using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using PandaBear.Testing.Roslyn;

namespace PandaBear.Aspire.Analyzers.Tests;

/// <summary>
/// Builds in-memory compilations and runs SharedKernelAspireAnalyzer over them for the Aspire analyzer tests.
/// </summary>
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

    /// <summary>
    /// Compiles the given source with default usings and the xUnit assembly referenced, under the given assembly name and file path.
    /// </summary>
    public static CSharpCompilation CreateCompilation(
        string source,
        string assemblyName = "SharedKernel.Aspire.Analyzers.Tests.Dynamic",
        string path = "TestSource.cs")
    {
        return Testing.Roslyn.AnalyzerTestHarness.CreateCompilation(
            source,
            DefaultUsings,
            [typeof(FactAttribute).Assembly],
            assemblyName,
            path);
    }

    /// <summary>
    /// Compiles the given source and returns the diagnostics reported by SharedKernelAspireAnalyzer.
    /// </summary>
    public static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnostics(
        string source,
        string assemblyName = "SharedKernel.Aspire.Analyzers.Tests.Dynamic",
        string path = "TestSource.cs")
    {
        var compilation = CreateCompilation(source, assemblyName, path);
        var analyzer = new SharedKernelAspireAnalyzer();
        var optionsProvider = new TestAnalyzerConfigOptionsProvider(null);
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
