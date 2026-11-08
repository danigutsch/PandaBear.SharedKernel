using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using PandaBear.Testing.Roslyn;

namespace PandaBear.Results.Analyzers.Tests;

/// <summary>Compiles test source against the default usings and runs the Results analyzer over it.</summary>
internal static class AnalyzerTestHarness
{
    private const string DefaultUsings = """
        using System;
        using System.Collections.Generic;
        using System.Threading.Tasks;
        using Microsoft.AspNetCore.Builder;
        using Microsoft.AspNetCore.Http;
        using Microsoft.AspNetCore.Routing;
        using PandaBear.Results;
        using PandaBear.Results.AspNet;

        """;

    /// <summary>Compiles the source and returns the diagnostics reported by SharedKernelResultsAnalyzer, throwing when the source has compile errors.</summary>
    public static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnostics(string source) =>
        await Analyze(CreateCompilation(source, withResultsReference: true)).ConfigureAwait(false);

    /// <summary>Same as <see cref="GetAnalyzerDiagnostics"/> but the compilation does not reference SharedKernel.Results; the source must not use it.</summary>
    public static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsWithoutResultsReference(string source) =>
        await Analyze(CreateCompilation(source, withResultsReference: false)).ConfigureAwait(false);

    /// <summary>Returns the full source line on which the diagnostic starts, throwing when the diagnostic has no source location.</summary>
    public static string GetReportedLine(Diagnostic diagnostic)
    {
        var sourceTree = diagnostic.Location.SourceTree
            ?? throw new InvalidOperationException("The diagnostic has no source location.");
        return sourceTree.GetText().Lines.GetLineFromPosition(diagnostic.Location.SourceSpan.Start).ToString();
    }

    /// <summary>Returns the source text covered by the diagnostic location, throwing when the diagnostic has no source location.</summary>
    public static string GetReportedText(Diagnostic diagnostic)
    {
        var sourceTree = diagnostic.Location.SourceTree
            ?? throw new InvalidOperationException("The diagnostic has no source location.");
        return sourceTree.GetText().ToString(diagnostic.Location.SourceSpan);
    }

    private static async Task<ImmutableArray<Diagnostic>> Analyze(CSharpCompilation compilation)
    {
        var optionsProvider = new TestAnalyzerConfigOptionsProvider(null);
        var compilationOptions = new CompilationWithAnalyzersOptions(
            new AnalyzerOptions([]),
            onAnalyzerException: null,
            concurrentAnalysis: true,
            logAnalyzerExecutionTime: false,
            reportSuppressedDiagnostics: false,
            analyzerExceptionFilter: null,
            _ => optionsProvider);
        var compilationWithAnalyzers = compilation.WithAnalyzers([new SharedKernelResultsAnalyzer()], compilationOptions);

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
    }

    private static CSharpCompilation CreateCompilation(string source, bool withResultsReference)
    {
        var compilation = Testing.Roslyn.AnalyzerTestHarness.CreateCompilation(
            source,
            withResultsReference ? DefaultUsings : DefaultUsings.Replace("using PandaBear.Results;", string.Empty, StringComparison.Ordinal)
                .Replace("using PandaBear.Results.AspNet;", string.Empty, StringComparison.Ordinal),
            assemblyName: "SharedKernel.Results.Analyzers.Tests.Dynamic");
        if (!withResultsReference)
        {
            var resultsReferences = compilation.References
                .Where(static reference => IsResultsReference(reference.Display))
                .ToArray();
            compilation = compilation.RemoveReferences(resultsReferences);
        }

        var errors = compilation.GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToArray();
        if (errors.Length != 0)
        {
            throw new InvalidOperationException($"Test source does not compile: {string.Join(Environment.NewLine, errors.Select(static error => error.ToString()))}");
        }

        return compilation;
    }

    private static bool IsResultsReference(string? display) =>
        display is not null
        && (display.EndsWith("SharedKernel.Results.dll", StringComparison.Ordinal)
            || display.EndsWith("SharedKernel.Results.AspNet.dll", StringComparison.Ordinal));
}
