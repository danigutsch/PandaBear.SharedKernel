using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using PandaBear.Testing.Roslyn;

namespace PandaBear.EntityFrameworkCore.Analyzers.Tests;

/// <summary>Compiles test source against the default usings and runs the Entity Framework Core analyzer over it.</summary>
internal static class AnalyzerTestHarness
{
    private const string DefaultUsings = """
        using System;
        using System.Collections.Generic;
        using Microsoft.EntityFrameworkCore;
        using Microsoft.EntityFrameworkCore.Metadata.Builders;

        """;

    private const string SharedKernelUsing = "using PandaBear.EntityFrameworkCore;\n";

    /// <summary>Compiles the source and returns the diagnostics reported by SharedKernelEntityFrameworkCoreAnalyzer, throwing when the source has compile errors.</summary>
    public static Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnostics(string source) =>
        GetAnalyzerDiagnostics(source, withSharedKernel: true);

    /// <summary>Compiles the source, optionally without a reference to the SharedKernel.EntityFrameworkCore assembly, and returns the analyzer diagnostics.</summary>
    public static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnostics(string source, bool withSharedKernel)
    {
        var compilation = CreateCompilation(source, withSharedKernel);
        var optionsProvider = new TestAnalyzerConfigOptionsProvider(null);
        var compilationOptions = new CompilationWithAnalyzersOptions(
            new AnalyzerOptions([]),
            onAnalyzerException: null,
            concurrentAnalysis: true,
            logAnalyzerExecutionTime: false,
            reportSuppressedDiagnostics: false,
            analyzerExceptionFilter: null,
            _ => optionsProvider);
        var compilationWithAnalyzers = compilation.WithAnalyzers([new SharedKernelEntityFrameworkCoreAnalyzer()], compilationOptions);

        return await compilationWithAnalyzers.GetAnalyzerDiagnosticsAsync().ConfigureAwait(false);
    }

    /// <summary>Returns the full source line on which the diagnostic starts, throwing when the diagnostic has no source location.</summary>
    public static string GetReportedLine(Diagnostic diagnostic)
    {
        var sourceTree = diagnostic.Location.SourceTree
            ?? throw new InvalidOperationException("The diagnostic has no source location.");
        return sourceTree.GetText().Lines.GetLineFromPosition(diagnostic.Location.SourceSpan.Start).ToString();
    }

    private static CSharpCompilation CreateCompilation(string source, bool withSharedKernel)
    {
        var compilation = Testing.Roslyn.AnalyzerTestHarness.CreateCompilation(
            source,
            withSharedKernel ? DefaultUsings + SharedKernelUsing : DefaultUsings,
            assemblyName: "SharedKernel.EntityFrameworkCore.Analyzers.Tests.Dynamic");
        if (!withSharedKernel)
        {
            var sharedKernel = compilation.References
                .Where(static reference => reference.Display?.EndsWith("SharedKernel.EntityFrameworkCore.dll", StringComparison.Ordinal) == true)
                .ToArray();
            compilation = compilation.RemoveReferences(sharedKernel);
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
}
