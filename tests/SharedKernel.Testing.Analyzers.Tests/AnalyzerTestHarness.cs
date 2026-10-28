using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using PandaBear.Testing.Roslyn;

namespace PandaBear.Testing.Analyzers.Tests;

/// <summary>Test helper that compiles source with the standard usings and xunit reference and runs SharedKernelTestingAnalyzer over it.</summary>
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

    /// <summary>Compiles the source with System, collections, IO, threading, tasks and Xunit usings prepended and the xunit assembly referenced.</summary>
    /// <param name="source">C# source to compile.</param>
    /// <param name="assemblyName">Assembly name of the compilation.</param>
    /// <param name="path">File path given to the source's syntax tree.</param>
    public static CSharpCompilation CreateCompilation(
        string source,
        string assemblyName = "SharedKernel.Testing.Analyzers.Tests.Dynamic",
        string path = "TestSource.cs")
    {
        return Roslyn.AnalyzerTestHarness.CreateCompilation(
            source,
            DefaultUsings,
            [typeof(FactAttribute).Assembly, typeof(TestScopeAttribute).Assembly],
            assemblyName,
            path);
    }

    /// <summary>Compiles the source, runs SharedKernelTestingAnalyzer with the supplied analyzer config options, and returns the diagnostics it reports.</summary>
    /// <param name="source">C# source to analyze.</param>
    /// <param name="assemblyName">Assembly name of the compilation.</param>
    /// <param name="analyzerOptions">Analyzer config key/value options, or null for none.</param>
    /// <param name="path">File path given to the source's syntax tree.</param>
    public static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnostics(
        string source,
        string assemblyName = "SharedKernel.Testing.Analyzers.Tests.Dynamic",
        ImmutableDictionary<string, string>? analyzerOptions = null,
        string path = "TestSource.cs")
    {
        var compilation = CreateCompilation(source, assemblyName, path);
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

    /// <summary>Runs SharedKernelTestingAnalyzer over the source and returns only the diagnostics with the given id.</summary>
    /// <param name="diagnosticId">Diagnostic id to keep.</param>
    /// <param name="source">C# source to analyze.</param>
    /// <param name="analyzerOptions">Analyzer config key/value options, or null for none.</param>
    public static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsById(
        string diagnosticId,
        string source,
        ImmutableDictionary<string, string>? analyzerOptions = null)
    {
        var diagnostics = await GetAnalyzerDiagnostics(source, analyzerOptions: analyzerOptions).ConfigureAwait(false);

        return [.. diagnostics.Where(diagnostic => diagnostic.Id == diagnosticId)];
    }

}
