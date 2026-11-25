using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SharedKernel.Testing.Roslyn;

namespace SharedKernel.Style.Analyzers.Tests;

/// <summary>
/// Style analyzer test harness that compiles source with default usings and xUnit references and returns analyzer diagnostics.
/// </summary>
internal static class AnalyzerTestHarness
{
    /// <summary>Returns true when the diagnostic is reported inside the test source's <c>TourLoader</c> type.</summary>
    public static bool IsInsideTourLoader(Diagnostic diagnostic)
    {
        var text = diagnostic.Location.SourceTree!.ToString();
        return diagnostic.Location.SourceSpan.Start > text.IndexOf("class TourLoader", StringComparison.Ordinal);
    }

    private const string DefaultUsings = """
        using System;
        using System.Collections.Generic;
        using System.IO;
        using System.Threading;
        using System.Threading.Tasks;
        using Xunit;

        """;

    /// <summary>
    /// Compiles source with default usings and the xUnit assembly referenced.
    /// </summary>
    /// <param name="source">The C# source to compile.</param>
    /// <param name="assemblyName">The name of the compiled assembly.</param>
    /// <param name="path">The file path given to the syntax tree.</param>
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

    /// <summary>
    /// Compiles the source, runs the SharedKernel style analyzer with the given global options, and returns the resulting diagnostics.
    /// </summary>
    /// <param name="source">The C# source to analyze.</param>
    /// <param name="globalOptions">Global analyzer config options, or null for none.</param>
    /// <param name="assemblyName">The name of the compiled assembly.</param>
    /// <param name="path">The file path given to the syntax tree.</param>
    public static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnostics(
        string source,
        ImmutableDictionary<string, string>? globalOptions = null,
        string assemblyName = "SharedKernel.Style.Analyzers.Tests.Dynamic",
        string path = "TestSource.cs")
    {
        var compilation = CreateCompilation(source, assemblyName, path);
        var analyzer = new StyleAnalyzer();
        return await AnalyzerRun.GetDiagnostics(compilation, analyzer, globalOptions).ConfigureAwait(false);
    }

}
