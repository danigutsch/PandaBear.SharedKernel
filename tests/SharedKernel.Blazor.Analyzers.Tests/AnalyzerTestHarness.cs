using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using PandaBear.Testing.Roslyn;

namespace PandaBear.Blazor.Analyzers.Tests;

/// <summary>Compiles test source against the default usings and runs the Blazor analyzer over it.</summary>
internal static class AnalyzerTestHarness
{
    private const string DefaultUsings = """
        using System;
        using System.Collections.Generic;
        using System.Threading.Tasks;
        using Microsoft.AspNetCore.Components;

        """;

    /// <summary>Compiles the source and returns the diagnostics reported by SharedKernelBlazorAnalyzer, throwing when the source has compile errors.</summary>
    public static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnostics(string source)
    {
        var compilation = CreateCompilation(source);
        var optionsProvider = new TestAnalyzerConfigOptionsProvider(null);
        var compilationOptions = new CompilationWithAnalyzersOptions(
            new AnalyzerOptions([]),
            onAnalyzerException: null,
            concurrentAnalysis: true,
            logAnalyzerExecutionTime: false,
            reportSuppressedDiagnostics: false,
            analyzerExceptionFilter: null,
            _ => optionsProvider);
        var compilationWithAnalyzers = compilation.WithAnalyzers([new SharedKernelBlazorAnalyzer()], compilationOptions);

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
    }

    /// <summary>Returns the full source line on which the diagnostic starts, throwing when the diagnostic has no source location.</summary>
    public static string GetReportedLine(Diagnostic diagnostic)
    {
        var sourceTree = diagnostic.Location.SourceTree
            ?? throw new InvalidOperationException("The diagnostic has no source location.");
        return sourceTree.GetText().Lines.GetLineFromPosition(diagnostic.Location.SourceSpan.Start).ToString();
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        var compilation = Testing.Roslyn.AnalyzerTestHarness.CreateCompilation(
            source,
            DefaultUsings,
            assemblyName: "SharedKernel.Blazor.Analyzers.Tests.Dynamic");
        var errors = compilation.GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        if (errors.Length != 0)
        {
            throw new InvalidOperationException($"Test source does not compile: {string.Join(Environment.NewLine, errors.Select(static error => error.ToString()))}");
        }

        return compilation;
    }
}
