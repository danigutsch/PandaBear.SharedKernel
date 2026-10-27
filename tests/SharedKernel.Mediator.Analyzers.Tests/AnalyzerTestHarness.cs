using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using PandaBear.Testing.Roslyn;
namespace PandaBear.Mediator.Analyzers.Tests;

/// <summary>Compiles test source against the Mediator abstractions and runs the mediator analyzer over it.</summary>
internal static class AnalyzerTestHarness
{
    private const string DefaultUsings = """
        using System;
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;

        """;

    /// <summary>Compiles the source together with the default usings and a reference to the Mediator abstractions assembly under the given assembly name.</summary>
    public static CSharpCompilation CreateCompilation(
        string source,
        string assemblyName = "SharedKernel.Mediator.Analyzers.Tests.Dynamic")
    {
        return Testing.Roslyn.AnalyzerTestHarness.CreateCompilation(
            source,
            DefaultUsings,
            [typeof(IRequest<>).Assembly],
            assemblyName);
    }

    /// <summary>Runs SharedKernelMediatorAnalyzer over the source with optional global analyzer config options and returns the reported diagnostics.</summary>
    public static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnostics(
        string source,
        ImmutableDictionary<string, string>? globalOptions = null)
    {
        var compilation = CreateCompilation(source);
        var analyzer = new SharedKernelMediatorAnalyzer();
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
