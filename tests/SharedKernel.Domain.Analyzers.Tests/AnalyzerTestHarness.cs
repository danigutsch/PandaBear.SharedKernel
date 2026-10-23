using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using PandaBear.Testing.Roslyn;

namespace PandaBear.Domain.Analyzers.Tests;

internal static class AnalyzerTestHarness
{
    private const string DefaultUsings = """
        using System;
        using System.Collections;
        using System.Collections.Frozen;
        using System.Collections.Generic;
        using System.Collections.Immutable;
        using System.Collections.ObjectModel;

        """;

    public static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnostics(string source, string path = "TestSource.cs")
    {
        var compilation = CreateCompilation(source, path);
        var analyzer = new SharedKernelDomainAnalyzer();
        var optionsProvider = new TestAnalyzerConfigOptionsProvider(null);
        var compilationOptions = new CompilationWithAnalyzersOptions(
            new AnalyzerOptions([]),
            onAnalyzerException: null,
            concurrentAnalysis: true,
            logAnalyzerExecutionTime: false,
            reportSuppressedDiagnostics: false,
            analyzerExceptionFilter: null,
            _ => optionsProvider);
        var compilationWithAnalyzers = compilation.WithAnalyzers([analyzer], compilationOptions);

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
    }

    public static string GetReportedLine(Diagnostic diagnostic)
    {
        var sourceTree = diagnostic.Location.SourceTree
            ?? throw new InvalidOperationException("The diagnostic has no source location.");
        var text = sourceTree.GetText();
        return text.Lines.GetLineFromPosition(diagnostic.Location.SourceSpan.Start).ToString();
    }

    private static CSharpCompilation CreateCompilation(string source, string path)
    {
        var compilation = Testing.Roslyn.AnalyzerTestHarness.CreateCompilation(
            source,
            DefaultUsings,
            assemblyName: "SharedKernel.Domain.Analyzers.Tests.Dynamic",
            path: path);
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
