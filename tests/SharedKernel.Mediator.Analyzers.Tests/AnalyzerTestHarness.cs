using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using SharedKernel.Testing.Roslyn;
namespace SharedKernel.Mediator.Analyzers.Tests;

internal static class AnalyzerTestHarness
{
    private const string DefaultUsings = """
        using System;
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;

        """;

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
