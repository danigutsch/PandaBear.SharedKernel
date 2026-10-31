using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace PandaBear.Testing.Roslyn;

/// <summary>
/// Provides the mechanical setup shared by incremental source generator test suites: compilations, drivers, runs, and
/// generated-source lookup. Generator-specific sources, usings, references, and assertions stay in each test project.
/// </summary>
public static class GeneratorTestHarness
{
    /// <summary>
    /// Creates a dynamically linked library compilation that references the trusted platform assemblies.
    /// </summary>
    /// <param name="syntaxTrees">The syntax trees to compile.</param>
    /// <param name="assemblyName">The compilation assembly name.</param>
    /// <param name="additionalReferences">References added after the trusted platform assemblies.</param>
    /// <returns>The compilation.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="syntaxTrees"/> or <paramref name="assemblyName"/> is <see langword="null"/>.</exception>
    public static CSharpCompilation CreateCompilation(
        IEnumerable<SyntaxTree> syntaxTrees,
        string assemblyName,
        IEnumerable<MetadataReference>? additionalReferences = null)
    {
        ArgumentNullException.ThrowIfNull(syntaxTrees);
        ArgumentNullException.ThrowIfNull(assemblyName);

        return CSharpCompilation.Create(
            assemblyName,
            syntaxTrees,
            [.. AnalyzerTestHarness.GetTrustedPlatformReferences(), .. additionalReferences ?? []],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>
    /// Creates a generator driver. Run it repeatedly to observe incremental caching.
    /// </summary>
    /// <param name="generators">The generators to run.</param>
    /// <param name="parseOptions">The parse options of the compilation, usually from its first syntax tree.</param>
    /// <param name="globalOptions">The analyzer config global options the generators read.</param>
    /// <param name="trackIncrementalGeneratorSteps">Whether the driver records per-step outputs for caching assertions.</param>
    /// <returns>The driver.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="generators"/> is <see langword="null"/>.</exception>
    public static GeneratorDriver CreateDriver(
        IEnumerable<IIncrementalGenerator> generators,
        CSharpParseOptions? parseOptions,
        ImmutableDictionary<string, string>? globalOptions = null,
        bool trackIncrementalGeneratorSteps = false)
    {
        ArgumentNullException.ThrowIfNull(generators);

        return CSharpGeneratorDriver.Create(
            generators: [.. generators.Select(static generator => generator.AsSourceGenerator())],
            additionalTexts: [],
            parseOptions: parseOptions,
            optionsProvider: new TestAnalyzerConfigOptionsProvider(globalOptions),
            driverOptions: new GeneratorDriverOptions(
                disabledOutputs: IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: trackIncrementalGeneratorSteps));
    }

    /// <summary>
    /// Runs one generator over a compilation with a fresh driver.
    /// </summary>
    /// <param name="generator">The generator to run.</param>
    /// <param name="compilation">The compilation to generate from.</param>
    /// <param name="globalOptions">The analyzer config global options the generator reads.</param>
    /// <returns>The run result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="generator"/> or <paramref name="compilation"/> is <see langword="null"/>.</exception>
    public static GeneratorDriverRunResult Run(
        IIncrementalGenerator generator,
        CSharpCompilation compilation,
        ImmutableDictionary<string, string>? globalOptions = null)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(compilation);

        return CreateDriver([generator], GetParseOptions(compilation), globalOptions)
            .RunGenerators(compilation)
            .GetRunResult();
    }

    /// <summary>
    /// Runs generators over a compilation with a fresh driver and returns the compilation that includes the generated sources.
    /// </summary>
    /// <param name="generators">The generators to run.</param>
    /// <param name="compilation">The compilation to generate from.</param>
    /// <param name="outputCompilation">The compilation with the generated sources added.</param>
    /// <param name="diagnostics">The diagnostics reported while generating.</param>
    /// <returns>The run result.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="generators"/> or <paramref name="compilation"/> is <see langword="null"/>.</exception>
    public static GeneratorDriverRunResult RunAndUpdate(
        IEnumerable<IIncrementalGenerator> generators,
        CSharpCompilation compilation,
        out CSharpCompilation outputCompilation,
        out ImmutableArray<Diagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(generators);
        ArgumentNullException.ThrowIfNull(compilation);

        var driver = CreateDriver(generators, GetParseOptions(compilation))
            .RunGeneratorsAndUpdateCompilation(compilation, out var updated, out diagnostics);
        outputCompilation = (CSharpCompilation)updated;
        return driver.GetRunResult();
    }

    /// <summary>
    /// Gets the text of the generated source with the given hint name.
    /// </summary>
    /// <param name="runResult">The run result.</param>
    /// <param name="hintName">The hint name the generator passed to <c>AddSource</c>.</param>
    /// <returns>The generated source text.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no generated source has the hint name.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="runResult"/> or <paramref name="hintName"/> is <see langword="null"/>.</exception>
    public static string GetGeneratedSource(GeneratorDriverRunResult runResult, string hintName)
    {
        ArgumentNullException.ThrowIfNull(runResult);
        ArgumentNullException.ThrowIfNull(hintName);

        var generatedSource = runResult.Results
            .SelectMany(static result => result.GeneratedSources)
            .SingleOrDefault(source => string.Equals(source.HintName, hintName, StringComparison.Ordinal));

        return generatedSource.SourceText is null
            ? throw new InvalidOperationException($"Generated source not found: {hintName}")
            : generatedSource.SourceText.ToString();
    }

    /// <summary>
    /// Gets the diagnostics every generator in the run reported.
    /// </summary>
    /// <param name="runResult">The run result.</param>
    /// <returns>The diagnostics.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="runResult"/> is <see langword="null"/>.</exception>
    public static ImmutableArray<Diagnostic> GetDiagnostics(GeneratorDriverRunResult runResult)
    {
        ArgumentNullException.ThrowIfNull(runResult);

        return [.. runResult.Results.SelectMany(static result => result.Diagnostics)];
    }

    private static CSharpParseOptions? GetParseOptions(CSharpCompilation compilation) =>
        (CSharpParseOptions?)compilation.SyntaxTrees.FirstOrDefault()?.Options;
}
