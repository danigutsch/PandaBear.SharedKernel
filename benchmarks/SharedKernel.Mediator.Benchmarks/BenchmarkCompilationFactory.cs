using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SharedKernel.Mediator.SourceGenerator;

namespace SharedKernel.Mediator.Benchmarks;

/// <summary>
/// Creates benchmark compilations and runs the discovery generator against them.
/// </summary>
internal static class BenchmarkCompilationFactory
{
    private const string DefaultUsings = """
        using System;
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;

        """;

    /// <summary>
    /// Creates a compilation for the provided benchmark source.
    /// </summary>
    /// <param name="source">The benchmark input source.</param>
    /// <returns>The Roslyn compilation used by the benchmarks.</returns>
    public static CSharpCompilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(DefaultUsings + source, new CSharpParseOptions(LanguageVersion.Preview));

        return CSharpCompilation.Create(
            "SharedKernel.Mediator.Benchmarks.Dynamic",
            [syntaxTree],
            GetMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>
    /// Creates a fresh generator driver instance.
    /// </summary>
    /// <returns>The generator driver used by the benchmark scenarios.</returns>
    public static GeneratorDriver CreateGeneratorDriver()
    {
        return CSharpGeneratorDriver.Create(new SharedKernelMediatorGenerator());
    }

    /// <summary>
    /// Runs the discovery generator and returns the emitted source length.
    /// </summary>
    /// <param name="compilation">The compilation to run against.</param>
    /// <param name="generatorDriver">
    /// An optional existing driver used to simulate repeat incremental runs.
    /// </param>
    /// <returns>The emitted discovery-report source length.</returns>
    public static int GetGeneratedSourceLength(CSharpCompilation compilation, GeneratorDriver? generatorDriver = null)
    {
        var driver = (generatorDriver ?? CreateGeneratorDriver()).RunGenerators(compilation);
        var generatedSource = driver.GetRunResult().Results.Single().GeneratedSources.Single().SourceText.ToString();
        return generatedSource.Length;
    }

    private static List<MetadataReference> GetMetadataReferences()
    {
        var trustedPlatformAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        ArgumentException.ThrowIfNullOrEmpty(trustedPlatformAssemblies);

        var references = trustedPlatformAssemblies
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(static path => MetadataReference.CreateFromFile(path))
            .Cast<MetadataReference>()
            .ToList();

        references.Add(MetadataReference.CreateFromFile(typeof(IRequest<>).Assembly.Location));
        return references;
    }
}
