using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SharedKernel.Mediator.SourceGenerator;
using System.Reflection;
using System.Runtime.Loader;

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
    /// <param name="assemblyName">The dynamic assembly name.</param>
    /// <returns>The Roslyn compilation used by the benchmarks.</returns>
    public static CSharpCompilation CreateCompilation(
        string source,
        string assemblyName = "SharedKernel.Mediator.Benchmarks.Dynamic")
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(DefaultUsings + source, new CSharpParseOptions(LanguageVersion.Preview));

        return CSharpCompilation.Create(
            assemblyName,
            [syntaxTree],
            GetMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>
    /// Emits and loads an in-memory benchmark assembly.
    /// </summary>
    /// <param name="source">The benchmark source to compile.</param>
    /// <param name="assemblyName">The dynamic assembly name.</param>
    /// <returns>The loaded benchmark assembly.</returns>
    public static Assembly LoadAssembly(string source, string assemblyName)
    {
        var compilation = CreateCompilation(source, assemblyName);
        using var stream = new MemoryStream();
        EmitAssembly(compilation, stream);
        stream.Position = 0;
        return new AssemblyLoadContext(assemblyName, isCollectible: true).LoadFromStream(stream);
    }

    /// <summary>
    /// Emits the provided benchmark source and returns the emitted assembly size in bytes.
    /// </summary>
    /// <param name="source">The benchmark source to compile.</param>
    /// <param name="assemblyName">The dynamic assembly name.</param>
    /// <returns>The emitted assembly size in bytes.</returns>
    public static int GetEmittedAssemblySize(string source, string assemblyName)
    {
        var compilation = CreateCompilation(source, assemblyName);
        using var stream = new MemoryStream();
        EmitAssembly(compilation, stream);
        return checked((int)stream.Length);
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

    /// <summary>
    /// Runs the generator and returns the total emitted source length across all generated files.
    /// </summary>
    /// <param name="compilation">The compilation to run against.</param>
    /// <param name="generatorDriver">
    /// An optional existing driver used to simulate repeat incremental runs.
    /// </param>
    /// <returns>The total emitted source length across generated files.</returns>
    public static int GetTotalGeneratedSourceLength(CSharpCompilation compilation, GeneratorDriver? generatorDriver = null)
    {
        var driver = (generatorDriver ?? CreateGeneratorDriver()).RunGenerators(compilation);
        return driver.GetRunResult().Results.Single().GeneratedSources.Sum(static source => source.SourceText.Length);
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
        references.Add(MetadataReference.CreateFromFile(typeof(SharedKernelMediatorActivitySource).Assembly.Location));
        return references;
    }

    private static void EmitAssembly(CSharpCompilation compilation, MemoryStream stream)
    {
        var emitResult = compilation.Emit(stream);

        if (!emitResult.Success)
        {
            var diagnostics = string.Join(
                Environment.NewLine,
                emitResult.Diagnostics.Select(static diagnostic => diagnostic.ToString()));
            throw new InvalidOperationException($"Failed to emit benchmark compilation:{Environment.NewLine}{diagnostics}");
        }
    }
}
